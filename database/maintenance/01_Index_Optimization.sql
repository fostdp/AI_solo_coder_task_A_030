-- =============================================
-- 智能建筑中央空调冷站数据库
-- 索引优化脚本
-- =============================================

USE [ChillerPlantDB]
GO

-- =============================================
-- 1. DeviceData 表索引优化（时序数据表，最大最关键）
-- =============================================

-- 按设备和时间查询（最常用）
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DeviceData_DeviceId_Timestamp' AND object_id = OBJECT_ID('DeviceData'))
CREATE NONCLUSTERED INDEX [IX_DeviceData_DeviceId_Timestamp] ON [dbo].[DeviceData]
(
    [DeviceId] ASC,
    [Timestamp] DESC
)
INCLUDE ([Power], [SupplyTemperature], [ReturnTemperature], [Pressure], [FlowRate], [COP], [LoadRate])
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = ON, 
    DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON,
    FILLFACTOR = 90)
GO

-- 按时间范围查询（能效分析用）
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DeviceData_Timestamp_DeviceId' AND object_id = OBJECT_ID('DeviceData'))
CREATE NONCLUSTERED INDEX [IX_DeviceData_Timestamp_DeviceId] ON [dbo].[DeviceData]
(
    [Timestamp] DESC,
    [DeviceId] ASC
)
INCLUDE ([Power], [COP], [LoadRate])
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = ON, 
    DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON,
    FILLFACTOR = 90)
GO

-- 按设备类型查询（报表统计）
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DeviceData_DeviceType_Timestamp' AND object_id = OBJECT_ID('DeviceData'))
CREATE NONCLUSTERED INDEX [IX_DeviceData_DeviceType_Timestamp] ON [dbo].[DeviceData]
(
    [DeviceType] ASC,
    [Timestamp] DESC
)
INCLUDE ([Power], [COP])
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = ON, 
    DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON,
    FILLFACTOR = 90)
GO

-- =============================================
-- 2. EfficiencyRecords 表索引优化
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EfficiencyRecords_Timestamp' AND object_id = OBJECT_ID('EfficiencyRecords'))
CREATE NONCLUSTERED INDEX [IX_EfficiencyRecords_Timestamp] ON [dbo].[EfficiencyRecords]
(
    [Timestamp] DESC
)
INCLUDE ([SystemCOP], [EfficiencyRatio], [TotalPower], [TotalCoolingCapacity], [LoadRate])
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = ON, 
    DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON,
    FILLFACTOR = 95)
GO

-- =============================================
-- 3. Alarms 表索引优化
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Alarms_Status_StartTime' AND object_id = OBJECT_ID('Alarms'))
CREATE NONCLUSTERED INDEX [IX_Alarms_Status_StartTime] ON [dbo].[Alarms]
(
    [Status] ASC,
    [StartTime] DESC
)
INCLUDE ([DeviceId], [AlarmLevel], [AlarmType], [Message])
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = ON, 
    DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON,
    FILLFACTOR = 90)
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Alarms_DeviceId_StartTime' AND object_id = OBJECT_ID('Alarms'))
CREATE NONCLUSTERED INDEX [IX_Alarms_DeviceId_StartTime] ON [dbo].[Alarms]
(
    [DeviceId] ASC,
    [StartTime] DESC
)
INCLUDE ([AlarmLevel], [Status])
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = ON, 
    DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON,
    FILLFACTOR = 90)
GO

-- =============================================
-- 4. OptimizationRecommendations 表索引优化
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OptimizationRecommendations_GeneratedAt' AND object_id = OBJECT_ID('OptimizationRecommendations'))
CREATE NONCLUSTERED INDEX [IX_OptimizationRecommendations_GeneratedAt] ON [dbo].[OptimizationRecommendations]
(
    [GeneratedAt] DESC
)
INCLUDE ([PredictedCOP], [Status], [ChilledWaterSetpoint])
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = ON, 
    DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON,
    FILLFACTOR = 95)
GO

-- =============================================
-- 5. WorkOrders 表索引优化
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_WorkOrders_Status_CreatedAt' AND object_id = OBJECT_ID('WorkOrders'))
CREATE NONCLUSTERED INDEX [IX_WorkOrders_Status_CreatedAt] ON [dbo].[WorkOrders]
(
    [Status] ASC,
    [CreatedAt] DESC
)
INCLUDE ([AlarmId], [AssignedTo], [Priority])
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = ON, 
    DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON,
    FILLFACTOR = 90)
GO

-- =============================================
-- 6. 缺失索引查找（可手动执行查看优化建议）
-- =============================================

PRINT N'============================================='
PRINT N'索引创建完成'
PRINT N'============================================='

SELECT 
    dbschemas.[name] as 'Schema',
    dbtables.[name] as 'Table',
    dbindexes.[name] as 'Index',
    indexstats.avg_fragmentation_in_percent as 'FragmentationPercent',
    indexstats.page_count as 'PageCount'
FROM sys.dm_db_index_physical_stats (DB_ID(), NULL, NULL, NULL, NULL) AS indexstats
INNER JOIN sys.tables dbtables on dbtables.[object_id] = indexstats.[object_id]
INNER JOIN sys.schemas dbschemas on dbtables.[schema_id] = dbschemas.[schema_id]
INNER JOIN sys.indexes AS dbindexes ON dbindexes.[object_id] = indexstats.[object_id]
    AND indexstats.index_id = dbindexes.index_id
WHERE indexstats.database_id = DB_ID()
ORDER BY indexstats.avg_fragmentation_in_percent DESC
GO
