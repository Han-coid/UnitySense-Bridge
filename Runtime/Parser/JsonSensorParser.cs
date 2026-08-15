using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnitySenseFramework.Data;

namespace UnitySenseFramework.Parser
{
    /// <summary>
    /// JSON 传感器数据解析器。
    /// 负责将 MQTT 接收到的 JSON 字节流解析为 SensorMessage 对象。
    ///
    /// 支持的 JSON 格式（自动适配）：
    ///
    /// 格式 A（ESP32 当前格式 - 扁平结构）：
    /// { "device": "ESP32-001", "sensor": { "temperature": 24, "humidity": 57, "light": 300 } }
    ///
    /// 格式 B（新框架格式 - Dictionary 结构）：
    /// { "deviceId": "ESP32-001", "timestamp": 1691234567890, "values": { "temperature": 24.5, "humidity": 57.0 } }
    ///
    /// 格式 C（简单格式）：
    /// { "deviceId": "ESP32-001", "temperature": 24, "humidity": 57 }
    /// </summary>
    public static class JsonSensorParser
    {
        /// <summary>
        /// 从 JSON 字符串解析 SensorMessage
        /// </summary>
        /// <param name="json">JSON 字符串</param>
        /// <returns>解析后的 SensorMessage，失败返回 null</returns>
        public static SensorMessage Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                JObject root = JObject.Parse(json);

                // ---- 格式 B：新框架格式 ----
                if (root["deviceId"] != null && root["values"] != null)
                {
                    return ParseFormatB(root);
                }

                // ---- 格式 A：ESP32 当前格式 { device, sensor: {...} } ----
                if (root["device"] != null && root["sensor"] is JObject)
                {
                    return ParseFormatA(root);
                }

                // ---- 格式 C：简单扁平格式 { deviceId, temperature, humidity, ... } ----
                if (root["deviceId"] != null)
                {
                    return ParseFormatC(root);
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 从字节数组解析 SensorMessage（UTF-8 编码）
        /// </summary>
        public static SensorMessage Parse(byte[] rawData)
        {
            if (rawData == null || rawData.Length == 0) return null;
            try
            {
                string json = System.Text.Encoding.UTF8.GetString(rawData);
                return Parse(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 格式 A：{ "device": "xxx", "sensor": { "temperature": 24, ... } }
        /// ESP32 当前固件格式
        /// </summary>
        private static SensorMessage ParseFormatA(JObject root)
        {
            string deviceId = root["device"]?.Value<string>();
            JObject sensor = root["sensor"] as JObject;

            var values = new Dictionary<string, float>();
            foreach (var prop in sensor.Properties())
            {
                values[prop.Name] = prop.Value.Value<float>();
            }

            long timestamp = root["timestamp"]?.Value<long>() ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            return new SensorMessage(deviceId, values, timestamp);
        }

        /// <summary>
        /// 格式 B：{ "deviceId": "xxx", "values": { "temperature": 24.5, ... } }
        /// 新框架标准格式
        /// </summary>
        private static SensorMessage ParseFormatB(JObject root)
        {
            string deviceId = root["deviceId"].Value<string>();
            long timestamp = root["timestamp"]?.Value<long>() ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var values = new Dictionary<string, float>();
            JObject vals = root["values"] as JObject;
            if (vals != null)
            {
                foreach (var prop in vals.Properties())
                {
                    values[prop.Name] = prop.Value.Value<float>();
                }
            }

            return new SensorMessage(deviceId, values, timestamp);
        }

        /// <summary>
        /// 格式 C：{ "deviceId": "xxx", "temperature": 24, "humidity": 57, ... }
        /// 简单扁平格式
        /// </summary>
        private static SensorMessage ParseFormatC(JObject root)
        {
            string deviceId = root["deviceId"].Value<string>();
            long timestamp = root["timestamp"]?.Value<long>() ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var values = new Dictionary<string, float>();
            foreach (var prop in root.Properties())
            {
                if (prop.Name == "deviceId" || prop.Name == "timestamp") continue;
                values[prop.Name] = prop.Value.Value<float>();
            }

            return new SensorMessage(deviceId, values, timestamp);
        }

        /// <summary>
        /// 将 SensorMessage 序列化为标准框架 JSON 字符串
        /// </summary>
        public static string ToJson(SensorMessage message)
        {
            return JsonConvert.SerializeObject(message);
        }
    }
}
