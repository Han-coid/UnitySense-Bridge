# UnitySense Framework API Reference

## 命名空间

| 命名空间 | 说明 |
|---|---|
| `UnitySenseFramework.Communication` | MQTT 通信层接口和适配器 |
| `UnitySenseFramework.Data` | 数据模型 |
| `UnitySenseFramework.Device` | 设备管理核心 |
| `UnitySenseFramework.Event` | 事件总线 |
| `UnitySenseFramework.Parser` | JSON 解析器 |

---

## DeviceManager

框架核心单例，负责所有的 MQTT 连接、设备管理和数据分发。

### 获取实例

```csharp
DeviceManager dm = DeviceManager.Instance;
```

### 配置方法

```csharp
// 在 Connect() 之前配置
dm.Configure(
    host: "broker.emqx.io",      // MQTT Broker 地址
    port: 1883,                   // 端口
    topic: "UnitySenseBridge"     // 订阅主题
);
```

### 连接管理

```csharp
dm.Connect();       // 连接 Broker
dm.Disconnect();    // 断开连接
```

### Inspector 属性

| 属性 | 默认值 | 说明 |
|---|---|---|
| `brokerHost` | `"broker.emqx.io"` | MQTT Broker 地址 |
| `brokerPort` | `1883` | MQTT 端口 |
| `autoConnect` | `true` | 启动时自动连接 |
| `subscribeTopic` | `"UnitySenseBridge"` | 订阅的 MQTT 主题 |
| `offlineTimeout` | `10f` | 离线超时（秒） |
| `autoRegisterDevices` | `true` | 自动注册新设备 |

### 替换 MQTT 实现

框架通过静态工厂创建 `IMqttClient`，默认使用 `M2MqttAdapter`：

```csharp
DeviceManager.MqttFactory = () => new MyMqttAdapter();
```

应在 `DeviceManager.Connect()` 之前设置。

### 设备管理

```csharp
// 获取所有设备
IReadOnlyList<DeviceInfo> allDevices = dm.GetAllDevices();

// 获取单个设备
DeviceInfo device = dm.GetDevice("ESP32-S3-001");

// 手动注册设备
dm.RegisterDevice("ESP32-S3-002", "Room 102", "ESP32");

// 移除设备
dm.UnregisterDevice("ESP32-S3-002");

// 设备计数
int total = dm.DeviceCount;
int online = dm.OnlineDeviceCount;
```

### 消息发布

```csharp
// 向 ESP32 发送指令
dm.Publish("device/cmd", "{\"action\":\"restart\"}");
```

### 事件

以下示例默认已引入 `UnitySenseFramework.Data` 和 `UnitySenseFramework.Device` 命名空间。

```csharp
// 传感器数据更新（Unity 主线程，可安全操作 UI）
dm.OnSensorDataUpdated += (SensorMessage msg) => {
    Debug.Log($"Device: {msg.deviceId}");
    float temp = msg.GetValue("temperature");
};

// 新设备被发现
dm.OnDeviceDiscovered += (DeviceInfo device) => {
    Debug.Log($"New device: {device.deviceId}");
};

// 设备上线
dm.OnDeviceOnline += (DeviceInfo device) => { };

// 设备离线
dm.OnDeviceOffline += (DeviceInfo device) => { };

// MQTT 连接成功
dm.OnMqttConnected += () => { };

// MQTT 断线
dm.OnMqttDisconnected += (string reason) => { };
```

---

## SensorMessage

通用传感器消息模型。使用 Dictionary 存储任意传感器数据。

### 构造

```csharp
var values = new Dictionary<string, float> {
    { "temperature", 24.5f },
    { "humidity", 57.0f }
};

var msg = new SensorMessage("ESP32-001", values);
// 或指定时间戳
var msg2 = new SensorMessage("ESP32-001", values, 1691234567890);
```

### 属性

| 属性 | 类型 | 说明 |
|---|---|---|
| `deviceId` | `string` | 设备唯一标识符 |
| `timestamp` | `long` | Unix 时间戳（毫秒） |
| `values` | `Dictionary<string, float>` | 传感器键值对 |

### 方法

```csharp
// 安全获取传感器值（不存在返回默认值）
float temp = msg.GetValue("temperature", defaultValue: 0f);

// 检查传感器类型是否存在
bool hasCO2 = msg.HasSensor("co2");
```

### JSON 格式

框架兼容三种 JSON 格式：

**格式 A**：ESP32 常见格式
```json
{ "device": "ESP32-001", "sensor": { "temperature": 24, "humidity": 57 } }
```

**格式 B**：框架标准格式
```json
{ "deviceId": "ESP32-001", "timestamp": 1691234567890, "values": { "temperature": 24.5 } }
```

**格式 C**：简单扁平格式
```json
{ "deviceId": "ESP32-001", "temperature": 24, "humidity": 57 }
```

---

## DeviceInfo

设备元信息。

```csharp
DeviceInfo device = dm.GetDevice("ESP32-001");

string id = device.deviceId;
string name = device.displayName;
string type = device.deviceType;
bool online = device.isOnline;

// 自定义标签
device.tags["room"] = "101";
device.tags["firmware"] = "v1.2.0";
```

---

## IMqttClient

MQTT 客户端接口。框架代码只依赖此接口，不依赖具体 MQTT 库。

### 实现自定义适配器

```csharp
public class MyMqttAdapter : IMqttClient
{
    public bool IsConnected => ...;
    public event Action<string, byte[]> OnMessageReceived;

    public void Connect(MqttConfig config) { ... }
    public void Disconnect() { ... }
    public void Subscribe(string topic, byte qosLevel = 0) { ... }
    public void Unsubscribe(string topic) { ... }
    public void Publish(string topic, byte[] payload, byte qosLevel = 0, bool retain = false) { ... }
}
```

---

## MqttConfig

MQTT 连接配置。可作为 ScriptableObject 在 Inspector 中配置。

```csharp
// 代码创建
var config = MqttConfig.Create("broker.emqx.io", 1883, "my-client-id");

// ScriptableObject
// 右键 → Create → UnitySense Framework → MQTT Config
```

---

## JsonSensorParser

JSON 解析器（静态工具类）。

```csharp
// 从字符串解析
SensorMessage msg = JsonSensorParser.Parse(jsonString);

// 从字节数组解析
SensorMessage msg = JsonSensorParser.Parse(byteArray);

// 序列化为 JSON
string json = JsonSensorParser.ToJson(message);
```

---

## SensorEventBus

底层线程安全事件总线。通常不需要直接使用，由 `DeviceManager` 内部管理。

```csharp
SensorEventBus bus = SensorEventBus.Instance;

// MQTT 线程安全入队
bus.Enqueue(message);

// 主线程事件
bus.OnSensorDataUpdated += (SensorMessage msg) => { };

// 当前队列待处理数量
int pending = bus.PendingCount;
```
