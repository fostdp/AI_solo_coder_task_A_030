-- =============================================
-- 智能建筑中央空调冷站群控与能效优化系统
-- 数据库初始化脚本
-- =============================================

USE master
GO

IF EXISTS (SELECT name FROM sys.databases WHERE name = N'ChillerPlantDB')
BEGIN
    ALTER DATABASE ChillerPlantDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE
    DROP DATABASE ChillerPlantDB
END
GO

CREATE DATABASE ChillerPlantDB
GO

USE ChillerPlantDB
GO

-- =============================================
-- 设备类型表
-- =============================================
CREATE TABLE DeviceTypes (
    DeviceTypeId INT PRIMARY KEY IDENTITY(1,1),
    TypeName NVARCHAR(50) NOT NULL,
    Description NVARCHAR(200),
    DesignCOP DECIMAL(5,2) NOT NULL,
    PowerRating DECIMAL(10,2) NOT NULL,
    CreatedAt DATETIME DEFAULT GETDATE()
)
GO

-- =============================================
-- 设备表
-- =============================================
CREATE TABLE Devices (
    DeviceId INT PRIMARY KEY IDENTITY(1,1),
    DeviceTypeId INT NOT NULL FOREIGN KEY REFERENCES DeviceTypes(DeviceTypeId),
    DeviceName NVARCHAR(100) NOT NULL,
    DeviceCode NVARCHAR(50) NOT NULL UNIQUE,
    BacnetInstance INT NOT NULL UNIQUE,
    IpAddress NVARCHAR(50),
    Location NVARCHAR(200),
    InstallDate DATE,
    Status INT NOT NULL DEFAULT 1, -- 1:运行 0:停机 -1:故障
    DesignCOP DECIMAL(5,2) NOT NULL,
    RatedPower DECIMAL(10,2) NOT NULL,
    RatedCapacity DECIMAL(10,2) NOT NULL,
    X INT NOT NULL DEFAULT 0,
    Y INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME DEFAULT GETDATE(),
    UpdatedAt DATETIME DEFAULT GETDATE()
)
GO

-- =============================================
-- 设备实时数据表
-- =============================================
CREATE TABLE DeviceData (
    DataId BIGINT PRIMARY KEY IDENTITY(1,1),
    DeviceId INT NOT NULL FOREIGN KEY REFERENCES Devices(DeviceId),
    Timestamp DATETIME NOT NULL DEFAULT GETDATE(),
    Power DECIMAL(10,2) NOT NULL, -- kW
    SupplyWaterTemp DECIMAL(5,2), -- 冷冻水出水温度
    ReturnWaterTemp DECIMAL(5,2), -- 冷冻水回水温度
    CoolingWaterInTemp DECIMAL(5,2), -- 冷却水进水温度
    CoolingWaterOutTemp DECIMAL(5,2), -- 冷却水出水温度
    FlowRate DECIMAL(10,2), -- m³/h
    SupplyPressure DECIMAL(5,2), -- 供水压力 MPa
    ReturnPressure DECIMAL(5,2), -- 回水压力 MPa
    LoadRate DECIMAL(5,2), -- 负荷率 %
    Frequency DECIMAL(5,2), -- 频率 Hz
    Vibration DECIMAL(5,2), -- 振动 mm/s
    Current DECIMAL(8,2), -- 电流 A
    Voltage DECIMAL(8,2), -- 电压 V
    RunningHours BIGINT DEFAULT 0, -- 运行小时数
    Status INT NOT NULL DEFAULT 1,
    COP DECIMAL(5,2), -- 实时COP
    CreatedAt DATETIME DEFAULT GETDATE()
)
GO

CREATE NONCLUSTERED INDEX IX_DeviceData_DeviceId_Timestamp 
ON DeviceData(DeviceId, Timestamp DESC)
GO

CREATE NONCLUSTERED INDEX IX_DeviceData_Timestamp 
ON DeviceData(Timestamp DESC)
GO

