using System;
using System.Collections.Generic;
using System.Linq;
using UnitySenseFramework.Communication;
using UnitySenseFramework.Data;
using UnitySenseFramework.Event;
using UnitySenseFramework.Parser;
using UnityEngine;

namespace UnitySenseFramework.Device
{
    /// <summary>
    /// 设备管理器：框架核心。
    ///
    /// 职责：
    /// - 连接 MQTT Broker 并管理重连
    /// - 维护设备注册表（增删改查、离线检测）
    /// - 将 MQTT 原始数据委托给 SensorEventBus 进行线程安全分发
    ///
    /// 使用示例：
    /// <code>
    /// // 方式一：通过 DeviceManager 便捷事件订阅
    /// DeviceManager.Instance.OnSensorDataUpdated += msg => Debug.Log(msg.deviceId);
    ///
    /// // 方式二：直接订阅事件总线（同样运行在主线程）
    /// SensorEventBus.Instance.OnSensorDataUpdated += msg => Debug.Log(msg.deviceId);
    ///
    /// DeviceManager.Instance.Configure("broker.emqx.io");
    /// DeviceManager.Instance.Connect();
    /// </code>
    /// </summary>
    public class DeviceManager : MonoBehaviour
    {
        #region Singleton
        private static DeviceManager _instance;

        /// <summary>获取 DeviceManager 单例</summary>
        public static DeviceManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[UnitySenseFramework] DeviceManager");
                    _instance = go.AddComponent<DeviceManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
        #endregion

        #region Events
        /// <summary>传感器数据更新（Unity 主线程，转发自 SensorEventBus）</summary>
        public event Action<SensorMessage> OnSensorDataUpdated;

        /// <summary>设备上线</summary>
        public event Action<DeviceInfo> OnDeviceOnline;

        /// <summary>设备离线</summary>
        public event Action<DeviceInfo> OnDeviceOffline;

        /// <summary>新设备被发现</summary>
        public event Action<DeviceInfo> OnDeviceDiscovered;

        /// <summary>MQTT 连接成功</summary>
        public event Action OnMqttConnected;

        /// <summary>MQTT 断线</summary>
        public event Action<string> OnMqttDisconnected;
        #endregion

        #region Inspector Fields
        [Header("MQTT Connection")]
        [Tooltip("MQTT Broker 地址")]
        public string brokerHost = "broker.emqx.io";

        [Tooltip("MQTT Broker 端口")]
        public int brokerPort = 1883;

        [Tooltip("自动连接")]
        public bool autoConnect = true;

        [Tooltip("订阅的 MQTT 主题")]
        public string subscribeTopic = "UnitySenseBridge";

        [Header("Device Management")]
        [Tooltip("离线超时（秒）")]
        public float offlineTimeout = 10f;

        [Tooltip("自动注册新设备")]
        public bool autoRegisterDevices = true;
        #endregion

        #region MqttFactory
        /// <summary>
        /// MQTT 客户端工厂委托。默认使用 M2MqttAdapter。
        /// 替换实现示例：DeviceManager.MqttFactory = () => new MqttnetAdapter();
        /// </summary>
        public static Func<IMqttClient> MqttFactory { get; set; } = () => new M2MqttAdapter();
        #endregion

        #region Internal State
        private IMqttClient _mqtt;
        private readonly Dictionary<string, DeviceInfo> _devices = new Dictionary<string, DeviceInfo>();
        private float _reconnectTimer;
        private MqttConfig _config;
        private bool _isConnected;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // 订阅事件总线，用于内部设备追踪 + 转发到自身事件
            SensorEventBus.Instance.OnSensorDataUpdated += OnSensorDataForTracking;

            if (autoConnect)
            {
                Configure(brokerHost, brokerPort, subscribeTopic);
                Connect();
            }
        }

        private void Update()
        {
            // ---- 1. 离线检测 ----
            float now = Time.realtimeSinceStartup;
            foreach (var kvp in _devices)
            {
                var device = kvp.Value;
                if (device.isOnline && now - device.lastUnityTime > offlineTimeout)
                {
                    device.isOnline = false;
                    OnDeviceOffline?.Invoke(device);
                }
            }

            // ---- 2. 自动重连 ----
            if (_config != null && _config.autoReconnect && !_isConnected)
            {
                _reconnectTimer += Time.deltaTime;
                if (_reconnectTimer >= _config.reconnectInterval)
                {
                    _reconnectTimer = 0;
                    Debug.Log("[UnitySenseFramework] Attempting reconnect...");
                    TryConnectInternal();
                }
            }
        }

        private void OnDestroy()
        {
            SensorEventBus.Instance.OnSensorDataUpdated -= OnSensorDataForTracking;
            Disconnect();
            if (_instance == this) _instance = null;
        }
        #endregion

