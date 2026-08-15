using System;
using System.Collections.Concurrent;
using UnitySenseFramework.Data;
using UnityEngine;

namespace UnitySenseFramework.Event
{
    /// <summary>
    /// 传感器事件总线：框架唯一的线程安全事件中枢。
    ///
    /// 职责边界：
    /// - MQTT 后台线程 → ConcurrentQueue → Unity 主线程分发
    /// - 对外广播 OnSensorDataUpdated（主线程安全）
    /// - 不关心设备管理、不关心业务逻辑
    ///
    /// DeviceManager 内部订阅此事件来更新设备注册表，
    /// 业务层（UI / 告警等）直接订阅此事件获取数据。
    ///
    /// 线程模型：
    ///   MQTT Thread → Enqueue(msg) → ConcurrentQueue
    ///   Unity Main Thread (Update) → Dequeue → OnSensorDataUpdated
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SensorEventBus : MonoBehaviour
    {
        #region Singleton
        private static SensorEventBus _instance;

        /// <summary>获取事件总线单例</summary>
        public static SensorEventBus Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[UnitySenseFramework] SensorEventBus");
                    _instance = go.AddComponent<SensorEventBus>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// 传感器数据更新事件。
        /// 运行在 Unity 主线程，可安全操作 UI / GameObject / Transform 等。
        /// </summary>
        public event Action<SensorMessage> OnSensorDataUpdated;

        /// <summary>
        /// 原始 MQTT 消息事件（运行在 MQTT 工作线程，禁止操作 Unity API）。
        /// 仅用于调试日志或转发到外部系统。
        /// </summary>
        public event Action<string, byte[]> OnRawMessageReceived;
        #endregion

        #region Internal Queue
        private readonly ConcurrentQueue<SensorMessage> _messageQueue = new ConcurrentQueue<SensorMessage>();
        private const int MaxProcessPerFrame = 200;

        /// <summary>
        /// 当前队列中待处理的消息数
        /// </summary>
        public int PendingCount => _messageQueue.Count;
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

        private void Update()
        {
            int processed = 0;
            while (_messageQueue.TryDequeue(out SensorMessage msg))
            {
                if (msg != null && !string.IsNullOrEmpty(msg.deviceId))
                {
                    OnSensorDataUpdated?.Invoke(msg);
                }

                if (++processed >= MaxProcessPerFrame)
                {
                    Debug.LogWarning($"[SensorEventBus] Frame limit reached ({MaxProcessPerFrame}), {_messageQueue.Count} remaining");
                    break;
                }
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
        #endregion

        #region Public API
        /// <summary>
        /// 将传感器消息入队（MQTT 线程安全调用）。
        /// 消息将在下一个 Update 帧中从主线程出队并触发事件。
        /// </summary>
        public void Enqueue(SensorMessage message)
        {
            if (message == null) return;
            _messageQueue.Enqueue(message);
        }

        /// <summary>
        /// 触发原始消息事件（MQTT 线程调用，仅用于调试）。
        /// </summary>
        public void NotifyRawMessage(string topic, byte[] payload)
        {
            OnRawMessageReceived?.Invoke(topic, payload);
        }
        #endregion
    }
}