-- =============================================
-- 系统能效表
-- =============================================
CREATE TABLE SystemEfficiency (
    EfficiencyId BIGINT PRIMARY KEY IDENTITY(1,1),
    Timestamp DATETIME NOT NULL DEFAULT GETDATE(),
    TotalCoolingCapacity DECIMAL(12,2), -- 总制冷量 kW
    TotalPowerConsumption DECIMAL(12,2), -- 总功耗 kW
    SystemCOP DECIMAL(5,2) NOT NULL, -- 系统COP
    DesignCOP DECIMAL(5,2) NOT NULL, -- 设计COP
    COPRatio DECIMAL(5,2), -- COP比值
    ChillerPower DECIMAL(12,2), -- 冷水机组功耗
    PumpPower DECIMAL(12,2), -- 水泵功耗
    TowerPower DECIMAL(12,2), -- 冷却塔功耗
    OutdoorTemp DECIMAL(5,2), -- 室外温度
    WetBulbTemp DECIMAL(5,2), -- 湿球温度
    TotalFlowRate DECIMAL(10,2), -- 总流量
    CreatedAt DATETIME DEFAULT GETDATE()
)
GO

CREATE NONCLUSTERED INDEX IX_SystemEfficiency_Timestamp 
ON SystemEfficiency(Timestamp DESC)
GO

-- =============================================
-- 能耗统计表
-- =============================================
CREATE TABLE EnergyConsumption (
    ConsumptionId BIGINT PRIMARY KEY IDENTITY(1,1),
    DeviceId INT FOREIGN KEY REFERENCES Devices(DeviceId),
    Date DATE NOT NULL,
    Hour INT,
    EnergyConsumed DECIMAL(12,2) NOT NULL, -- kWh
    CoolingCapacity DECIMAL(12,2), -- 制冷量 kWh
    AvgCOP DECIMAL(5,2),
    PeakPower DECIMAL(10,2),
    Runtime INT, -- 分钟
    CreatedAt DATETIME DEFAULT GETDATE()
)
GO

CREATE UNIQUE NONCLUSTERED INDEX IX_EnergyConsumption_DeviceDateHour 
ON EnergyConsumption(DeviceId, Date, Hour) WHERE Hour IS NOT NULL
GO

CREATE UNIQUE NONCLUSTERED INDEX IX_EnergyConsumption_DeviceDate 
ON EnergyConsumption(DeviceId, Date) WHERE Hour IS NULL
GO

-- =============================================
-- 告警表
-- =============================================
CREATE TABLE Alarms (
    AlarmId BIGINT PRIMARY KEY IDENTITY(1,1),
    AlarmCode NVARCHAR(50) NOT NULL,
    AlarmLevel INT NOT NULL, -- 1:一级告警 2:二级告警
    DeviceId INT FOREIGN KEY REFERENCES Devices(DeviceId),
    AlarmType NVARCHAR(50) NOT NULL, -- 参数超限/COP过低/通信故障等
    AlarmMessage NVARCHAR(500) NOT NULL,
    ParameterName NVARCHAR(50),
    ActualValue DECIMAL(12,4),
    ThresholdValue DECIMAL(12,4),
    StartTime DATETIME NOT NULL,
    EndTime DATETIME,
    Duration INT, -- 持续分钟数
    AckStatus INT DEFAULT 0, -- 0:未确认 1:已确认
    AckBy NVARCHAR(50),
    AckTime DATETIME,
    Status INT DEFAULT 1, -- 1:激活 0:已清除
    WechatPushStatus INT DEFAULT 0, -- 0:未推送 1:已推送 2:推送失败
    WorkOrderId BIGINT,
    CreatedAt DATETIME DEFAULT GETDATE()
)
GO

CREATE NONCLUSTERED INDEX IX_Alarms_Status_Level 
ON Alarms(Status DESC, AlarmLevel, StartTime DESC)
GO

-- =============================================
-- 工单表
-- =============================================
CREATE TABLE WorkOrders (
    WorkOrderId BIGINT PRIMARY KEY IDENTITY(1,1),
    OrderNo NVARCHAR(50) NOT NULL UNIQUE,
    AlarmId BIGINT FOREIGN KEY REFERENCES Alarms(AlarmId),
    DeviceId INT FOREIGN KEY REFERENCES Devices(DeviceId),
    OrderType NVARCHAR(50) NOT NULL, -- 告警处理/例行维护/能效优化
    Priority INT NOT NULL, -- 1:紧急 2:高 3:中 4:低
    Title NVARCHAR(200) NOT NULL,
    Description NVARCHAR(1000),
    Status INT NOT NULL DEFAULT 0, -- 0:待处理 1:处理中 2:已完成 3:已关闭
    Assignee NVARCHAR(50),
    EstimateTime DECIMAL(5,1), -- 预计工时
    ActualTime DECIMAL(5,1),
    StartTime DATETIME,
    CompleteTime DATETIME,
    CloseTime DATETIME,
    Remark NVARCHAR(1000),
    CreatedBy NVARCHAR(50),
    CreatedAt DATETIME DEFAULT GETDATE(),
    UpdatedAt DATETIME DEFAULT GETDATE()
)
GO

