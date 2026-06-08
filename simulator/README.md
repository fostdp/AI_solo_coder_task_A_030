# BACnet/IP 设备模拟器

## 功能概述

模拟智能建筑中央空调冷站的37台BACnet/IP设备，每30秒向服务器上报运行数据。

## 设备配置

| 设备类型 | 数量 | 设备编号前缀 | 额定功率 | 设计COP |
|---------|------|-------------|---------|---------|
| 离心式冷水机组 | 3台 | CEN-CH-001 ~ 003 | 800kW | 5.8 |
| 螺杆式冷水机组 | 2台 | SCR-CH-001 ~ 002 | 500kW | 5.2 |
| 冷却塔 | 8台 | CT-001 ~ 008 | 75kW | 35.0 |
| 冷冻水泵 | 12台 | CHP-001 ~ 012 | 90kW | 20.0 |
| 冷却水泵 | 12台 | CWP-001 ~ 012 | 75kW | 25.0 |

**总计: 37台设备**

## 采集数据点

- **功率 (Power)**: kW
- **供水/出口温度**: °C
- **回水/进口温度**: °C
- **压力 (Pressure)**: MPa
- **流量 (FlowRate)**: m³/h
- **频率 (Frequency)**: Hz
- **电流 (Current)**: A
- **电压 (Voltage)**: V
- **风机转速 (FanSpeed)**: % (仅冷却塔)

## 安装与运行

### 安装依赖

```bash
pip install -r requirements.txt
```

### 运行模拟器

```bash
# 默认配置 (API: http://localhost:5000, 间隔30秒)
python bacnet_simulator.py

# 指定API地址
python bacnet_simulator.py --api-url http://your-server:5000

# 修改采集间隔
python bacnet_simulator.py --interval 15

# 调试模式
python bacnet_simulator.py --log-level DEBUG
```

## 模拟特性

1. **随机负载波动**: 设备负载在60%-100%之间随机波动
2. **设备启停模拟**: 设备随机启动和停机
3. **故障模拟**: 极低概率触发设备故障
4. **低效模拟**: 偶尔触发设备低效运行状态
5. **数据平滑**: 使用惯性滤波模拟真实设备的渐变特性

## 后端API接口

模拟器向以下接口发送数据:

- `POST /api/device/{deviceId}/data` - 上报设备数据
- `PUT /api/device/{deviceId}/status/{status}` - 更新设备状态
- `GET /api/device` - 获取已注册设备列表
- `GET /api/system/health` - 健康检查

## 退出

按 `Ctrl+C` 停止模拟器。
