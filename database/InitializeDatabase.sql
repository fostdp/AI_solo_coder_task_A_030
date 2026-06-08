-- =============================================
-- 智能建筑中央空调冷站群控与能效优化系统
-- SQL Server 数据库初始化脚本
-- =============================================

-- 创建数据库
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ChillerPlantOptimization')
BEGIN
    CREATE DATABASE ChillerPlantOptimization
    COLLATE Chinese_PRC_CI_AS;
END
GO

USE ChillerPlantOptimization;
GO

-- =============================================
-- 1. 设备类型枚举表
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DeviceTypes')
BEGIN
    CREATE TABLE DeviceTypes (
        Id INT PRIMARY KEY,
        Name NVARCHAR(50) NOT NULL,
        Description NVARCHAR(200) NULL
    );
    
    INSERT INTO DeviceTypes (Id, Name, Description) VALUES
    (1, 'CentrifugalChiller', '离心式冷水机组'),
    (2, 'ScrewChiller', '螺杆式冷水机组'),
    (3, 'CoolingTower', '冷却塔'),
    (4, 'ChilledWaterPump', '冷冻水泵'),
    (5, 'CoolingWaterPump', '冷却水泵');
END
GO

-- =============================================
-- 2. 设备状态枚举表
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DeviceStatuses')
BEGIN
    CREATE TABLE DeviceStatuses (
        Id INT PRIMARY KEY,
        Name NVARCHAR(50) NOT NULL
    );
    
    INSERT INTO DeviceStatuses (Id, Name) VALUES
    (0, 'Stopped'),
    (1, 'Running'),
    (2, 'Fault'),
    (3, 'Standby');
END
GO

-- =============================================
-- 3. 能效状态枚举表
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EfficiencyStatuses')
BEGIN
    CREATE TABLE EfficiencyStatuses (
        Id INT PRIMARY KEY,
        Name NVARCHAR(50) NOT NULL,
        Color NVARCHAR(20) NOT NULL,
        MinRatio DECIMAL(18,4) NULL,
        MaxRatio DECIMAL(18,4) NULL
    );
    
    INSERT INTO EfficiencyStatuses (Id, Name, Color, MinRatio, MaxRatio) VALUES
    (0, 'High', '#00B42A', 0.90, NULL),
    (1, 'Normal', '#FF7D00', 0.70, 0.90),
    (2, 'Low', '#F53F3F', NULL, 0.70),
    (3, 'Fault', '#F53F3F', NULL, NULL);
END
GO