-- =============================================
-- 优化建议表
-- =============================================
CREATE TABLE OptimizationRecommendations (
    RecommendationId BIGINT PRIMARY KEY IDENTITY(1,1),
    RecommendationTime DATETIME NOT NULL,
    CurrentLoadRate DECIMAL(5,2) NOT NULL,
    OutdoorTemp DECIMAL(5,2),
    WetBulbTemp DECIMAL(5,2),
    RecommendedChillerCombination NVARCHAR(500), -- 推荐的设备组合
    RecommendedSupplyWaterTemp DECIMAL(5,2), -- 推荐冷冻水温度
    PredictedCOP DECIMAL(5,2), -- 预测COP
    CurrentCOP DECIMAL(5,2), -- 当前COP
    ExpectedEnergySaving DECIMAL(10,2), -- 预期节能量 kWh/h
    ExpectedEnergySavingPercent DECIMAL(5,2), -- 预期节能率
    OptimizationStrategy NVARCHAR(500), -- 优化策略说明
    IsImplemented BIT DEFAULT 0,
    ImplementedAt DATETIME,
    ActualCOPAfterImpl DECIMAL(5,2),
    ActualEnergySaving DECIMAL(10,2),
    CreatedAt DATETIME DEFAULT GETDATE()
)
GO

CREATE NONCLUSTERED INDEX IX_OptimizationRecommendations_Time 
ON OptimizationRecommendations(RecommendationTime DESC)
GO

-- =============================================
-- 节能诊断报告表
-- =============================================
CREATE TABLE EnergyDiagnosisReports (
    ReportId BIGINT PRIMARY KEY IDENTITY(1,1),
    ReportDate DATE NOT NULL,
    SystemAvgCOP DECIMAL(5,2),
    DesignCOP DECIMAL(5,2),
    COPRatio DECIMAL(5,2),
    TotalEnergyConsumption DECIMAL(12,2),
    BenchmarkEnergyConsumption DECIMAL(12,2),
    EnergySavingPotential DECIMAL(12,2),
    DiagnosisFindings NVARCHAR(2000), -- 诊断发现
    Recommendations NVARCHAR(2000), -- 改进建议
    LowEfficiencyDevices NVARCHAR(500), -- 低效设备列表
    GeneratedAt DATETIME DEFAULT GETDATE()
)
GO

-- =============================================
-- 管道连接表
-- =============================================
CREATE TABLE PipeConnections (
    ConnectionId INT PRIMARY KEY IDENTITY(1,1),
    FromDeviceId INT NOT NULL FOREIGN KEY REFERENCES Devices(DeviceId),
    ToDeviceId INT NOT NULL FOREIGN KEY REFERENCES Devices(DeviceId),
    PipeType NVARCHAR(50) NOT NULL, -- 冷冻水供/回 冷却水供/回
    Color NVARCHAR(20) DEFAULT '#3498db',
    FlowDirection INT NOT NULL, -- 1:正向 -1:反向 0:静止
    CreatedAt DATETIME DEFAULT GETDATE()
)
GO

-- =============================================
-- 模型训练数据表
-- =============================================
CREATE TABLE ModelTrainingData (
    TrainingId BIGINT PRIMARY KEY IDENTITY(1,1),
    Timestamp DATETIME NOT NULL,
    OutdoorTemp DECIMAL(5,2),
    WetBulbTemp DECIMAL(5,2),
    ChillerCombination NVARCHAR(200),
    ChillerCount INT,
    SupplyWaterTemp DECIMAL(5,2),
    CoolingWaterInTemp DECIMAL(5,2),
    LoadRate DECIMAL(5,2),
    TotalPower DECIMAL(12,2),
    TotalCooling DECIMAL(12,2),
    ActualCOP DECIMAL(5,2),
    IsUsedForTraining BIT DEFAULT 0,
    CreatedAt DATETIME DEFAULT GETDATE()
)
GO

