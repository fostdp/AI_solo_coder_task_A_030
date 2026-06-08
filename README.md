# 智能建筑中央空调冷站群控与能效优化系统

## 项目概述

本系统是一套完整的中央空调冷站智能监控与能效优化全栈应用，适用于大型商业综合体的冷站设备群控管理。系统通过BACnet/IP协议实时采集37台设备的运行数据，使用机器学习模型进行能效优化，实现设备最优启停组合推荐和节能诊断。

## 系统架构

```
┌─────────────────────────────────────────────────────────────────┐
│                        前端 (React + TypeScript)               │
│  ┌─────────────┐  ┌────────────┐  ┌──────────┐  ┌──────────┐  │
│  │ Canvas流程图 │  │ 设备详情面板 │  │ 能效曲线  │  │ 告警面板  │  │
│  └─────────────┘  └────────────┘  └──────────┘  └──────────┘  │
│                        SignalR 实时推送                          │
└────────────────────────────────┬────────────────────────────────┘
                                 │ HTTP/WebSocket
┌────────────────────────────────┴────────────────────────────────┐
│                      后端 (C# .NET 8 Web API)                   │
│  ┌─────────────┐  ┌────────────┐  ┌──────────┐  ┌──────────┐  │
│  │ BACnet采集  │  │  能效计算   │  │ ML优化模型 │  │ 告警引擎  │  │
│  └─────────────┘  └────────────┘  └──────────┘  └──────────┘  │
│  ┌─────────────┐  ┌────────────┐  ┌──────────┐                │
│  │ 设备管理    │  │  工单系统   │  │ 通知推送  │                │
│  └─────────────┘  └────────────┘  └──────────┘                │
└────────────────────────────────┬────────────────────────────────┘
                                 │
┌────────────────────────────────┴────────────────────────────────┐
│                       SQL Server 数据库                          │
│  时序数据表 | 设备表 | 告警表 | 能效记录表 | 优化推荐表 | 工单表  │
└─────────────────────────────────────────────────────────────────┘
```

## 设备配置

| 设备类型 | 数量 | 设备编号 | 额定功率 | 设计COP |
|---------|------|---------|---------|---------|
| 离心式冷水机组 | 3台 | CEN-CH-001 ~ 003 | 800kW | 5.8 |
| 螺杆式冷水机组 | 2台 | SCR-CH-001 ~ 002 | 500kW | 5.2 |
| 冷却塔 | 8台 | CT-001 ~ 008 | 75kW | 35.0 |
| 冷冻水泵 | 12台 | CHP-001 ~ 012 | 90kW | 20.0 |
| 冷却水泵 | 12台 | CWP-001 ~ 012 | 75kW | 25.0 |

**总计: 37台设备，每30秒上报一次数据**

## 核心功能

### 1. 数据采集与存储
- BACnet/IP协议通信，每30秒采集所有设备数据
- 时序数据存储，支持高性能查询
- 设备运行参数：功率、温度、压力、流量、频率、电流、电压等

### 2. 可视化监控 (Canvas)
- 冷站系统流程图动态展示
- 设备图标根据能效状态变色（绿/黄/红）
- 管道水流方向箭头动画
- 点击设备查看详情和24小时趋势曲线
- 实时指标展示：当日能耗、实时COP、节能量

### 3. 能效优化模型
- 基于Microsoft.ML的FastTree回归算法
- 输入特征：冷冻水温度、冷却水温度、负荷率、设备组合
- 预测不同设备组合下的系统COP
- 每小时生成最优设备启停方案和冷冻水温度设定值
- 节能潜力分析和预期节电效果

### 4. 能效评估与诊断
- 实时COP计算：制冷量/总功率
- 能效比 = 实时COP / 设计COP
- 低于70%自动生成节能诊断报告
- 包含问题分析和改进建议

