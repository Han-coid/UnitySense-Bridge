# UnitySense Framework

**Unity IoT Sensor Bridge Framework** — 通用 Unity 物联网传感器交互框架。

将任何 ESP32、Arduino 或 IoT 设备通过 MQTT 连接到 Unity 项目，实现数字孪生、环境监控、智能家居等应用。

---

## 特性

- ✅ **即插即用**：导入 Package，配置 MQTT 地址，注册事件即可接收数据
- ✅ **多设备支持**：同时管理多个 IoT 设备，自动发现与注册
- ✅ **可扩展数据模型**：使用 `Dictionary<string, float>` 存储传感器数据，新增传感器类型无需改代码
- ✅ **线程安全**：MQTT 后台线程 → 消息队列 → Unity 主线程，自动处理线程切换
- ✅ **事件驱动**：通信层与 UI 层完全解耦
- ✅ **可替换 MQTT 实现**：通过 `IMqttClient` 接口，可替换 M2Mqtt、MQTTnet、WebSocket MQTT 等
- ✅ **不修改第三方库**：M2Mqtt 作为黑盒适配，保持开源规范

---

## 快速开始

### 1. 安装

本项目是一个 Unity Package。推荐将整个仓库目录（包含 `package.json`、`Runtime/`、`M2Mqtt/`）复制到 Unity 项目的 `Packages/com.unitysense.framework/` 下，或使用 Package Manager 的 “Add package from disk” 选择本目录。

### 2. 配置连接

通过 Unity Inspector 或代码配置：

```csharp
using UnitySenseFramework.Device;

var dm = DeviceManager.Instance;
dm.Configure(
    host: "broker.emqx.io",
    port: 1883,
    topic: "UnitySenseBridge"
);
dm.Connect();
```

### 3. 接收传感器数据

```csharp
using UnitySenseFramework.Data;
using UnitySenseFramework.Device;
using UnityEngine;

void Start()
{
    DeviceManager.Instance.OnSensorDataUpdated += OnSensorData;
}

void OnSensorData(SensorMessage msg)
{
    float temp = msg.GetValue("temperature");
    float hum = msg.GetValue("humidity");
    float light = msg.GetValue("light");

    Debug.Log($"Device: {msg.deviceId}, Temp: {temp}°C");
}
```

### 4. 向设备发送指令

```csharp
DeviceManager.Instance.Publish("device/ESP32-001/cmd", "{\"action\":\"restart\"}");
```

---

## 架构

```
┌──────────────────────────────────────────┐
│           业务应用层（你的 Unity 项目）     │
│          UI / 告警 / 业务逻辑              │
├──────────────────────────────────────────┤
│          UnitySense Framework             │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐  │
│  │DeviceMgr │ │EventBus  │ │JsonParser│  │
│  ├──────────┤ ├──────────┤ ├──────────┤  │
│  │IMqttClient│SensorMessage│DeviceInfo│  │
│  └──────────┘ └──────────┘ └──────────┘  │
├──────────────────────────────────────────┤
│  M2MqttAdapter  │  (可替换为其他实现)      │
├──────────────────────────────────────────┤
│   M2Mqtt 源码程序集（随包提供）           │
└──────────────────────────────────────────┘
```

---

## 数据模型

### SensorMessage（传感器消息）

```json
{
  "deviceId": "ESP32-S3-001",
  "timestamp": 1691234567890,
  "values": {
    "temperature": 24.5,
    "humidity": 57.0,
    "light": 300.0
  }
}
```

未来新增传感器类型只需在 `values` 中添加键值对：
```json
{
  "deviceId": "ESP32-S3-002",
  "values": {
    "temperature": 26.0,
    "humidity": 60.0,
    "co2": 800,
    "pm25": 35,
    "motion": 1
  }
}
```

---

## ESP32 端固件

本包不包含 ESP32 固件。设备端只需通过 MQTT 向 `subscribeTopic` 发布受支持的 JSON 数据即可，JSON 格式见 [API_REFERENCE.md](Documentation/API_REFERENCE.md) 中 `SensorMessage` 的说明。

---

## 目录结构

```
com.unitysense.framework/
├── README.md
├── package.json
├── Runtime/
│   ├── UnitySenseFramework.Runtime.asmdef
│   ├── Communication/        MQTT 接口 + 适配器 + 配置
│   ├── Data/                 SensorMessage + DeviceInfo
│   ├── Device/               DeviceManager（核心）
│   ├── Event/                SensorEventBus
│   └── Parser/               JsonSensorParser
├── M2Mqtt/                   M2Mqtt 源码程序集
└── Documentation/
    ├── API_REFERENCE.md
    └── ARCHITECTURE.md
```

---

## API 参考

详细 API 文档见 [API_REFERENCE.md](Documentation/API_REFERENCE.md)

### 核心类

| 类 | 说明 |
|---|---|
| `DeviceManager` | 框架核心，管理连接与设备 |
| `SensorMessage` | 通用传感器消息模型 |
| `DeviceInfo` | 设备元信息 |
| `IMqttClient` | MQTT 客户端接口 |
| `M2MqttAdapter` | M2Mqtt 适配器 |
| `MqttConfig` | MQTT 连接配置 |
| `JsonSensorParser` | JSON 解析器（兼容多种格式） |
| `SensorEventBus` | 线程安全事件总线 |

### 主要事件

| 事件 | 说明 |
|---|---|
| `DeviceManager.OnSensorDataUpdated` | 传感器数据更新（主线程） |
| `DeviceManager.OnDeviceDiscovered` | 新设备被发现 |
| `DeviceManager.OnDeviceOnline` | 设备上线 |
| `DeviceManager.OnDeviceOffline` | 设备离线 |
| `DeviceManager.OnMqttConnected` | MQTT 连接成功 |

---

## 扩展计划

- [ ] MQTTnet 适配器（支持 MQTT 5.0）
- [ ] WebSocket MQTT 适配器（WebGL 支持）
- [ ] ScriptableObject 设备配置预设
- [ ] 传感器数据录制与回放
- [ ] Unity Editor 设备模拟器

## 相关项目

| 项目 | 说明 |
|------|------|
| **[UnitySense-ESP32](https://github.com/Han-coid/UnitySense-ESP32)** | ESP32 端：ESP32 → MQTT → Unity |
| **[UnitySense-Bridge](https://github.com/Han-coid/UnitySense-Bridge)** | Unity 通用物联网交互框架 |
| **[UnitySense-SmartCare](https://github.com/Han-coid/UnitySense-SmartCare)** | 养老院智慧环境管理 Demo |


---

## 许可

本包未在根目录提供 `LICENSE` 文件；随包的 M2Mqtt 第三方库许可见 `M2Mqtt/M2Mqtt_LICENSE.txt`。