-- =============================================
-- 初始化设备类型
-- =============================================
INSERT INTO DeviceTypes (TypeName, Description, DesignCOP, PowerRating) VALUES
(N'离心式冷水机组', N'大型离心式冷水机组', 6.5, 800),
(N'螺杆式冷水机组', N'螺杆式冷水机组', 5.8, 450),
(N'冷却塔', N'逆流式冷却塔', 35.0, 15),
(N'冷冻水泵', N'变频冷冻水泵', 35.0, 90),
(N'冷却水泵', N'变频冷却水泵', 35.0, 75)
GO

-- =============================================
-- 初始化设备数据
-- =============================================
-- 3台离心式冷水机组
INSERT INTO Devices (DeviceTypeId, DeviceName, DeviceCode, BacnetInstance, DesignCOP, RatedPower, RatedCapacity, X, Y) VALUES
(1, N'1#离心冷水机组', 'CH-001', 10001, 6.5, 800, 5200, 150, 200),
(1, N'2#离心冷水机组', 'CH-002', 10002, 6.5, 800, 5200, 300, 200),
(1, N'3#离心冷水机组', 'CH-003', 10003, 6.5, 800, 5200, 450, 200)
GO

-- 2台螺杆式冷水机组
INSERT INTO Devices (DeviceTypeId, DeviceName, DeviceCode, BacnetInstance, DesignCOP, RatedPower, RatedCapacity, X, Y) VALUES
(2, N'4#螺杆冷水机组', 'CH-004', 10004, 5.8, 450, 2610, 600, 200),
(2, N'5#螺杆冷水机组', 'CH-005', 10005, 5.8, 450, 2610, 750, 200)
GO

-- 8台冷却塔
INSERT INTO Devices (DeviceTypeId, DeviceName, DeviceCode, BacnetInstance, DesignCOP, RatedPower, RatedCapacity, X, Y) VALUES
(3, N'1#冷却塔', 'CT-001', 20001, 35.0, 15, 525, 100, 400),
(3, N'2#冷却塔', 'CT-002', 20002, 35.0, 15, 525, 200, 400),
(3, N'3#冷却塔', 'CT-003', 20003, 35.0, 15, 525, 300, 400),
(3, N'4#冷却塔', 'CT-004', 20004, 35.0, 15, 525, 400, 400),
(3, N'5#冷却塔', 'CT-005', 20005, 35.0, 15, 525, 500, 400),
(3, N'6#冷却塔', 'CT-006', 20006, 35.0, 15, 525, 600, 400),
(3, N'7#冷却塔', 'CT-007', 20007, 35.0, 15, 525, 700, 400),
(3, N'8#冷却塔', 'CT-008', 20008, 35.0, 15, 525, 800, 400)
GO

-- 12台冷冻水泵
INSERT INTO Devices (DeviceTypeId, DeviceName, DeviceCode, BacnetInstance, DesignCOP, RatedPower, RatedCapacity, X, Y) VALUES
(4, N'1#冷冻水泵', 'CHWP-001', 30001, 35.0, 90, 3150, 100, 100),
(4, N'2#冷冻水泵', 'CHWP-002', 30002, 35.0, 90, 3150, 200, 100),
(4, N'3#冷冻水泵', 'CHWP-003', 30003, 35.0, 90, 3150, 300, 100),
(4, N'4#冷冻水泵', 'CHWP-004', 30004, 35.0, 90, 3150, 400, 100),
(4, N'5#冷冻水泵', 'CHWP-005', 30005, 35.0, 90, 3150, 500, 100),
(4, N'6#冷冻水泵', 'CHWP-006', 30006, 35.0, 90, 3150, 600, 100),
(4, N'7#冷冻水泵', 'CHWP-007', 30007, 35.0, 90, 3150, 150, 50),
(4, N'8#冷冻水泵', 'CHWP-008', 30008, 35.0, 90, 3150, 250, 50),
(4, N'9#冷冻水泵', 'CHWP-009', 30009, 35.0, 90, 3150, 350, 50),
(4, N'10#冷冻水泵', 'CHWP-010', 30010, 35.0, 90, 3150, 450, 50),
(4, N'11#冷冻水泵', 'CHWP-011', 30011, 35.0, 90, 3150, 550, 50),
(4, N'12#冷冻水泵', 'CHWP-012', 30012, 35.0, 90, 3150, 650, 50)
GO