        #region Public API - Connection
        /// <summary>配置连接参数（在 Connect 之前调用）</summary>
        public void Configure(string host, int port = 1883, string topic = "UnitySenseBridge")
        {
            brokerHost = host;
            brokerPort = port;
            subscribeTopic = topic;

            _config = new MqttConfig
            {
                host = host,
                port = port,
                clientId = $"UnitySense_{Guid.NewGuid():N}".Substring(0, 23),
                autoReconnect = true,
                reconnectInterval = 3f
            };
        }

        /// <summary>连接到 MQTT Broker</summary>
        public void Connect()
        {
            if (_config == null)
                Configure(brokerHost, brokerPort, subscribeTopic);

            TryConnectInternal();
        }

        /// <summary>断开 MQTT 连接</summary>
        public void Disconnect()
        {
            _isConnected = false;
            try { _mqtt?.Disconnect(); } catch { }
            _mqtt = null;
            OnMqttDisconnected?.Invoke("Disconnected by user");
        }
        #endregion

        #region Public API - Device Management
        /// <summary>获取所有已知设备</summary>
        public IReadOnlyList<DeviceInfo> GetAllDevices() => _devices.Values.ToList();

        /// <summary>获取指定设备</summary>
        public DeviceInfo GetDevice(string deviceId)
        {
            _devices.TryGetValue(deviceId, out var info);
            return info;
        }

        /// <summary>获取在线设备数量</summary>
        public int OnlineDeviceCount => _devices.Values.Count(d => d.isOnline);

        /// <summary>获取设备总数</summary>
        public int DeviceCount => _devices.Count;

        /// <summary>手动注册设备（在 autoRegisterDevices = false 时使用）</summary>
        public DeviceInfo RegisterDevice(string deviceId, string displayName = "", string deviceType = "")
        {
            if (_devices.TryGetValue(deviceId, out var existing))
                return existing;

            var info = new DeviceInfo(deviceId, displayName, deviceType);
            _devices[deviceId] = info;
            OnDeviceDiscovered?.Invoke(info);
            return info;
        }

        /// <summary>移除设备</summary>
        public void UnregisterDevice(string deviceId)
        {
            _devices.Remove(deviceId);
        }

        /// <summary>发布 MQTT 消息（向 ESP32 发送指令等）</summary>
        public void Publish(string topic, string payload)
        {
            if (_mqtt == null || !_mqtt.IsConnected) return;
            byte[] data = System.Text.Encoding.UTF8.GetBytes(payload);
            _mqtt.Publish(topic, data);
        }
        #endregion

        #region Internal
        private void TryConnectInternal()
        {
            try
            {
                _mqtt = MqttFactory?.Invoke() ?? new M2MqttAdapter();
                _mqtt.OnMessageReceived += OnMqttMessageReceived;
                _mqtt.Connect(_config);

                if (_mqtt.IsConnected)
                {
                    _mqtt.Subscribe(subscribeTopic);
                    _isConnected = true;
                    _reconnectTimer = 0;
                    Debug.Log($"[UnitySenseFramework] Connected to {_config.host}:{_config.port}, subscribed to '{subscribeTopic}'");
                    OnMqttConnected?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Debug.LogWarning($"[UnitySenseFramework] Connection failed: {ex.Message}");
                OnMqttDisconnected?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// MQTT 消息回调（运行在 MQTT 工作线程）。
        /// 仅做解析 + 委托事件总线入队，不操作 Unity API。
        /// </summary>
        private void OnMqttMessageReceived(string topic, byte[] payload)
        {
            try
            {
                var message = JsonSensorParser.Parse(payload);
                if (message != null)
                {
                    SensorEventBus.Instance.Enqueue(message);
                }
                else
                {
                    Debug.LogWarning($"[UnitySenseFramework] Failed to parse message from topic '{topic}'");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnitySenseFramework] Error processing message: {ex.Message}");
            }
        }

        /// <summary>
        /// 接收事件总线数据用于设备追踪（Unity 主线程）。
        /// 更新设备缓存后，转发到 DeviceManager 自身事件。
        /// </summary>
        private void OnSensorDataForTracking(SensorMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.deviceId)) return;

            // 自动注册新设备
            if (autoRegisterDevices && !_devices.ContainsKey(message.deviceId))
            {
                var info = new DeviceInfo(message.deviceId, message.deviceId, "Unknown");
                _devices[message.deviceId] = info;
                OnDeviceDiscovered?.Invoke(info);
            }

            if (_devices.TryGetValue(message.deviceId, out var device))
            {
                bool wasOffline = !device.isOnline;
                device.isOnline = true;
                device.lastUnityTime = Time.realtimeSinceStartup;
                device.lastDataTimestamp = message.timestamp;

                if (wasOffline)
                    OnDeviceOnline?.Invoke(device);
            }

            // 转发到 DeviceManager 自身事件，方便外部便捷订阅
            OnSensorDataUpdated?.Invoke(message);
        }
        #endregion
    }
}
