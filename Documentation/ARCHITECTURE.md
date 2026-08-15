# UnitySense Framework Architecture

## 设计理念

UnitySense Framework 遵循以下设计原则：

1. **接口隔离**：框架层只依赖抽象接口，不依赖具体实现
2. **事件驱动**：数据流动通过事件传递，模块间零耦合
3. **线程安全**：后台线程数据通过队列传递到 Unity 主线程
4. **可扩展性**：传感器类型、MQTT 实现、业务规则均可扩展
5. **可复用性**：Framework 目录可独立复制到任何 Unity 项目

---

## 分层架构

```
┌──────────────────────────────────────────────────────┐
│                   Application Layer                   │
│                                                      │
│   ┌─────────────────────────────────────┐            │
│   │        SmartCare Demo               │            │
│   │  SmartCareManager / AlertManager    │            │
│   │  UI (OverviewPanel / ZoneCard / ..) │            │
│   └──────────────┬──────────────────────┘            │
│                  │ 事件订阅                            │
│                  ▼                                    │
├────────────────────────────────┴─────────────────────┤
│                  Framework Layer                       │
│                                                      │
│   ┌──────────────┐  ┌────────────┐  ┌─────────────┐  │
│   │ DeviceManager│──│ SensorMsg  │──│ JsonParser  │  │
│   │ (核心编排)    │  │ (数据模型) │  │ (数据解析)  │  │
│   └──────┬───────┘  └────────────┘  └─────────────┘  │
│          │                                            │
│   ┌──────┴───────┐  ┌────────────┐                   │
│   │  IMqttClient │──│ DeviceInfo │                   │
│   │  (接口抽象)  │  │ (设备模型) │                   │
│   └──────┬───────┘  └────────────┘                   │
│          │                                            │
├──────────┴──────────────────────────────────────────┤
│                 Adapter Layer                         │
│                                                      │
│   ┌──────────────┐  ┌──────────────┐                 │
│   │ M2MqttAdapter│  │ MqttnetAdapter│ (未来)         │
│   └──────┬───────┘  └──────────────┘                 │
│          │                                            │
├──────────┴──────────────────────────────────────────┤
│              Third-Party Library                      │
│                                                      │
│   ┌──────────────┐  ┌──────────────┐                 │
│   │  M2Mqtt.dll  │  │  MQTTnet.dll │ (可选)          │
│   └──────────────┘  └──────────────┘                 │
└──────────────────────────────────────────────────────┘
```

---

## 数据流

### MQTT 接收流程

```
ESP32/Arduino
     │
     │ MQTT Publish
     ▼
┌─────────────┐
│ MQTT Broker │  (broker.emqx.io / 自建)
└──────┬──────┘
       │
       ▼
┌──────────────┐
│ M2MqttAdapter │  ← MQTT 工作线程
│ OnMessageReceived
└──────┬──────┘
       │ byte[]
       ▼
┌──────────────┐
│ JsonSensorParser │  ← 后台线程
│ Parse()
└──────┬──────┘
       │ SensorMessage
       ▼
┌──────────────────────┐
│ ConcurrentQueue       │  ← 线程安全队列
│ _messageQueue        │
└──────┬───────────────┘
       │
       ▼ (Unity Main Thread - Update)
┌──────────────┐
│ DeviceManager │  ← 主线程出队
│ Update()     │
└──────┬───────┘
       │
       ├──► UpdateDeviceData() ──► DeviceInfo 缓存
       │
       └──► OnSensorDataUpdated 事件
                │
                ▼
       ┌───────────────┐
       │ 业务层 / UI    │  ← 订阅者
       │ SmartCareManager
       │ OverviewPanel
       │ ZoneCardItem
       └───────────────┘
```

### 命令下发流程

```
Unity UI/逻辑
     │
     │ dm.Publish("topic", payload)
     ▼
DeviceManager
     │
     │ _mqtt.Publish()
     ▼
IMqttClient (M2MqttAdapter)
     │
     │ MQTT Publish
     ▼
MQTT Broker
     │
     ▼
ESP32 (client.loop() 接收)
```