-- 12台冷却水泵
INSERT INTO Devices (DeviceTypeId, DeviceName, DeviceCode, BacnetInstance, DesignCOP, RatedPower, RatedCapacity, X, Y) VALUES
(5, N'1#冷却水泵', 'CWP-001', 40001, 35.0, 75, 2625, 100, 300),
(5, N'2#冷却水泵', 'CWP-002', 40002, 35.0, 75, 2625, 200, 300),
(5, N'3#冷却水泵', 'CWP-003', 40003, 35.0, 75, 2625, 300, 300),
(5, N'4#冷却水泵', 'CWP-004', 40004, 35.0, 75, 2625, 400, 300),
(5, N'5#冷却水泵', 'CWP-005', 40005, 35.0, 75, 2625, 500, 300),
(5, N'6#冷却水泵', 'CWP-006', 40006, 35.0, 75, 2625, 600, 300),
(5, N'7#冷却水泵', 'CWP-007', 40007, 35.0, 75, 2625, 150, 350),
(5, N'8#冷却水泵', 'CWP-008', 40008, 35.0, 75, 2625, 250, 350),
(5, N'9#冷却水泵', 'CWP-009', 40009, 35.0, 75, 2625, 350, 350),
(5, N'10#冷却水泵', 'CWP-010', 40010, 35.0, 75, 2625, 450, 350),
(5, N'11#冷却水泵', 'CWP-011', 40011, 35.0, 75, 2625, 550, 350),
(5, N'12#冷却水泵', 'CWP-012', 40012, 35.0, 75, 2625, 650, 350)
GO

-- =============================================
-- 初始化管道连接
-- =============================================
-- 冷冻水系统连接（简化）
INSERT INTO PipeConnections (FromDeviceId, ToDeviceId, PipeType, Color, FlowDirection) VALUES
-- 冷冻水泵 -> 冷水机组
(6, 1, N'冷冻水供水', '#3498db', 1),
(7, 2, N'冷冻水供水', '#3498db', 1),
(8, 3, N'冷冻水供水', '#3498db', 1),
(9, 4, N'冷冻水供水', '#3498db', 1),
(10, 5, N'冷冻水供水', '#3498db', 1),
-- 冷水机组 -> 末端（用虚拟连接表示）
(1, 11, N'冷冻水回水', '#e74c3c', -1),
(2, 12, N'冷冻水回水', '#e74c3c', -1),
(3, 13, N'冷冻水回水', '#e74c3c', -1),
(4, 14, N'冷冻水回水', '#e74c3c', -1),
(5, 15, N'冷冻水回水', '#e74c3c', -1),
-- 冷却水系统连接
(16, 1, N'冷却水供水', '#2ecc71', 1),
(17, 2, N'冷却水供水', '#2ecc71', 1),
(18, 3, N'冷却水供水', '#2ecc71', 1),
(19, 4, N'冷却水供水', '#2ecc71', 1),
(20, 5, N'冷却水供水', '#2ecc71', 1),
-- 冷水机组 -> 冷却塔
(1, 21, N'冷却水回水', '#f39c12', -1),
(2, 22, N'冷却水回水', '#f39c12', -1),
(3, 23, N'冷却水回水', '#f39c12', -1),
(4, 24, N'冷却水回水', '#f39c12', -1),
(5, 25, N'冷却水回水', '#f39c12', -1),
(1, 26, N'冷却水回水', '#f39c12', -1),
(2, 27, N'冷却水回水', '#f39c12', -1),
(3, 28, N'冷却水回水', '#f39c12', -1)
GO

-- =============================================
-- 存储过程：获取设备近24小时数据
-- =============================================
CREATE PROCEDURE sp_GetDevice24HourData
    @DeviceId INT
AS
BEGIN
    SELECT 
        Timestamp,
        Power,
        SupplyWaterTemp,
        ReturnWaterTemp,
        CoolingWaterInTemp,
        CoolingWaterOutTemp,
        FlowRate,
        LoadRate,
        COP,
        Status
    FROM DeviceData
    WHERE DeviceId = @DeviceId 
        AND Timestamp >= DATEADD(HOUR, -24, GETDATE())
    ORDER BY Timestamp ASC
END
GO

-- =============================================
-- 存储过程：获取当日能耗统计
-- =============================================
CREATE PROCEDURE sp_GetDailyEnergyConsumption
    @Date DATE = NULL
