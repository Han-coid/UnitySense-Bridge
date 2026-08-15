using System;
using System.Collections.Generic;

namespace UnitySenseFramework.Data
{
    /// <summary>
    /// 通用传感器消息模型。
    /// 使用 Dictionary&lt;string, float&gt; 存储任意传感器数据，
    /// 新增传感器类型（CO2、PM2.5、陀螺仪等）无需修改本类。
    ///
    /// JSON 示例：
    /// {
    ///   "deviceId": "ESP32-S3-001",
    ///   "timestamp": 1691234567890,
    ///   "values": {
    ///     "temperature": 24.5,
    ///     "humidity": 57.0,
    ///     "light": 300.0
    ///   }
    /// }
    /// </summary>
    [Serializable]
    public class SensorMessage
    {
        /// <summary>设备唯一标识符</summary>
        public string deviceId;

        /// <summary>Unix 时间戳（毫秒），由 ESP32 端填充或 Unity 端到达时间</summary>
        public long timestamp;

        /// <summary>传感器键值对。Key: 传感器类型（如 "temperature"），Value: 传感器值</summary>
        public Dictionary<string, float> values;

        public SensorMessage()
        {
            values = new Dictionary<string, float>();
        }

        /// <summary>
        /// 快捷构造方法
        /// </summary>
        public SensorMessage(string deviceId, Dictionary<string, float> values, long timestamp = 0)
        {
            this.deviceId = deviceId;
            this.values = values ?? new Dictionary<string, float>();
            this.timestamp = timestamp > 0 ? timestamp : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// 安全获取传感器值，不存在时返回默认值
        /// </summary>
        public float GetValue(string sensorKey, float defaultValue = 0f)
        {
            return values.TryGetValue(sensorKey, out float val) ? val : defaultValue;
        }

        /// <summary>
        /// 检查是否包含指定传感器类型的数据
        /// </summary>
        public bool HasSensor(string sensorKey) => values.ContainsKey(sensorKey);

        public override string ToString()
        {
            return $"[SensorMessage] device={deviceId} ts={timestamp} sensors={values.Count}";
        }
    }
}