-- =============================================
-- 4. 设备表
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Devices')
BEGIN
    CREATE TABLE Devices (
        Id NVARCHAR(50) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        DeviceTypeId INT NOT NULL FOREIGN KEY REFERENCES DeviceTypes(Id),
        DesignCOP DECIMAL(18,4) NOT NULL,
        RatedPower DECIMAL(18,4) NOT NULL,
        RatedCoolingCapacity DECIMAL(18,4) NOT NULL,
        BACnetAddress NVARCHAR(100) NOT NULL,
        BACnetInstance INT NOT NULL,
        PositionX INT NOT NULL,
        PositionY INT NOT NULL,
        Status INT NOT NULL DEFAULT 0 FOREIGN KEY REFERENCES DeviceStatuses(Id),
        EfficiencyStatus INT NOT NULL DEFAULT 0 FOREIGN KEY REFERENCES EfficiencyStatuses(Id),
        CurrentCOP DECIMAL(18,4) NULL,
        OperatingHours DECIMAL(18,4) NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    
    CREATE INDEX IX_Devices_DeviceType ON Devices (DeviceTypeId);
    CREATE INDEX IX_Devices_Status ON Devices (Status);
END
GO

-- =============================================
-- 5. 设备时序数据表
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DeviceData')
BEGIN
    CREATE TABLE DeviceData (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        DeviceId NVARCHAR(50) NOT NULL FOREIGN KEY REFERENCES Devices(Id),
        Timestamp DATETIME2 NOT NULL,
        Power DECIMAL(18,4) NOT NULL,
        SupplyTemperature DECIMAL(18,4) NOT NULL,
        ReturnTemperature DECIMAL(18,4) NOT NULL,
        Pressure DECIMAL(18,4) NOT NULL,
        FlowRate DECIMAL(18,4) NOT NULL,
        Frequency DECIMAL(18,4) NULL,
        Current DECIMAL(18,4) NULL,
        Voltage DECIMAL(18,4) NULL,
        InletTemperature DECIMAL(18,4) NULL,
        OutletTemperature DECIMAL(18,4) NULL,
        FanSpeed DECIMAL(18,4) NULL
    );
    
    CREATE CLUSTERED INDEX IX_DeviceData_Timestamp ON DeviceData (Timestamp DESC);
    CREATE INDEX IX_DeviceData_DeviceId_Timestamp ON DeviceData (DeviceId, Timestamp DESC);
END
GO

-- =============================================
-- 6. 告警级别枚举表
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AlarmLevels')
BEGIN
    CREATE TABLE AlarmLevels (
        Id INT PRIMARY KEY,
        Name NVARCHAR(50) NOT NULL,
        Description NVARCHAR(200) NULL
    );
    
    INSERT INTO AlarmLevels (Id, Name, Description) VALUES
    (1, '一级告警', '设备运行参数超限持续10分钟'),
    (2, '二级告警', '系统COP低于设计值60%持续30分钟');
END
GO

-- =============================================
-- 7. 告警类型枚举表
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AlarmTypes')
BEGIN
    CREATE TABLE AlarmTypes (
        Id INT PRIMARY KEY,
        Name NVARCHAR(50) NOT NULL
    );
    
    INSERT INTO AlarmTypes (Id, Name) VALUES
    (1, 'ParameterExceedance', '参数超限'),
    (2, 'LowEfficiency', '能效过低'),
    (3, 'SystemFault', '系统故障'),
    (4, 'CommunicationError', '通信故障');
END
GO

-- =============================================
-- 8. 告警状态枚举表
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AlarmStatuses')
BEGIN
    CREATE TABLE AlarmStatuses (
        Id INT PRIMARY KEY,
        Name NVARCHAR(50) NOT NULL
    );
    
    INSERT INTO AlarmStatuses (Id, Name) VALUES
    (0, 'Active'),
    (1, 'Acknowledged'),
    (2, 'Resolved'),
    (3, 'Cleared');
END
GO

-- =============================================
-- 9. 告警表
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Alarms')
BEGIN
    CREATE TABLE Alarms (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        DeviceId NVARCHAR(50) NULL FOREIGN KEY REFERENCES Devices(Id),
        AlarmLevel INT NOT NULL FOREIGN KEY REFERENCES AlarmLevels(Id),
        AlarmType INT NOT NULL FOREIGN KEY REFERENCES AlarmTypes(Id),
        ParameterName NVARCHAR(100) NULL,
        ParameterValue DECIMAL(18,4) NULL,
        ThresholdValue DECIMAL(18,4) NULL,
        Message NVARCHAR(500) NOT NULL,
        StartTime DATETIME2 NOT NULL,
        EndTime DATETIME2 NULL,
        Status INT NOT NULL DEFAULT 0 FOREIGN KEY REFERENCES AlarmStatuses(Id),
        DurationMinutes INT NOT NULL DEFAULT 0,
        Acknowledged BIT NOT NULL DEFAULT 0,
        AcknowledgedBy NVARCHAR(50) NULL,
        AcknowledgedAt DATETIME2 NULL,
        WeChatPushed BIT NOT NULL DEFAULT 0,
        WeChatPushedAt DATETIME2 NULL
    );
    
    CREATE INDEX IX_Alarms_Status ON Alarms (Status);
    CREATE INDEX IX_Alarms_StartTime ON Alarms (StartTime DESC);
    CREATE INDEX IX_Alarms_DeviceId ON Alarms (DeviceId);
END
GO

-- =============================================
-- 10. 告警阈值配置表
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AlarmThresholds')
BEGIN
    CREATE TABLE AlarmThresholds (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ParameterName NVARCHAR(100) NOT NULL,
        DeviceTypeId INT NULL FOREIGN KEY REFERENCES DeviceTypes(Id),
        UpperLimit DECIMAL(18,4) NULL,
        LowerLimit DECIMAL(18,4) NULL,
        DurationMinutes INT NOT NULL DEFAULT 10,
        AlarmLevel INT NOT NULL DEFAULT 1 FOREIGN KEY REFERENCES AlarmLevels(Id),
        Enabled BIT NOT NULL DEFAULT 1
    );
    
    INSERT INTO AlarmThresholds (ParameterName, DeviceTypeId, UpperLimit, DurationMinutes, AlarmLevel) VALUES
    ('Power', 1, 1200, 10, 1),
    ('SupplyTemperature', 1, 10, 10, 1),
    ('ReturnTemperature', 1, 18, 10, 1),
    ('Pressure', 1, 1.6, 10, 1),
    ('Current', 1, 200, 10, 1),
    ('Power', 2, 800, 10, 1),
    ('SupplyTemperature', 2, 10, 10, 1),
    ('FanSpeed', 3, 1500, 10, 1),
    ('InletTemperature', 3, 40, 10, 1),
    ('Power', 4, 110, 10, 1),
    ('Pressure', 4, 2.0, 10, 1),
    ('FlowRate', 4, 1200, 10, 1),
    ('Power', 5, 110, 10, 1),
    ('Pressure', 5, 2.0, 10, 1),
    ('FlowRate', 5, 1200, 10, 1);
END
GO

-- =============================================
-- 11. 工单状态枚举表
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkOrderStatuses')
BEGIN
    CREATE TABLE WorkOrderStatuses (
        Id INT PRIMARY KEY,
        Name NVARCHAR(50) NOT NULL
    );
    
    INSERT INTO WorkOrderStatuses (Id, Name) VALUES
    (0, 'Pending'),
    (1, 'Assigned'),
    (2, 'InProgress'),
    (3, 'Completed'),
    (4, 'Cancelled');
END
GO

-- =============================================
-- 12. 工单表
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkOrders')
BEGIN
    CREATE TABLE WorkOrders (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        AlarmId BIGINT NULL FOREIGN KEY REFERENCES Alarms(Id),
        WorkOrderNo NVARCHAR(50) NOT NULL UNIQUE,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(1000) NOT NULL,
        Assignee NVARCHAR(50) NULL,
        Status INT NOT NULL DEFAULT 0 FOREIGN KEY REFERENCES WorkOrderStatuses(Id),
        Priority INT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CompletedAt DATETIME2 NULL,
        CompletedBy NVARCHAR(50) NULL,
        Resolution NVARCHAR(1000) NULL
    );
    
    CREATE INDEX IX_WorkOrders_Status ON WorkOrders (Status);
    CREATE INDEX IX_WorkOrders_AlarmId ON WorkOrders (AlarmId);
END
GO

-- =============================================
-- 13. 能效记录表
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EfficiencyRecords')
BEGIN
    CREATE TABLE EfficiencyRecords (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        Timestamp DATETIME2 NOT NULL,
        SystemCOP DECIMAL(18,4) NOT NULL,
        DesignCOP DECIMAL(18,4) NOT NULL,
        DesignCOPRatio DECIMAL(18,4) NOT NULL,
        TotalPower DECIMAL(18,4) NOT NULL,
        TotalCoolingCapacity DECIMAL(18,4) NOT NULL,
        ChilledWaterSupplyTemp DECIMAL(18,4) NOT NULL,
        ChilledWaterReturnTemp DECIMAL(18,4) NOT NULL,
        CoolingWaterSupplyTemp DECIMAL(18,4) NOT NULL,
        CoolingWaterReturnTemp DECIMAL(18,4) NOT NULL,
        LoadRate DECIMAL(18,4) NOT NULL,
        DailyEnergyConsumption DECIMAL(18,4) NOT NULL,
        EnergySaving DECIMAL(18,4) NOT NULL
    );
    
    CREATE CLUSTERED INDEX IX_EfficiencyRecords_Timestamp ON EfficiencyRecords (Timestamp DESC);
END
GO

-- =============================================
-- 14. 优化推荐状态枚举表
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RecommendationStatuses')
BEGIN
    CREATE TABLE RecommendationStatuses (
        Id INT PRIMARY KEY,
        Name NVARCHAR(50) NOT NULL
    );
    
    INSERT INTO RecommendationStatuses (Id, Name) VALUES
    (0, 'New'),
    (1, 'Applied'),
    (2, 'Rejected'),
    (3, 'Expired');
END
GO

-- =============================================
-- 15. 优化推荐表
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OptimizationRecommendations')
BEGIN
    CREATE TABLE OptimizationRecommendations (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        GeneratedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        DeviceCombination NVARCHAR(500) NOT NULL,
        RunningChillers NVARCHAR(200) NOT NULL,
        RunningPumps NVARCHAR(500) NOT NULL,
        RunningTowers NVARCHAR(200) NOT NULL,
        PredictedCOP DECIMAL(18,4) NOT NULL,
        PredictedPower DECIMAL(18,4) NOT NULL,
        ChilledWaterSetpoint DECIMAL(18,4) NOT NULL,
        ExpectedEnergySaving DECIMAL(18,4) NOT NULL,
        ExpectedSavingPercent DECIMAL(18,4) NOT NULL,
        LoadRate DECIMAL(18,4) NOT NULL,
        AmbientTemp DECIMAL(18,4) NULL,
        Status INT NOT NULL DEFAULT 0 FOREIGN KEY REFERENCES RecommendationStatuses(Id),
        AppliedAt DATETIME2 NULL,
        AppliedBy NVARCHAR(50) NULL,
        ActualCOP DECIMAL(18,4) NULL,
        ActualEnergySaving DECIMAL(18,4) NULL
    );
    
    CREATE INDEX IX_OptimizationRecommendations_GeneratedAt ON OptimizationRecommendations (GeneratedAt DESC);
END
GO

-- =============================================
-- 16. 系统指标表
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SystemMetrics')
BEGIN
    CREATE TABLE SystemMetrics (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        DailyEnergy DECIMAL(18,4) NOT NULL,
        RealtimeCOP DECIMAL(18,4) NOT NULL,
        EnergySaving DECIMAL(18,4) NOT NULL,
        PeakPower DECIMAL(18,4) NOT NULL,
        RunningDeviceCount INT NOT NULL,
        TotalDeviceCount INT NOT NULL
    );
    
    CREATE INDEX IX_SystemMetrics_Timestamp ON SystemMetrics (Timestamp DESC);
END
GO

-- =============================================
-- 17. 节能诊断报告表
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DiagnosisReports')
BEGIN
    CREATE TABLE DiagnosisReports (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        GeneratedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        ReportDate DATE NOT NULL,
        SystemAverageCOP DECIMAL(18,4) NOT NULL,
        DesignCOPRatio DECIMAL(18,4) NOT NULL,
        TotalEnergyConsumption DECIMAL(18,4) NOT NULL,
        TotalEnergySaving DECIMAL(18,4) NOT NULL,
        LowEfficiencyDevices NVARCHAR(500) NULL,
        DiagnosisContent NVARCHAR(MAX) NOT NULL,
        Recommendations NVARCHAR(MAX) NOT NULL
    );
    
    CREATE INDEX IX_DiagnosisReports_ReportDate ON DiagnosisReports (ReportDate DESC);
END
GO

-- =============================================
-- 18. 用户表
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE Users (
        Id NVARCHAR(50) PRIMARY KEY,
        Username NVARCHAR(50) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(200) NOT NULL,
        RealName NVARCHAR(50) NOT NULL,
        Role INT NOT NULL,
        Email NVARCHAR(100) NULL,
        Phone NVARCHAR(20) NULL,
        WeChatUserId NVARCHAR(100) NULL,
        Enabled BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        LastLoginAt DATETIME2 NULL
    );
    
    INSERT INTO Users (Id, Username, PasswordHash, RealName, Role, Email, Phone) VALUES
    ('U001', 'admin', 'AQAAAAEAACcQAAAAELsJQw==', '系统管理员', 0, 'admin@chiller.com', '13800138000'),
    ('U002', 'engineer', 'AQAAAAEAACcQAAAAELsJQw==', '运维工程师', 1, 'engineer@chiller.com', '13800138001'),
    ('U003', 'manager', 'AQAAAAEAACcQAAAAELsJQw==', '管理者', 2, 'manager@chiller.com', '13800138002');
END
GO

-- =============================================
-- 19. 系统配置表
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SystemSettings')
BEGIN
    CREATE TABLE SystemSettings (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        SettingKey NVARCHAR(100) NOT NULL UNIQUE,
        SettingValue NVARCHAR(500) NOT NULL,
        Description NVARCHAR(200) NULL,
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    
    INSERT INTO SystemSettings (SettingKey, SettingValue, Description) VALUES
    ('SystemDesignCOP', '5.5', '系统设计COP'),
    ('ChilledWaterDefaultSetpoint', '7.0', '冷冻水默认设定温度(°C)'),
    ('LowEfficiencyThreshold', '0.70', '低能效阈值(设计COP比值)'),
    ('CriticalLowEfficiencyThreshold', '0.60', '严重低能效阈值(设计COP比值)'),
    ('OptimizationIntervalMinutes', '60', '优化方案更新间隔(分钟)'),
    ('WeChatWebhookUrl', '', '企业微信机器人Webhook地址'),
    ('WeChatCorpId', '', '企业微信CorpId'),
    ('WeChatAgentId', '', '企业微信AgentId'),
    ('WeChatAppSecret', '', '企业微信AppSecret');
END
GO

-- =============================================
-- 20. 初始化设备数据
-- =============================================
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Devices')
BEGIN
    DELETE FROM Devices;
    
    -- 3台离心式冷水机组
    INSERT INTO Devices (Id, Name, DeviceTypeId, DesignCOP, RatedPower, RatedCoolingCapacity, BACnetAddress, BACnetInstance, PositionX, PositionY) VALUES
    ('CH-C-01', '离心机组1#', 1, 6.2, 1000, 6200, '192.168.1.11', 1, 150, 200),
    ('CH-C-02', '离心机组2#', 1, 6.2, 1000, 6200, '192.168.1.12', 2, 150, 350),
    ('CH-C-03', '离心机组3#', 1, 6.2, 1000, 6200, '192.168.1.13', 3, 150, 500);
    
    -- 2台螺杆式冷水机组
    INSERT INTO Devices (Id, Name, DeviceTypeId, DesignCOP, RatedPower, RatedCoolingCapacity, BACnetAddress, BACnetInstance, PositionX, PositionY) VALUES
    ('CH-S-01', '螺杆机组1#', 2, 5.0, 600, 3000, '192.168.1.14', 4, 300, 275),
    ('CH-S-02', '螺杆机组2#', 2, 5.0, 600, 3000, '192.168.1.15', 5, 300, 425);
    
    -- 8台冷却塔
    INSERT INTO Devices (Id, Name, DeviceTypeId, DesignCOP, RatedPower, RatedCoolingCapacity, BACnetAddress, BACnetInstance, PositionX, PositionY) VALUES
    ('CT-01', '冷却塔1#', 3, 35.0, 22, 6200, '192.168.1.21', 11, 550, 150),
    ('CT-02', '冷却塔2#', 3, 35.0, 22, 6200, '192.168.1.22', 12, 550, 240),
    ('CT-03', '冷却塔3#', 3, 35.0, 22, 3000, '192.168.1.23', 13, 550, 330),
    ('CT-04', '冷却塔4#', 3, 35.0, 22, 3000, '192.168.1.24', 14, 550, 420),
    ('CT-05', '冷却塔5#', 3, 35.0, 22, 3000, '192.168.1.25', 15, 550, 510),
    ('CT-06', '冷却塔6#', 3, 35.0, 22, 3000, '192.168.1.26', 16, 550, 600),
    ('CT-07', '冷却塔7#', 3, 35.0, 22, 3000, '192.168.1.27', 17, 700, 200),
    ('CT-08', '冷却塔8#', 3, 35.0, 22, 3000, '192.168.1.28', 18, 700, 500);
    
    -- 12台冷冻水泵
    INSERT INTO Devices (Id, Name, DeviceTypeId, DesignCOP, RatedPower, RatedCoolingCapacity, BACnetAddress, BACnetInstance, PositionX, PositionY) VALUES
    ('CHWP-01', '冷冻水泵1#', 4, 0.85, 90, 0, '192.168.1.31', 21, 50, 150),
    ('CHWP-02', '冷冻水泵2#', 4, 0.85, 90, 0, '192.168.1.32', 22, 50, 220),
    ('CHWP-03', '冷冻水泵3#', 4, 0.85, 90, 0, '192.168.1.33', 23, 50, 290),
    ('CHWP-04', '冷冻水泵4#', 4, 0.85, 90, 0, '192.168.1.34', 24, 50, 360),
    ('CHWP-05', '冷冻水泵5#', 4, 0.85, 90, 0, '192.168.1.35', 25, 50, 430),
    ('CHWP-06', '冷冻水泵6#', 4, 0.85, 90, 0, '192.168.1.36', 26, 50, 500),
    ('CHWP-07', '冷冻水泵7#', 4, 0.85, 90, 0, '192.168.1.37', 27, 50, 570),
    ('CHWP-08', '冷冻水泵8#', 4, 0.85, 90, 0, '192.168.1.38', 28, 50, 640),
    ('CHWP-09', '冷冻水泵9#', 4, 0.85, 90, 0, '192.168.1.39', 29, -80, 150),
    ('CHWP-10', '冷冻水泵10#', 4, 0.85, 90, 0, '192.168.1.40', 30, -80, 290),
    ('CHWP-11', '冷冻水泵11#', 4, 0.85, 90, 0, '192.168.1.41', 31, -80, 430),
    ('CHWP-12', '冷冻水泵12#', 4, 0.85, 90, 0, '192.168.1.42', 32, -80, 570);
    
    -- 12台冷却水泵
    INSERT INTO Devices (Id, Name, DeviceTypeId, DesignCOP, RatedPower, RatedCoolingCapacity, BACnetAddress, BACnetInstance, PositionX, PositionY) VALUES
    ('CWP-01', '冷却水泵1#', 5, 0.85, 90, 0, '192.168.1.51', 41, 850, 150),
    ('CWP-02', '冷却水泵2#', 5, 0.85, 90, 0, '192.168.1.52', 42, 850, 220),
    ('CWP-03', '冷却水泵3#', 5, 0.85, 90, 0, '192.168.1.53', 43, 850, 290),
    ('CWP-04', '冷却水泵4#', 5, 0.85, 90, 0, '192.168.1.54', 44, 850, 360),
    ('CWP-05', '冷却水泵5#', 5, 0.85, 90, 0, '192.168.1.55', 45, 850, 430),
    ('CWP-06', '冷却水泵6#', 5, 0.85, 90, 0, '192.168.1.56', 46, 850, 500),
    ('CWP-07', '冷却水泵7#', 5, 0.85, 90, 0, '192.168.1.57', 47, 850, 570),
    ('CWP-08', '冷却水泵8#', 5, 0.85, 90, 0, '192.168.1.58', 48, 850, 640),
    ('CWP-09', '冷却水泵9#', 5, 0.85, 90, 0, '192.168.1.59', 49, 980, 200),
    ('CWP-10', '冷却水泵10#', 5, 0.85, 90, 0, '192.168.1.60', 50, 980, 320),
    ('CWP-11', '冷却水泵11#', 5, 0.85, 90, 0, '192.168.1.61', 51, 980, 440),
    ('CWP-12', '冷却水泵12#', 5, 0.85, 90, 0, '192.168.1.62', 52, 980, 560);
END
GO

-- =============================================
-- 21. 创建存储过程 - 计算系统实时COP
-- =============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_CalculateSystemCOP')
    DROP PROCEDURE sp_CalculateSystemCOP;
GO

CREATE PROCEDURE sp_CalculateSystemCOP
    @Timestamp DATETIME2
AS
BEGIN
    DECLARE @TotalPower DECIMAL(18,4)
    DECLARE @TotalCoolingCapacity DECIMAL(18,4)
    DECLARE @DesignCOP DECIMAL(18,4)
    DECLARE @SystemCOP DECIMAL(18,4)
    DECLARE @ChilledSupplyTemp DECIMAL(18,4)
    DECLARE @ChilledReturnTemp DECIMAL(18,4)
    DECLARE @CoolingSupplyTemp DECIMAL(18,4)
    DECLARE @CoolingReturnTemp DECIMAL(18,4)
    DECLARE @TotalFlowRate DECIMAL(18,4)
    DECLARE @LoadRate DECIMAL(18,4)
    DECLARE @DailyEnergy DECIMAL(18,4)
    DECLARE @EnergySaving DECIMAL(18,4)
    DECLARE @RunningCount INT
    DECLARE @TotalCount INT
    
    SELECT @DesignCOP = CAST(SettingValue AS DECIMAL(18,4))
    FROM SystemSettings WHERE SettingKey = 'SystemDesignCOP'
    
    SELECT 
        @TotalPower = SUM(dd.Power),
        @ChilledSupplyTemp = AVG(CASE WHEN d.DeviceTypeId = 4 THEN dd.SupplyTemperature ELSE NULL END),
        @ChilledReturnTemp = AVG(CASE WHEN d.DeviceTypeId = 4 THEN dd.ReturnTemperature ELSE NULL END),
        @CoolingSupplyTemp = AVG(CASE WHEN d.DeviceTypeId = 5 THEN dd.SupplyTemperature ELSE NULL END),
        @CoolingReturnTemp = AVG(CASE WHEN d.DeviceTypeId = 5 THEN dd.ReturnTemperature ELSE NULL END),
        @TotalFlowRate = SUM(CASE WHEN d.DeviceTypeId = 4 THEN dd.FlowRate ELSE 0 END),
        @RunningCount = COUNT(DISTINCT dd.DeviceId)
    FROM DeviceData dd
    INNER JOIN Devices d ON dd.DeviceId = d.Id
    WHERE dd.Timestamp >= DATEADD(SECOND, -60, @Timestamp)
        AND dd.Timestamp <= @Timestamp
        AND d.Status = 1
    
    SELECT @TotalCount = COUNT(*) FROM Devices WHERE Status <> 2
    
    IF @TotalFlowRate > 0 AND @ChilledReturnTemp > @ChilledSupplyTemp
    BEGIN
        SET @TotalCoolingCapacity = @TotalFlowRate * 4.186 * (@ChilledReturnTemp - @ChilledSupplyTemp) / 3600
        SET @SystemCOP = @TotalCoolingCapacity / @TotalPower
        
        DECLARE @TotalRatedCooling DECIMAL(18,4)
        SELECT @TotalRatedCooling = SUM(RatedCoolingCapacity) 
        FROM Devices WHERE DeviceTypeId IN (1,2) AND Status = 1
        
        SET @LoadRate = CASE WHEN @TotalRatedCooling > 0 
                            THEN @TotalCoolingCapacity / @TotalRatedCooling 
                            ELSE 0 END
    END
    ELSE
    BEGIN
        SET @TotalCoolingCapacity = 0
        SET @SystemCOP = 0
        SET @LoadRate = 0
    END
    
    SELECT @DailyEnergy = SUM(Power / 120)
    FROM DeviceData dd
    WHERE dd.Timestamp >= CAST(@Timestamp AS DATE)
        AND dd.Timestamp <= @Timestamp
    
    SET @EnergySaving = @DailyEnergy * 0.15
    
    INSERT INTO EfficiencyRecords (
        Timestamp, SystemCOP, DesignCOP, DesignCOPRatio, TotalPower, TotalCoolingCapacity,
        ChilledWaterSupplyTemp, ChilledWaterReturnTemp, CoolingWaterSupplyTemp, CoolingWaterReturnTemp,
        LoadRate, DailyEnergyConsumption, EnergySaving
    ) VALUES (
        @Timestamp, ISNULL(@SystemCOP, 0), ISNULL(@DesignCOP, 5.5),
        CASE WHEN @DesignCOP > 0 THEN ISNULL(@SystemCOP, 0) / @DesignCOP ELSE 0 END,
        ISNULL(@TotalPower, 0), ISNULL(@TotalCoolingCapacity, 0),
        ISNULL(@ChilledSupplyTemp, 0), ISNULL(@ChilledReturnTemp, 0),
        ISNULL(@CoolingSupplyTemp, 0), ISNULL(@CoolingReturnTemp, 0),
        ISNULL(@LoadRate, 0), ISNULL(@DailyEnergy, 0), ISNULL(@EnergySaving, 0)
    )
    
    INSERT INTO SystemMetrics (
        Timestamp, DailyEnergy, RealtimeCOP, EnergySaving, PeakPower,
        RunningDeviceCount, TotalDeviceCount
    ) VALUES (
        @Timestamp, ISNULL(@DailyEnergy, 0), ISNULL(@SystemCOP, 0),
        ISNULL(@EnergySaving, 0), ISNULL(@TotalPower, 0),
        ISNULL(@RunningCount, 0), ISNULL(@TotalCount, 37)
    )
END
GO

-- =============================================
-- 22. 创建存储过程 - 生成节能诊断报告
-- =============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GenerateDiagnosisReport')
    DROP PROCEDURE sp_GenerateDiagnosisReport;
GO

CREATE PROCEDURE sp_GenerateDiagnosisReport
    @ReportDate DATE
AS
BEGIN
    DECLARE @AvgCOP DECIMAL(18,4)
    DECLARE @DesignCOP DECIMAL(18,4)
    DECLARE @DesignCOPRatio DECIMAL(18,4)
    DECLARE @TotalEnergy DECIMAL(18,4)
    DECLARE @TotalSaving DECIMAL(18,4)
    DECLARE @LowEfficiencyDevices NVARCHAR(500)
    DECLARE @Diagnosis NVARCHAR(MAX)
    DECLARE @Recommendations NVARCHAR(MAX)
    
    SELECT @DesignCOP = CAST(SettingValue AS DECIMAL(18,4))
    FROM SystemSettings WHERE SettingKey = 'SystemDesignCOP'
    
    SELECT 
        @AvgCOP = AVG(SystemCOP),
        @DesignCOPRatio = AVG(DesignCOPRatio),
        @TotalEnergy = MAX(DailyEnergyConsumption),
        @TotalSaving = SUM(EnergySaving)
    FROM EfficiencyRecords
    WHERE CAST(Timestamp AS DATE) = @ReportDate
    
    SELECT @LowEfficiencyDevices = STRING_AGG(d.Name, ', ')
    FROM Devices d
    WHERE d.DeviceTypeId IN (1,2)
        AND d.Status = 1
        AND d.CurrentCOP < @DesignCOP * 0.7
    
    SET @Diagnosis = N'
## 系统能效诊断报告

**报告日期**: ' + CONVERT(NVARCHAR(20), @ReportDate, 23) + N'

### 一、系统整体能效评估

- 系统平均COP: ' + CAST(ISNULL(@AvgCOP, 0) AS NVARCHAR(20)) + N'
- 设计COP: ' + CAST(ISNULL(@DesignCOP, 5.5) AS NVARCHAR(20)) + N'
- 能效比: ' + CAST(ISNULL(@DesignCOPRatio * 100, 0) AS NVARCHAR(20)) + N'%
- 日总能耗: ' + CAST(ISNULL(@TotalEnergy, 0) AS NVARCHAR(20)) + N' kWh
- 预计节能量: ' + CAST(ISNULL(@TotalSaving, 0) AS NVARCHAR(20)) + N' kWh

### 二、能效问题诊断

'
    
    IF @DesignCOPRatio < 0.7
    BEGIN
        SET @Diagnosis = @Diagnosis + N'⚠️ **严重问题**: 系统整体能效低于设计值70%，需立即检查优化。

'
    END
    ELSE IF @DesignCOPRatio < 0.9
    BEGIN
        SET @Diagnosis = @Diagnosis + N'⚡ **需关注**: 系统整体能效偏低，建议进行运行参数优化。

'
    END
    
    IF @LowEfficiencyDevices IS NOT NULL
    BEGIN
        SET @Diagnosis = @Diagnosis + N'🔧 **低效设备**: 以下设备能效偏低，建议检查：
' + @LowEfficiencyDevices + N'

'
    END
    
    SET @Recommendations = N'### 三、优化建议

1. **设备组合优化**: 根据当前冷负荷重新计算最优设备启停组合
2. **温度设定值优化**: 适当提高冷冻水出水温度设定值
3. **水泵变频优化**: 根据供回水温差调节水泵频率
4. **冷却塔优化**: 根据湿球温度调整冷却塔运行台数
5. **定期维护**: 加强设备定期维护保养，保持换热效率

### 四、预期节能效果

如全部实施优化建议，预计可提升系统COP 10%-15%，日节能量可达 ' + CAST(ISNULL(@TotalEnergy * 0.15, 0) AS NVARCHAR(20)) + N' kWh。
'
    
    INSERT INTO DiagnosisReports (
        ReportDate, SystemAverageCOP, DesignCOPRatio, TotalEnergyConsumption,
        TotalEnergySaving, LowEfficiencyDevices, DiagnosisContent, Recommendations
    ) VALUES (
        @ReportDate, ISNULL(@AvgCOP, 0), ISNULL(@DesignCOPRatio, 0),
        ISNULL(@TotalEnergy, 0), ISNULL(@TotalSaving, 0),
        @LowEfficiencyDevices, @Diagnosis, @Recommendations
    )
    
    SELECT * FROM DiagnosisReports WHERE Id = SCOPE_IDENTITY()
END
GO

PRINT '数据库初始化完成！'
GO
