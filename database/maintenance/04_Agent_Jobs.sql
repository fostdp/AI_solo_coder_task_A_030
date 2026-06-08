-- =============================================
-- SQL Server Agent 自动维护作业
-- =============================================

USE [msdb]
GO

-- =============================================
-- 作业1: 每日统计信息更新 (01:00)
-- =============================================
IF NOT EXISTS (SELECT * FROM dbo.sysjobs WHERE name = N'ChillerPlant_Daily_Statistics_Update')
BEGIN
    EXEC dbo.sp_add_job
        @job_name = N'ChillerPlant_Daily_Statistics_Update',
        @enabled = 1,
        @description = N'冷站数据库每日统计信息更新',
        @owner_login_name = N'sa',
        @notify_level_eventlog = 2,
        @notify_level_email = 0,
        @notify_level_netsend = 0,
        @notify_level_page = 0,
        @delete_level = 0,
        @category_name = N'Database Maintenance'
END
GO

-- 添加作业步骤
DECLARE @jobId BINARY(16)
SELECT @jobId = job_id FROM dbo.sysjobs WHERE name = N'ChillerPlant_Daily_Statistics_Update'

IF NOT EXISTS (SELECT * FROM dbo.sysjobsteps WHERE job_id = @jobId AND step_name = N'执行统计信息更新')
BEGIN
    EXEC dbo.sp_add_jobstep
        @job_id = @jobId,
        @step_name = N'执行统计信息更新',
        @step_id = 1,
        @cmdexec_success_code = 0,
        @on_success_action = 1,
        @on_success_step_id = 0,
        @on_fail_action = 2,
        @on_fail_step_id = 0,
        @retry_attempts = 3,
        @retry_interval = 5,
        @os_run_priority = 0,
        @subsystem = N'TSQL',
        @command = N'EXEC [ChillerPlantDB].dbo.sp_executesql N''EXEC sp_updatestats''',
        @database_name = N'ChillerPlantDB',
        @flags = 0
END
GO

-- 添加调度
DECLARE @jobId BINARY(16)
SELECT @jobId = job_id FROM dbo.sysjobs WHERE name = N'ChillerPlant_Daily_Statistics_Update'

IF NOT EXISTS (SELECT * FROM dbo.sysjobschedules WHERE job_id = @jobId)
BEGIN
    EXEC dbo.sp_add_jobschedule
        @job_id = @jobId,
        @name = N'每日01:00执行',
        @freq_type = 4,
        @freq_interval = 1,
        @freq_subday_type = 1,
        @freq_subday_interval = 0,
        @freq_relative_interval = 0,
        @freq_recurrence_factor = 0,
        @active_start_date = 20240101,
        @active_end_date = 99991231,
        @active_start_time = 10000,
        @active_end_time = 235959
END
GO

-- =============================================
-- 作业2: 每周索引维护 (周日02:00)
-- =============================================
IF NOT EXISTS (SELECT * FROM dbo.sysjobs WHERE name = N'ChillerPlant_Weekly_Index_Maintenance')
BEGIN
    EXEC dbo.sp_add_job
        @job_name = N'ChillerPlant_Weekly_Index_Maintenance',
        @enabled = 1,
        @description = N'冷站数据库每周索引重建和重组',
        @owner_login_name = N'sa',
        @notify_level_eventlog = 2,
        @notify_level_email = 0,
        @notify_level_netsend = 0,
        @notify_level_page = 0,
        @delete_level = 0,
        @category_name = N'Database Maintenance'
END
GO

DECLARE @jobId BINARY(16)
SELECT @jobId = job_id FROM dbo.sysjobs WHERE name = N'ChillerPlant_Weekly_Index_Maintenance'

IF NOT EXISTS (SELECT * FROM dbo.sysjobsteps WHERE job_id = @jobId AND step_name = N'执行索引维护')
BEGIN
    EXEC dbo.sp_add_jobstep
        @job_id = @jobId,
        @step_name = N'执行索引维护',
        @step_id = 1,
        @cmdexec_success_code = 0,
        @on_success_action = 1,
        @on_success_step_id = 0,
        @on_fail_action = 2,
        @on_fail_step_id = 0,
        @retry_attempts = 2,
        @retry_interval = 10,
        @os_run_priority = 0,
        @subsystem = N'TSQL',
        @command = N':r /opt/mssql/maintenance/02_Index_Maintenance.sql',
        @database_name = N'ChillerPlantDB',
        @flags = 0
END
GO

DECLARE @jobId BINARY(16)
SELECT @jobId = job_id FROM dbo.sysjobs WHERE name = N'ChillerPlant_Weekly_Index_Maintenance'