### 5. 两级告警系统
| 告警级别 | 触发条件 | 持续时间 | 处理方式 |
|---------|---------|---------|---------|
| 一级告警 | 设备参数超限 | 10分钟 | 企业微信推送 + 工单 |
| 二级告警 | 系统COP < 设计值×60% | 30分钟 | 企业微信推送 + 工单 |

### 6. 工单管理
- 告警自动生成工单
- 工单流转：创建→指派→处理→完成→关闭
- 处理记录和解决方案追踪

## 技术栈

### 后端
- **框架**: .NET 8 + ASP.NET Core Web API
- **ORM**: Entity Framework Core 8.0
- **机器学习**: Microsoft.ML 3.0.1 (FastTree回归)
- **实时通信**: SignalR
- **日志**: Serilog
- **数据库**: SQL Server 2019+

### 前端
- **框架**: React 18 + TypeScript
- **构建工具**: Vite 5.0
- **图表**: ECharts 5.5
- **实时通信**: @microsoft/signalr
- **HTTP客户端**: Axios
- **样式**: Tailwind CSS

### 模拟器
- **语言**: Python 3.8+
- **库**: aiohttp, asyncio

## 项目目录结构

```
AI_solo_coder_task_A_030/
├── .trae/documents/
│   ├── PRD.md                     # 产品需求文档
│   └── TechnicalArchitecture.md   # 技术架构文档
├── backend/src/
│   ├── BackgroundServices/        # 后台定时任务
│   ├── Controllers/               # Web API控制器
│   ├── DTOs/                      # 数据传输对象
│   ├── Data/                      # 数据库上下文
│   ├── Hubs/                      # SignalR集线器
│   ├── Models/                    # 数据模型
│   ├── Repositories/              # 数据访问层
│   ├── Services/                  # 业务服务层
│   ├── Program.cs                 # 应用入口
│   ├── appsettings.json           # 配置文件
│   └── ChillerPlantOptimization.API.csproj
├── database/
│   └── InitializeDatabase.sql     # 数据库初始化脚本
├── frontend/
│   ├── src/
│   │   ├── components/            # React组件
│   │   ├── services/              # API和SignalR服务
│   │   ├── types/                 # TypeScript类型定义
│   │   ├── App.tsx                # 主应用组件
│   │   └── main.tsx               # 入口文件
│   ├── index.html
│   ├── package.json
│   ├── tsconfig.json
│   └── vite.config.ts
└── simulator/
    ├── bacnet_simulator.py        # BACnet/IP模拟器
    ├── requirements.txt           # Python依赖
    └── README.md                  # 模拟器说明
```

## 快速开始

### 1. 数据库初始化

```bash
# 在SQL Server中执行初始化脚本
sqlcmd -S localhost -U sa -P YourPassword123! -i database/InitializeDatabase.sql
```

### 2. 后端配置

修改 `backend/src/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ChillerPlantDB;User Id=sa;Password=YourPassword123!;TrustServerCertificate=True;"
  },
  "Notification": {
    "WeChatWebhookUrl": "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=your-webhook-key"
  }
}
```

### 3. 启动后端

```bash
cd backend/src
dotnet restore
dotnet run
```

后端将在 `http://localhost:5000` 启动，Swagger文档: `http://localhost:5000/swagger`

### 4. 启动前端

```bash
cd frontend
npm install
npm run dev
```

前端将在 `http://localhost:3000` 启动

### 5. 启动BACnet模拟器

```bash
cd simulator
pip install -r requirements.txt
python bacnet_simulator.py
```

## API接口列表

### 设备管理
- `GET /api/device` - 获取所有设备
- `GET /api/device/{id}` - 获取设备详情
- `GET /api/device/{id}/realtime` - 获取设备实时数据
- `GET /api/device/{id}/trend` - 获取设备趋势数据
- `POST /api/device/{id}/data` - 上报设备数据
- `PUT /api/device/{id}/status/{status}` - 更新设备状态

