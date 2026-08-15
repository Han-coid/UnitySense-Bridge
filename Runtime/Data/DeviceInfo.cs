using System;
using System.Collections.Generic;

namespace UnitySenseFramework.Data
{
    /// <summary>
    /// 设备元信息。
    /// 描述一个 IoT 设备的基本属性，不包含传感器数值。
    /// </summary>
    [Serializable]
    public class DeviceInfo
    {
        /// <summary>设备唯一标识符</summary>
        public string deviceId;

        /// <summary>设备显示名称（如 "Room 101 Sensor Node"）</summary>
        public string displayName;

        /// <summary>设备类型（如 "ESP32", "Arduino", "RaspberryPi"）</summary>
        public string deviceType;

        /// <summary>设备所属区域/分组（可选）</summary>
        public string zone;

        /// <summary>设备固件版本</summary>
        public string firmwareVersion;

        /// <summary>设备是否在线</summary>
        public bool isOnline;

        /// <summary>最后收到数据的时间（Unix 毫秒）</summary>
        public long lastDataTimestamp;

        /// <summary>最后一次数据到达时的 Time.realtimeSinceStartup（Unity 主线程维护）</summary>
        [NonSerialized] public float lastUnityTime;

        /// <summary>自定义标签/元数据扩展</summary>
        public Dictionary<string, string> tags;

        public DeviceInfo()
        {
            tags = new Dictionary<string, string>();
        }

        public DeviceInfo(string deviceId, string displayName = "", string deviceType = "")
        {
            this.deviceId = deviceId;
            this.displayName = string.IsNullOrEmpty(displayName) ? deviceId : displayName;
            this.deviceType = deviceType;
            tags = new Dictionary<string, string>();
        }

        public override string ToString()
        {
            return $"[Device] {deviceId} ({displayName}) type={deviceType} online={isOnline}";
        }
    }
}