---

## 核心类职责

### DeviceManager
- **单一职责**：设备管理与 MQTT 连接编排
- **不依赖 UI**：通过事件通知上层
- **线程安全**：内部使用 ConcurrentQueue 桥接 MQTT 线程与 Unity 主线程
- **自动重连**：断线时自动重连

### SensorMessage
- 使用 `Dictionary<string, float>` 替代固定字段
- 新增传感器类型不影响现有代码
- 兼容多种 JSON 格式（通过 JsonSensorParser）

### IMqttClient
- 定义 MQTT 客户端标准接口
- 与具体 MQTT 库解耦
- 支持替换实现（M2Mqtt → MQTTnet → WebSocket MQTT）

### M2MqttAdapter
- 薄封装层，不修改 M2Mqtt 源码
- 将 M2Mqtt 特定事件转换为框架标准接口

---

## 线程模型

```
┌───────────────────────────────────────┐
│          MQTT Worker Thread            │
│                                       │
│  M2MqttAdapter.OnMqttMessageReceived  │
│        │                              │
│        ▼                              │
│  JsonSensorParser.Parse()             │
│        │                              │
│        ▼                              │
│  _messageQueue.Enqueue()  ← lock-free│
└──────────────┬────────────────────────┘
               │ ConcurrentQueue
               ▼
┌───────────────────────────────────────┐
│         Unity Main Thread              │
│                                       │
│  DeviceManager.Update()               │
│        │                              │
│        ▼                              │
│  _messageQueue.TryDequeue()           │
│        │                              │
│        ▼                              │
│  UpdateDeviceData()                   │
│        │                              │
│        ▼                              │
│  OnSensorDataUpdated?.Invoke()        │
│        │                              │
│        ▼                              │
│  Business Layer / UI                  │
│  (Safe to use Unity API here)         │
└───────────────────────────────────────┘
```

---

## 扩展指南

### 新增传感器类型

无需修改任何框架代码。ESP32 固件在 JSON 的 `sensor` 对象中添加新字段即可：

```json
{
  "device": "ESP32-001",
  "sensor": {
    "temperature": 24,
    "humidity": 57,
    "light": 300,
    "co2": 800,
    "pm25": 35
  }
}
```

Unity 端读取：

```csharp
float co2 = msg.GetValue("co2");
float pm25 = msg.GetValue("pm25");
```

### 替换 MQTT 实现

1. 实现 `IMqttClient` 接口
2. 修改 `DeviceManager.TryConnectInternal()` 中 `new M2MqttAdapter()` 为你的实现

### 添加新的 Demo/Application

1. 在 `Examples/` 下创建新目录
2. 引用 `UnitySenseFramework.Runtime` 程序集
3. 订阅 `DeviceManager.Instance.OnSensorDataUpdated`

---

## 文件清单

```
UnitySenseFramework/
├── README.md                         框架说明
├── API_REFERENCE.md                  API 文档
├── ARCHITECTURE.md                   架构文档（本文档）
├── package.json                      Package 清单
├── Runtime/
│   ├── UnitySenseFramework.Runtime.asmdef
│   ├── Communication/
│   │   ├── IMqttClient.cs           MQTT 客户端接口
│   │   ├── M2MqttAdapter.cs         M2Mqtt 适配器
│   │   └── MqttConfig.cs            MQTT 配置
│   ├── Data/
│   │   ├── SensorMessage.cs         传感器消息模型
│   │   └── DeviceInfo.cs            设备元信息
│   ├── Device/
│   │   └── DeviceManager.cs         设备管理核心
│   ├── Event/
│   │   └── SensorEventBus.cs        事件总线
│   └── Parser/
│       └── JsonSensorParser.cs       JSON 解析器
└── Plugins/
    └── M2Mqtt/                       M2Mqtt 第三方库
```