IF NOT EXISTS (SELECT * FROM dbo.sysjobschedules WHERE job_id = @jobId)
BEGIN
    EXEC dbo.sp_add_jobschedule
        @job_id = @jobId,
        @name = N'每周日02:00执行',
        @freq_type = 8,
        @freq_interval = 1,
        @freq_subday_type = 1,
        @freq_subday_interval = 0,
        @freq_relative_interval = 0,
        @freq_recurrence_factor = 1,
        @active_start_date = 20240101,
        @active_end_date = 99991231,
        @active_start_time = 20000,
        @active_end_time = 235959
END
GO

-- =============================================
-- 作业3: 每日数据清理 (03:00)
-- =============================================
IF NOT EXISTS (SELECT * FROM dbo.sysjobs WHERE name = N'ChillerPlant_Daily_Data_Cleanup')
BEGIN
    EXEC dbo.sp_add_job
        @job_name = N'ChillerPlant_Daily_Data_Cleanup',
        @enabled = 1,
        @description = N'冷站数据库每日历史数据清理（保留365天）',
        @owner_login_name = N'sa',
        @notify_level_eventlog = 2,
        @notify_level_email = 0,
        @notify_level_netsend = 0,
        @notify_level_page = 0,
        @delete_level = 0,
        @category_name = N'Database Maintenance'
END
GO

DECLARE @jobId BINARY(16)
SELECT @jobId = job_id FROM dbo.sysjobs WHERE name = N'ChillerPlant_Daily_Data_Cleanup'

IF NOT EXISTS (SELECT * FROM dbo.sysjobsteps WHERE job_id = @jobId AND step_name = N'执行数据清理')
BEGIN
    EXEC dbo.sp_add_jobstep
        @job_id = @jobId,
        @step_name = N'执行数据清理',
        @step_id = 1,
        @cmdexec_success_code = 0,
        @on_success_action = 1,
        @on_success_step_id = 0,
        @on_fail_action = 2,
        @on_fail_step_id = 0,
        @retry_attempts = 3,
        @retry_interval = 5,
        @os_run_priority = 0,
        @subsystem = N'TSQL',
        @command = N'DECLARE @RetentionDays INT = 365
DECLARE @CutoffDate DATETIME = DATEADD(DAY, -@RetentionDays, GETUTCDATE())

DECLARE @RowsDeleted INT
DECLARE @TotalRowsDeleted INT = 0

WHILE 1 = 1
BEGIN
    DELETE TOP (10000)
    FROM [ChillerPlantDB].[dbo].[DeviceData]
    WHERE [Timestamp] < @CutoffDate

    SET @RowsDeleted = @@ROWCOUNT
    SET @TotalRowsDeleted += @RowsDeleted

    IF @RowsDeleted = 0 BREAK

    WAITFOR DELAY ''00:00:01''
END

PRINT ''清理完成，删除 '' + CAST(@TotalRowsDeleted AS NVARCHAR(20)) + '' 条DeviceData记录''',
        @database_name = N'ChillerPlantDB',
        @flags = 0
END
GO

DECLARE @jobId BINARY(16)
SELECT @jobId = job_id FROM dbo.sysjobs WHERE name = N'ChillerPlant_Daily_Data_Cleanup'

IF NOT EXISTS (SELECT * FROM dbo.sysjobschedules WHERE job_id = @jobId)
BEGIN
    EXEC dbo.sp_add_jobschedule
        @job_id = @jobId,
        @name = N'每日03:00执行',
        @freq_type = 4,
        @freq_interval = 1,
        @freq_subday_type = 1,
        @freq_subday_interval = 0,
        @freq_relative_interval = 0,
        @freq_recurrence_factor = 0,
        @active_start_date = 20240101,
        @active_end_date = 99991231,
        @active_start_time = 30000,
        @active_end_time = 235959
END
GO

-- 注册作业到服务器
EXEC dbo.sp_add_jobserver @job_name = N'ChillerPlant_Daily_Statistics_Update', @server_name = N'(local)'
EXEC dbo.sp_add_jobserver @job_name = N'ChillerPlant_Weekly_Index_Maintenance', @server_name = N'(local)'
EXEC dbo.sp_add_jobserver @job_name = N'ChillerPlant_Daily_Data_Cleanup', @server_name = N'(local)'
GO

PRINT N'============================================='
PRINT N'SQL Server Agent 作业创建完成'
PRINT N'============================================='
PRINT N'1. ChillerPlant_Daily_Statistics_Update - 每日01:00'
PRINT N'2. ChillerPlant_Weekly_Index_Maintenance - 每周日02:00'
PRINT N'3. ChillerPlant_Daily_Data_Cleanup - 每日03:00'
PRINT N'============================================='
GO
