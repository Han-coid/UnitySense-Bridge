using System;
using UnityEngine;

namespace UnitySenseFramework.Communication
{
    /// <summary>
    /// MQTT 连接配置（ScriptableObject）。
    /// 可在 Unity Inspector 中直接配置，也可运行时通过代码设置。
    /// </summary>
    [CreateAssetMenu(fileName = "MqttConfig", menuName = "UnitySense Framework/MQTT Config")]
    public class MqttConfig : ScriptableObject
    {
        [Header("Broker")]
        [Tooltip("MQTT Broker 地址（IP 或域名）")]
        public string host = "broker.emqx.io";

        [Tooltip("MQTT Broker 端口")]
        public int port = 1883;

        [Tooltip("是否使用加密连接（TLS/SSL）")]
        public bool useEncryption = false;

        [Header("Client")]
        [Tooltip("客户端 ID（留空自动生成）")]
        public string clientId = "";

        [Tooltip("用户名（可选）")]
        public string username = "";

        [Tooltip("密码（可选）")]
        public string password = "";

        [Header("Connection")]
        [Tooltip("连接超时（毫秒）")]
        public int connectionTimeoutMs = 5000;

        [Tooltip("断线自动重连")]
        public bool autoReconnect = true;

        [Tooltip("重连间隔（秒）")]
        public float reconnectInterval = 3f;

        /// <summary>
        /// 创建运行时配置的便捷方法
        /// </summary>
        public static MqttConfig Create(string host, int port = 1883, string clientId = "")
        {
            return new MqttConfig
            {
                host = host,
                port = port,
                clientId = clientId
            };
        }
    }
}
