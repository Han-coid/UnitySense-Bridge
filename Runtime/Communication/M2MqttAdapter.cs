using System;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace UnitySenseFramework.Communication
{
    /// <summary>
    /// M2Mqtt 适配器：将 M2Mqtt 库封装为 IMqttClient 接口。
    /// 不修改 M2Mqtt 源码，仅做薄封装层。
    /// 未来可替换为 MqttnetAdapter、WebSocketMqttAdapter 等实现。
    /// </summary>
    public class M2MqttAdapter : IMqttClient
    {
        private MqttClient _client;
        private MqttConfig _config;

        /// <inheritdoc />
        public bool IsConnected => _client != null && _client.IsConnected;

        /// <inheritdoc />
        public event Action<string, byte[]> OnMessageReceived;

        /// <inheritdoc />
        public void Connect(MqttConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            try
            {
                _client = new MqttClient(
                    config.host,
                    config.port,
                    config.useEncryption,
                    null,
                    null,
                    config.useEncryption ? MqttSslProtocols.SSLv3 : MqttSslProtocols.None
                );
                // 注册 M2Mqtt 消息回调
                _client.MqttMsgPublishReceived += OnMqttMessageReceived;

                string cid = string.IsNullOrEmpty(config.clientId)
                    ? Guid.NewGuid().ToString()
                    : config.clientId;

                _client.Connect(cid, config.username, config.password);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"[UnitySenseFramework] MQTT connection failed: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        public void Disconnect()
        {
            if (_client == null) return;

            if (_client.IsConnected)
                _client.Disconnect();

            _client.MqttMsgPublishReceived -= OnMqttMessageReceived;
            _client = null;
        }

        /// <inheritdoc />
        public void Subscribe(string topic, byte qosLevel = 0)
        {
            if (_client == null || !_client.IsConnected)
                throw new InvalidOperationException("[UnitySenseFramework] Cannot subscribe: not connected to broker.");

            _client.Subscribe(new[] { topic }, new[] { qosLevel });
        }

        /// <inheritdoc />
        public void Unsubscribe(string topic)
        {
            if (_client == null || !_client.IsConnected) return;

            _client.Unsubscribe(new[] { topic });
        }

        /// <inheritdoc />
        public void Publish(string topic, byte[] payload, byte qosLevel = 0, bool retain = false)
        {
            if (_client == null || !_client.IsConnected) return;

            _client.Publish(topic, payload, qosLevel, retain);
        }

        /// <summary>
        /// M2Mqtt 消息回调 → 转发到框架接口事件。
        /// 注意：此回调运行在 MQTT 工作线程，上层需处理线程安全。
        /// </summary>
        private void OnMqttMessageReceived(object sender, MqttMsgPublishEventArgs e)
        {
            OnMessageReceived?.Invoke(e.Topic, e.Message);
        }
    }
}