AS
BEGIN
    SET @Date = ISNULL(@Date, CAST(GETDATE() AS DATE))
    
    SELECT 
        SUM(EnergyConsumed) AS TotalEnergy,
        SUM(CASE WHEN d.DeviceTypeId = 1 OR d.DeviceTypeId = 2 THEN EnergyConsumed ELSE 0 END) AS ChillerEnergy,
        SUM(CASE WHEN d.DeviceTypeId = 4 THEN EnergyConsumed ELSE 0 END) AS ChillerPumpEnergy,
        SUM(CASE WHEN d.DeviceTypeId = 5 THEN EnergyConsumed ELSE 0 END) AS CoolingPumpEnergy,
        SUM(CASE WHEN d.DeviceTypeId = 3 THEN EnergyConsumed ELSE 0 END) AS TowerEnergy
    FROM EnergyConsumption ec
    INNER JOIN Devices d ON ec.DeviceId = d.DeviceId
    WHERE ec.Date = @Date AND ec.Hour IS NULL
END
GO

-- =============================================
-- 存储过程：获取实时能效数据
-- =============================================
CREATE PROCEDURE sp_GetRealtimeEfficiency
AS
BEGIN
    SELECT TOP 1
        Timestamp,
        TotalCoolingCapacity,
        TotalPowerConsumption,
        SystemCOP,
        DesignCOP,
        COPRatio,
        ChillerPower,
        PumpPower,
        TowerPower,
        OutdoorTemp,
        WetBulbTemp
    FROM SystemEfficiency
    ORDER BY Timestamp DESC
END
GO

-- =============================================
-- 存储过程：获取活跃告警
-- =============================================
CREATE PROCEDURE sp_GetActiveAlarms
AS
BEGIN
    SELECT 
        a.AlarmId,
        a.AlarmCode,
        a.AlarmLevel,
        a.DeviceId,
        d.DeviceName,
        d.DeviceCode,
        a.AlarmType,
        a.AlarmMessage,
        a.ActualValue,
        a.ThresholdValue,
        a.StartTime,
        a.Duration,
        a.Status,
        a.AckStatus
    FROM Alarms a
    INNER JOIN Devices d ON a.DeviceId = d.DeviceId
    WHERE a.Status = 1
    ORDER BY a.AlarmLevel, a.StartTime DESC
END
GO

-- =============================================
-- 存储过程：获取最新优化建议
-- =============================================
CREATE PROCEDURE sp_GetLatestRecommendation
AS
BEGIN
    SELECT TOP 1
        RecommendationId,
        RecommendationTime,
        CurrentLoadRate,
        OutdoorTemp,
        WetBulbTemp,
        RecommendedChillerCombination,
        RecommendedSupplyWaterTemp,
        PredictedCOP,
        CurrentCOP,
        ExpectedEnergySaving,
        ExpectedEnergySavingPercent,
        OptimizationStrategy,
        IsImplemented
    FROM OptimizationRecommendations
    ORDER BY RecommendationTime DESC
END
GO

-- =============================================
-- 存储过程：插入设备数据
-- =============================================
CREATE PROCEDURE sp_InsertDeviceData
    @DeviceId INT,
    @Power DECIMAL(10,2),
    @SupplyWaterTemp DECIMAL(5,2) = NULL,
    @ReturnWaterTemp DECIMAL(5,2) = NULL,
    @CoolingWaterInTemp DECIMAL(5,2) = NULL,
    @CoolingWaterOutTemp DECIMAL(5,2) = NULL,
    @FlowRate DECIMAL(10,2) = NULL,
    @SupplyPressure DECIMAL(5,2) = NULL,
    @ReturnPressure DECIMAL(5,2) = NULL,
    @LoadRate DECIMAL(5,2) = NULL,
    @Frequency DECIMAL(5,2) = NULL,
    @Vibration DECIMAL(5,2) = NULL,
    @Current DECIMAL(8,2) = NULL,
    @Voltage DECIMAL(8,2) = NULL,
    @RunningHours BIGINT = 0,
    @Status INT = 1,
    @COP DECIMAL(5,2) = NULL