### 能效管理
- `GET /api/efficiency/current` - 获取当前系统指标
- `GET /api/efficiency/today` - 获取今日能效数据
- `GET /api/efficiency/history` - 获取历史能效数据
- `GET /api/efficiency/reports` - 获取诊断报告
- `POST /api/efficiency/calculate` - 手动计算COP

### 优化推荐
- `GET /api/optimization/current` - 获取当前优化建议
- `POST /api/optimization/generate` - 生成优化建议
- `POST /api/optimization/apply` - 应用优化方案
- `POST /api/optimization/train` - 训练ML模型

### 告警管理
- `GET /api/alarm/active` - 获取活动告警
- `GET /api/alarm/history` - 获取历史告警
- `PUT /api/alarm/{id}/acknowledge` - 确认告警
- `PUT /api/alarm/{id}/clear` - 清除告警

### 工单管理
- `GET /api/workorder` - 获取工单列表
- `POST /api/workorder` - 创建工单
- `PUT /api/workorder/{id}/assign` - 指派工单
- `PUT /api/workorder/{id}/start` - 开始处理
- `PUT /api/workorder/{id}/complete` - 完成工单

## 核心算法

### COP计算
```
系统COP = 总制冷量 / 总功率
总制冷量 = 流量 × 4.186 × (回水温度 - 供水温度) / 3600
```

### 能效评估
```
能效比 = 实时COP / 设计COP
- 能效比 ≥ 90%: 高效 (绿色)
- 70% ≤ 能效比 < 90%: 效率偏低 (黄色)
- 能效比 < 70%: 低效 (红色)
- 能效比 < 70% 持续: 生成诊断报告
```

### ML优化模型
- **算法**: FastTree回归（决策树集成）
- **输入特征**: 冷冻水供水温度、冷却水进水温度、负荷率、各类型设备数量
- **输出**: 预测COP值
- **优化目标**: 在满足冷量需求的前提下，最大化系统COP

## 数据库表结构

1. **Devices** - 设备基础信息表 (37条初始化数据)
2. **DeviceData** - 设备时序数据表 (每30秒一条)
3. **EfficiencyRecords** - 能效记录表
4. **SystemMetrics** - 系统指标表
5. **Alarms** - 告警表
6. **AlarmThresholds** - 告警阈值配置表
7. **WorkOrders** - 工单表
8. **OptimizationRecommendations** - 优化推荐表
9. **DiagnosisReports** - 诊断报告表
10. **SystemConfigs** - 系统配置表

## 监控指标说明

### 关键指标卡片
1. **当日累计能耗**: 当日所有设备总耗电量 (kWh)
2. **实时COP**: 系统当前制冷系数 (制冷量/功率)
3. **累计节能量**: 与基线相比的节能量 (kWh)

### 设备状态颜色
- 🟢 **绿色**: 高效运行 (能效比 ≥ 90%)
- 🟡 **黄色**: 效率偏低 (70% ≤ 能效比 < 90%)
- 🔴 **红色**: 故障或低效 (能效比 < 70% 或设备故障)
- ⚪ **灰色**: 待机状态

## 开发规范

### 后端
- 采用Repository + Service分层架构
- 异步编程 (async/await)
- 依赖注入
- 全局异常处理
- 结构化日志

### 前端
- TypeScript严格模式
- 组件化设计
- 响应式布局
- 深色工业风格UI

## 系统特性

✅ **实时数据采集** - 37台设备每30秒上报  
✅ **Canvas流程图** - 动态展示设备状态和水流方向  
✅ **ML优化模型** - FastTree决策树预测最优设备组合  
✅ **两级告警** - 参数超限和低能效检测  
✅ **企业微信推送** - 实时告警通知  
✅ **工单系统** - 告警自动转工单，流程闭环  
✅ **能效诊断** - 自动生成节能诊断报告  
✅ **趋势分析** - 24小时参数曲线和能效曲线  
✅ **后台任务** - 定时COP计算、优化推荐、告警检测  

## 许可协议

本项目为演示用途。
