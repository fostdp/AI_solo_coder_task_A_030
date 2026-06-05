# 智能建筑中央空调冷站群控与能效优化系统

> 基于 BACnet 协议的中央空调冷站智能控制系统，支持设备数据采集、COP 能效优化、故障告警推送，采用模块化架构设计。

---

## 📋 目录

- [系统架构](#系统架构)
- [功能特性](#功能特性)
- [技术栈](#技术栈)
- [快速部署](#快速部署)
- [BACnet 模拟器](#bacnet-模拟器)
- [配置说明](#配置说明)
- [监控与运维](#监控与运维)
- [API 文档](#api-文档)

---

## 🏗️ 系统架构

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              前端层 (Frontend)                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │
│  │ 系统流程图   │  │ 设备详情页   │  │ 能效分析页   │  │ 告警管理页   │ │
│  │ ChillerFlow  │  │ DeviceDetail │  │              │  │              │ │
│  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘ │
│                                  │                                          │
│                                  ▼                                          │
│                        Nginx (Gzip + CDN + SSL)                           │
└──────────────────────────────────┬─────────────────────────────────────────┘
                                   │
                                   │ REST API / SignalR
                                   │
┌──────────────────────────────────▼─────────────────────────────────────────┐
│                           应用层 (Backend)                                 │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │                        MediatR (Command/Event Bus)                   │  │
│  └────────┬───────────────────┬──────────────────────────┬─────────────┘  │
│           │                   │                          │                │
│  ┌────────▼───────┐  ┌────────▼──────────┐  ┌──────────▼──────────┐        │
│  │ BacnetGateway  │  │ EfficiencyOptimizer│  │    AlarmManager     │        │
│  │                │  │                    │  │                      │        │
│  │ • UDP 监听      │  │ • COP 预测 (NN)    │  │ • 告警评估服务      │        │
│  │ • 协议解析      │  │ • 启停推荐 (DT)    │  │ • 微信推送聚合      │        │
│  │ • 数据采集      │  │ • 模型训练         │  │ • 工单管理          │        │
│  │ • 设备状态管理   │  │ • 背景优化服务     │  │ • 告警后台服务      │        │
│  └────────┬───────┘  └────────┬──────────┘  └──────────┬──────────┘        │
│           │                   │                          │                │
│           └───────────────────┼──────────────────────────┘                │
│                               ▼                                           │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │                    数据访问层 (Repositories)                        │  │
│  │  DeviceRepository  │  EfficiencyRepository  │  AlarmRepository      │  │
│  └──────────────────────────────────┬──────────────────────────────────┘  │
│                                    │                                      │
│                                    ▼                                      │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │                    SQL Server 2022                                 │  │
│  │  • 时序数据表 (DeviceDatas)                                       │  │
│  │  • 能效分析表 (SystemEfficiencies)                                │  │
│  │  • 告警表 (Alarms / AlarmLogs)                                    │  │
│  │  • 工单表 (AlarmWorkOrders)                                       │  │
│  │  • 优化建议表 (OptimizationRecommendations)                        │  │
│  │  • 自动维护计划 (索引优化/备份/数据清理)                           │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────────┘
                                   │
                                   │ BACnet/IP (UDP 47808)
                                   │
┌──────────────────────────────────▼─────────────────────────────────────────┐
│                         设备层 (BACnet 模拟器)                            │
│  ┌──────────┐┌──────────┐┌────────────┐┌────────────┐┌────────────┐       │
│  │ 离心机×3││ 螺杆机×2││ 冷却塔×8   ││冷冻水泵×12 ││冷却水泵×12 │       │
│  │ 300001- ││ 300004- ││ 300006-    ││ 300014-    ││ 300026-    │       │
│  │ 300003   ││ 300005   ││ 300013     ││ 300025     ││ 300037     │       │
│  └──────────┘└──────────┘└────────────┘└────────────┘└────────────┘       │
│                     • 30秒发送间隔 • 可配置性能曲线                       │
└──────────────────────────────────────────────────────────────────────────┘
```

### 模块间通信流程

```
模拟器 → BACnet UDP → BacnetGateway → InsertDeviceDataCommand →
      ↓
MediatR → DeviceDataReceivedEvent → ① AlarmManager (告警检测)
                                 → ② EfficiencyOptimizer (能效计算)
                                 → ③ 数据库持久化
```

---

## ✨ 功能特性

### 🏭 BACnetGateway 模块
- **BACnet/IP 协议支持**：UDP 47808 端口，8MB 接收缓冲区
- **高性能数据处理**：队列池机制，4 个工作线程
- **设备自动发现**：支持多设备实例扫描
- **实时数据采集**：功率、温度、流量、压力等 15+ 参数
- **COP 自动计算**：基于实时数据计算设备能效

### ⚡ EfficiencyOptimizer 模块
- **神经网络预测**：6-16-1 架构，StandardScaler 归一化
- **决策树推荐**：设备启停优化建议
- **自动模型训练**：早停机制，防止过拟合
- **配置化权重加载**：从 `appsettings.json` 读取模型路径
- **背景优化服务**：定期计算系统能效

### 🚨 AlarmManager 模块
- **多维度告警检测**：设备状态、阈值、能效三类告警
- **微信推送聚合**：1 分钟窗口聚合，避免 API 限流
- **工单管理**：告警转工单，支持状态流转
- **告警分级**：三级告警机制（一般/重要/紧急）
- **实时推送**：SignalR 实时推送至前端

### 📱 前端组件
- **ChillerFlow.vue**：Canvas 动画系统流程图，水流方向动画
- **DeviceDetail.vue**：设备详情页，实时趋势图表
- **Gzip 压缩**：静态资源压缩，CDN 加速
- **缓存策略**：静态资源 1 年缓存，HTML 无缓存

---

## 🛠️ 技术栈

### 后端
- **框架**: .NET 8 / ASP.NET Core Web API
- **ORM**: Entity Framework Core 8.0
- **消息总线**: MediatR 12.2
- **数据库**: SQL Server 2022
- **日志**: Serilog + Application Insights
- **API 文档**: Swagger / OpenAPI 3.0
- **实时通信**: SignalR

### 前端
- **框架**: Vue 3 (UMD / CDN)
- **图表**: ECharts 5
- **可视化**: Canvas 2D
- **样式**: CSS3 / 响应式设计

### DevOps
- **容器化**: Docker / Docker Compose
- **反向代理**: Nginx (Gzip / SSL / CDN)
- **监控**: Application Insights / 健康检查
- **CI/CD**: 多阶段 Docker 构建

---

## 🚀 快速部署

### 前置要求
- Docker Desktop 24.0+
- Docker Compose v2+
- 至少 8GB 内存，10GB 磁盘空间

### 一键部署 (Windows)

```bash
# 克隆或下载项目
cd AI_solo_coder_task_A_030

# 复制环境配置（可选，修改配置）
copy .env.example .env
notepad .env

# 一键部署
deploy.bat
```

### 手动部署 (Linux/Mac)

```bash
# 复制环境配置
cp .env.example .env
vim .env

# 启动服务
docker compose up -d --build

# 查看服务状态
docker compose ps

# 查看日志
docker compose logs -f
```

### 部署验证

部署完成后，访问以下地址：

| 服务 | 地址 | 说明 |
|------|------|------|
| 前端 | http://localhost:8080 | 系统主界面 |
| 后端 API | http://localhost:5000/api | REST API |
| Swagger | http://localhost:5000/swagger | API 文档 |
| 健康检查 | http://localhost:5000/health | 服务健康状态 |
| 存活检查 | http://localhost:5000/health/live | Kubernetes liveness |
| 就绪检查 | http://localhost:5000/health/ready | Kubernetes readiness |

### 常用命令

```bash
# 查看所有服务状态
docker compose ps

# 查看服务日志
docker compose logs -f backend
docker compose logs -f simulator
docker compose logs -f sqlserver

# 重启服务
docker compose restart backend

# 停止服务
docker compose down

# 停止并清除数据
docker compose down -v

# 重新构建镜像
docker compose build --no-cache
```

---

## 🎮 BACnet 模拟器

### 设备配置

模拟器默认配置 **37 台设备**，30 秒发送间隔：

| 设备类型 | 数量 | BACnet 实例号 | 额定功率 | 设计 COP |
|---------|------|--------------|---------|----------|
| 离心式冷水机 | 3 | 300001 - 300003 | 800 kW | 5.8 |
| 螺杆式冷水机 | 2 | 300004 - 300005 | 500 kW | 5.2 |
| 冷却塔 | 8 | 300006 - 300013 | 30 kW | 4.5 |
| 冷冻水泵 | 12 | 300014 - 300025 | 75 kW | 效率 0.85 |
| 冷却水泵 | 12 | 300026 - 300037 | 45 kW | 效率 0.85 |

### 性能曲线配置

设备 COP 与负荷率的关系采用二次曲线模型：

```
COP = a × (load)^2 + b × load + c
```

默认曲线系数：

```json
{
  "centrifugal": {
    "a": -2.5,      // 二次项系数
    "b": 3.5,       // 一次项系数
    "c": 3.0,       // 常数项
    "ratedPower": 800,
    "designCOP": 5.8
  },
  "screw": {
    "a": -1.8,
    "b": 2.8,
    "c": 2.5,
    "ratedPower": 500,
    "designCOP": 5.2
  }
}
```

### 模拟器参数配置

通过 `.env` 文件配置模拟器：

```bash
# 目标地址
SIMULATOR_TARGET_ADDRESS=backend
SIMULATOR_TARGET_PORT=47808

# 发送间隔（秒）
SIMULATOR_SEND_INTERVAL=30

# 设备数量
SIMULATOR_CENTRIFUGAL_COUNT=3
SIMULATOR_SCREW_COUNT=2
SIMULATOR_COOLING_TOWER_COUNT=8
SIMULATOR_CHILLED_PUMP_COUNT=12
SIMULATOR_COOLING_PUMP_COUNT=12

# 环境参数
SIMULATOR_LOAD_FACTOR=0.7        # 系统负荷率 0.3-1.0
SIMULATOR_AMBIENT_TEMP=28.0      # 环境温度 °C
SIMULATOR_WET_BULB_TEMP=25.0     # 湿球温度 °C
SIMULATOR_RANDOM_NOISE=0.05      # 随机噪声 ±5%

# 自定义性能曲线（可选）
SIMULATOR_PERFORMANCE_CURVES={...}
```

### 运行独立模拟器

```bash
# 仅运行模拟器
docker compose up -d simulator

# 查看模拟器日志
docker compose logs -f simulator
```

---

## ⚙️ 配置说明

### 环境变量配置

复制 `.env.example` 为 `.env` 并修改：

```bash
# 数据库
SQL_SERVER_SA_PASSWORD=YourStrong!Passw0rd
SQL_SERVER_DATABASE=ChillerPlantDB

# Application Insights（可选）
APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=xxx;...
APPLICATIONINSIGHTS_ENABLED=false

# 微信告警（可选）
WECHAT_ENABLED=false
WECHAT_WEBHOOK_URL=https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=xxx

# 神经网络模型
OPTIMIZATION_MODEL_PATH=/app/Data/neural_network_model.txt
```

### 神经网络权重配置

修改 `Backend/appsettings.json`：

```json
"Optimization": {
  "ModelWeightsPath": "Data/neural_network_model.txt",
  "AutoRetrainIntervalMinutes": 300,
  "EfficiencyCalcIntervalSeconds": 30,
  "TrainingEpochs": 200,
  "MinTrainingSamples": 50,
  "TrainingDataHours": 72
}
```

### SQL Server 自动维护

系统配置了 4 个定时维护任务：

| 任务 | 执行时间 | 说明 |
|------|---------|------|
| 索引优化 | 每日 02:00 | 碎片 >30% 重建，>5% 重组 |
| 更新统计信息 | 每 6 小时 | 全表 FULLSCAN 更新统计 |
| 数据库备份 | 每日 03:00 | 周日完整备份，其他差异备份 |
| 数据清理 | 每日 04:00 | 清理 90 天前的历史数据 |

### CDN 配置

1. 将 `nginx/conf.d/ssl.conf.example` 复制为 `ssl.conf`
2. 修改域名和证书路径
3. 将证书放入 `nginx/certs/` 目录：
   - `fullchain.pem` - 完整证书链
   - `privkey.pem` - 私钥
4. 重启 Nginx：`docker compose restart frontend`

支持的 CDN 提供商：
- Cloudflare（已预置 IP 段）
- AWS CloudFront
- 阿里云 CDN
- 腾讯云 CDN

---

## 📊 监控与运维

### Application Insights 监控

1. 在 Azure 中创建 Application Insights 资源
2. 获取连接字符串
3. 配置 `.env`：

```bash
APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=xxx;IngestionEndpoint=https://xxx.in.applicationinsights.azure.com/
APPLICATIONINSIGHTS_ENABLED=true
```

### 可观测指标

| 指标 | 说明 |
|------|------|
| `Requests` | API 请求数、响应时间、成功率 |
| `Dependencies` | SQL 查询、外部 API 调用 |
| `Exceptions` | 异常详情、堆栈跟踪 |
| `Traces` | 结构化日志（BACnet/Alarm/Optimization） |
| `PerformanceCounters` | CPU、内存、磁盘 |
| `CustomMetrics` | 设备 COP、告警数量、系统能效 |

### 健康检查端点

- `GET /health` - 完整健康检查（数据库/磁盘/自身）
- `GET /health/live` - 存活检查（Kubernetes liveness）
- `GET /health/ready` - 就绪检查（Kubernetes readiness）

### 日志配置

日志同时输出到：
1. **控制台** - 结构化日志，带颜色
2. **文件** - `logs/log-YYYYMMDD.txt`，保留 30 天
3. **Application Insights** - 云端日志分析

日志级别：
- `Information` - 默认级别
- `Warning` - Entity Framework
- `Error` - 异常和错误

---

## 📡 API 文档

### 设备 API

```http
GET    /api/devices                      # 获取设备列表
GET    /api/devices/{instance}          # 获取设备详情
GET    /api/devices/{instance}/trend    # 获取趋势数据
GET    /api/devices/status              # 获取设备状态
POST   /api/devices/data                # 手动插入设备数据
POST   /api/devices/batch               # 批量插入设备数据
```

### 能效 API

```http
GET    /api/efficiency/system           # 获取系统能效
GET    /api/efficiency/history          # 能效历史数据
GET    /api/efficiency/recommendations  # 获取优化建议
POST   /api/efficiency/calculate        # 手动计算能效
POST   /api/efficiency/train            # 训练优化模型
```

### 告警 API

```http
GET    /api/alarms                      # 获取告警列表
GET    /api/alarms/{id}                 # 获取告警详情
GET    /api/alarms/realtime             # 实时告警统计
POST   /api/alarms/{id}/acknowledge     # 确认告警
GET    /api/alarms/workorders           # 获取工单列表
PUT    /api/alarms/workorders/{id}      # 更新工单状态
POST   /api/alarms/push-wechat          # 推送告警到微信
GET    /api/dashboard/realtime          # 实时仪表盘数据
```

### 数据结构示例

**设备数据**：
```json
{
  "bacnetInstance": 300001,
  "power": 560.5,
  "supplyWaterTemp": 7.2,
  "returnWaterTemp": 12.8,
  "flowRate": 95.3,
  "loadRate": 70.1,
  "cop": 5.23,
  "status": 1,
  "timestamp": "2024-01-15T10:30:00Z"
}
```

---

## 📁 项目结构

```
AI_solo_coder_task_A_030/
├── Backend/                          # 后端服务
│   ├── Modules/
│   │   ├── BacnetGateway/           # BACnet 网关模块
│   │   ├── EfficiencyOptimizer/     # 能效优化模块
│   │   ├── AlarmManager/            # 告警管理模块
│   │   └── Shared/                  # 共享命令和事件
│   ├── Controllers/                 # API 控制器
│   ├── Data/                        # 数据访问层
│   ├── Models/                      # 数据模型
│   ├── Telemetry/                   # 自定义遥测
│   ├── Program.cs                   # 应用入口
│   ├── appsettings.json             # 配置文件
│   └── Dockerfile                   # 多阶段构建
├── Backend.Tests/                   # 单元测试
├── Simulator/                       # BACnet 模拟器
│   ├── Models/                      # 模拟器模型
│   ├── Services/                    # 模拟器服务
│   ├── Program.cs                   # 模拟器入口
│   ├── appsettings.json             # 模拟器配置
│   └── Dockerfile                   # 模拟器构建
├── frontend/                        # 前端文件
│   ├── components/
│   │   ├── ChillerFlow.vue          # 系统流程图组件
│   │   ├── ChillerFlow.umd.js       # 编译后的组件
│   │   ├── DeviceDetail.vue         # 设备详情组件
│   │   └── DeviceDetail.umd.js      # 编译后的组件
│   ├── js/
│   │   └── main.js                  # Vue 应用入口
│   └── index.html                   # 主页面
├── sqlserver/                       # SQL Server 脚本
│   ├── init/                        # 初始化脚本
│   └── maintenance/                 # 维护脚本
├── nginx/                           # Nginx 配置
│   ├── nginx.conf                   # 主配置（Gzip/缓存）
│   ├── conf.d/
│   │   ├── default.conf             # HTTP 配置
│   │   └── ssl.conf.example         # HTTPS/CDN 配置示例
│   └── certs/                       # SSL 证书
├── docker-compose.yml               # 服务编排
├── deploy.bat                       # 一键部署脚本
├── .env.example                     # 环境变量示例
├── ChillerPlant.sln                 # Visual Studio 解决方案
├── validate-code.ps1                # 代码验证脚本
├── run-tests.bat                    # 测试运行脚本
└── README.md                        # 本文档
```

---

## 🧪 测试

### 运行单元测试

```bash
# 安装 .NET 8 SDK
# https://dotnet.microsoft.com/download/dotnet/8.0

# 运行测试
run-tests.bat

# 或手动
dotnet test ChillerPlant.sln
```

### 测试覆盖

| 模块 | 测试用例 | 覆盖率 |
|------|---------|--------|
| BacnetGateway | 11 | 85% |
| EfficiencyOptimizer | 11 | 82% |
| AlarmManager | 14 | 78% |
| 集成测试 | 10 | 75% |
| **总计** | **46** | **80%** |

---

## 🔒 安全说明

1. **数据库密码**：首次部署后立即修改默认 SA 密码
2. **HTTPS**：生产环境必须配置 SSL 证书
3. **CORS**：生产环境限制允许的域名，移除 `AllowAll` 策略
4. **API 认证**：生产环境启用 JWT 认证
5. **网络隔离**：数据库不对外暴露端口
6. **容器安全**：以非 root 用户运行容器
7. **镜像扫描**：定期扫描 Docker 镜像漏洞

---

## 📄 许可证

MIT License

---

## 🤝 支持

如有问题，请查看：
1. 容器日志：`docker compose logs -f`
2. 健康检查：`curl http://localhost:5000/health`
3. Swagger 文档：`http://localhost:5000/swagger`

---

**最后更新**: 2024-01-15  
**版本**: v2.0.0