AS
BEGIN
    INSERT INTO DeviceData (
        DeviceId, Power, SupplyWaterTemp, ReturnWaterTemp, 
        CoolingWaterInTemp, CoolingWaterOutTemp, FlowRate,
        SupplyPressure, ReturnPressure, LoadRate, Frequency,
        Vibration, Current, Voltage, RunningHours, Status, COP
    ) VALUES (
        @DeviceId, @Power, @SupplyWaterTemp, @ReturnWaterTemp,
        @CoolingWaterInTemp, @CoolingWaterOutTemp, @FlowRate,
        @SupplyPressure, @ReturnPressure, @LoadRate, @Frequency,
        @Vibration, @Current, @Voltage, @RunningHours, @Status, @COP
    )
    
    UPDATE Devices 
    SET Status = @Status, UpdatedAt = GETDATE()
    WHERE DeviceId = @DeviceId
END
GO

-- =============================================
-- 存储过程：生成节能诊断报告
-- =============================================
CREATE PROCEDURE sp_GenerateEnergyDiagnosisReport
    @ReportDate DATE = NULL
AS
BEGIN
    SET @ReportDate = ISNULL(@ReportDate, CAST(DATEADD(DAY, -1, GETDATE()) AS DATE))
    
    DECLARE @SystemAvgCOP DECIMAL(5,2)
    DECLARE @DesignCOP DECIMAL(5,2) = 5.5
    DECLARE @COPRatio DECIMAL(5,2)
    DECLARE @TotalEnergy DECIMAL(12,2)
    DECLARE @BenchmarkEnergy DECIMAL(12,2)
    DECLARE @SavingPotential DECIMAL(12,2)
    DECLARE @Findings NVARCHAR(2000)
    DECLARE @Recommendations NVARCHAR(2000)
    DECLARE @LowEfficiency NVARCHAR(500)
    
    SELECT 
        @SystemAvgCOP = AVG(SystemCOP),
        @TotalEnergy = SUM(TotalPowerConsumption) / 120 -- 每30秒一条数据
    FROM SystemEfficiency
    WHERE CAST(Timestamp AS DATE) = @ReportDate
    
    IF @SystemAvgCOP IS NULL
        SET @SystemAvgCOP = 0
    
    SET @COPRatio = @SystemAvgCOP / @DesignCOP * 100
    SET @BenchmarkEnergy = @TotalEnergy * @DesignCOP / ISNULL(NULLIF(@SystemAvgCOP, 0), 1)
    SET @SavingPotential = @TotalEnergy - @BenchmarkEnergy
    
    -- 查找低效设备
    SELECT @LowEfficiency = STRING_AGG(d.DeviceName, ', ')
    FROM (
        SELECT DeviceId, AVG(COP) as AvgCOP
        FROM DeviceData
        WHERE CAST(Timestamp AS DATE) = @ReportDate AND COP IS NOT NULL
        GROUP BY DeviceId
    ) d1
    INNER JOIN Devices d ON d1.DeviceId = d.DeviceId
    WHERE d1.AvgCOP < d.DesignCOP * 0.7
        AND (d.DeviceTypeId = 1 OR d.DeviceTypeId = 2)
    
    SET @Findings = N'系统平均COP: ' + CAST(@SystemAvgCOP AS NVARCHAR(10)) + 
                    N'，设计COP: ' + CAST(@DesignCOP AS NVARCHAR(10)) +
                    N'，COP比值: ' + CAST(@COPRatio AS NVARCHAR(10)) + N'%'
    
    IF @COPRatio < 70
        SET @Findings = @Findings + N'；系统能效低于基准值70%，需重点关注。'
    ELSE
        SET @Findings = @Findings + N'；系统运行状况良好。'
    
    IF @LowEfficiency IS NOT NULL
        SET @Findings = @Findings + N'低效设备包括: ' + @LowEfficiency
    
    SET @Recommendations = N'1. 检查冷冻水/冷却水温度设定是否最优；2. 优化设备启停组合；3. 清理冷凝器管道；4. 检查水泵变频器运行状态；5. 定期清洗冷却塔填料'
    
    INSERT INTO EnergyDiagnosisReports (
        ReportDate, SystemAvgCOP, DesignCOP, COPRatio,
        TotalEnergyConsumption, BenchmarkEnergyConsumption,
        EnergySavingPotential, DiagnosisFindings, Recommendations,
        LowEfficiencyDevices
    ) VALUES (
        @ReportDate, @SystemAvgCOP, @DesignCOP, @COPRatio,
        @TotalEnergy, @BenchmarkEnergy, @SavingPotential,
        @Findings, @Recommendations, @LowEfficiency
    )
    
    SELECT SCOPE_IDENTITY() AS ReportId
END
GO

PRINT N'数据库初始化完成！'
GO
