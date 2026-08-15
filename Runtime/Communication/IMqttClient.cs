using System;

namespace UnitySenseFramework.Communication
{
    /// <summary>
    /// 通用 MQTT 客户端接口。
    /// 框架层只依赖此接口，具体实现由适配器提供。
    /// 可替换实现：M2MqttAdapter、MqttnetAdapter、WebSocketMqttAdapter 等。
    /// </summary>
    public interface IMqttClient
    {
        /// <summary>是否已连接 Broker</summary>
        bool IsConnected { get; }

        /// <summary>连接到 MQTT Broker</summary>
        /// <param name="config">MQTT 连接配置</param>
        void Connect(MqttConfig config);

        /// <summary>断开连接</summary>
        void Disconnect();

        /// <summary>订阅主题</summary>
        /// <param name="topic">MQTT 主题</param>
        /// <param name="qosLevel">QoS 等级（0, 1, 2）</param>
        void Subscribe(string topic, byte qosLevel = 0);

        /// <summary>取消订阅主题</summary>
        void Unsubscribe(string topic);

        /// <summary>发布消息</summary>
        /// <param name="topic">MQTT 主题</param>
        /// <param name="payload">消息体（字节数组）</param>
        /// <param name="qosLevel">QoS 等级</param>
        /// <param name="retain">是否保留消息</param>
        void Publish(string topic, byte[] payload, byte qosLevel = 0, bool retain = false);

        /// <summary>消息接收事件（运行在 MQTT 工作线程，非 Unity 主线程）</summary>
        event Action<string, byte[]> OnMessageReceived;
    }
}
