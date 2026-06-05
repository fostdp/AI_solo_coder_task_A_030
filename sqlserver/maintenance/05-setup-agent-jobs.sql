-- ========================================
--  SQL Server Agent Jobs Setup
-- ========================================

SET NOCOUNT ON;
GO

USE [msdb];
GO

PRINT '========================================';
PRINT '  Setting up SQL Server Agent Jobs';
PRINT '  ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '========================================';
GO

-- Enable Agent XPs
EXEC sp_configure 'show advanced options', 1;
RECONFIGURE;
EXEC sp_configure 'Agent XPs', 1;
RECONFIGURE;
GO

-- ========================================
--  Job 1: Index Optimization (Daily at 02:00)
-- ========================================
IF EXISTS (SELECT name FROM sysjobs WHERE name = 'ChillerPlant_Index_Optimization')
    EXEC sp_delete_job @job_name = 'ChillerPlant_Index_Optimization';
GO

DECLARE @JobID BINARY(16);

EXEC sp_add_job
    @job_name = N'ChillerPlant_Index_Optimization',
    @enabled = 1,
    @notify_level_eventlog = 2,
    @notify_level_email = 2,
    @notify_level_netsend = 2,
    @notify_level_page = 2,
    @delete_level = 0,
    @description = N'Daily index optimization for ChillerPlantDB',
    @category_name = N'Database Maintenance',
    @owner_login_name = N'sa',
    @job_id = @JobID OUTPUT;

EXEC sp_add_jobstep
    @job_id = @JobID,
    @step_name = N'Run Index Optimization',
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
    @command = N':r /maintenance/01-index-optimization.sql',
    @database_name = N'ChillerPlantDB',
    @flags = 0;

EXEC sp_add_jobschedule
    @job_id = @JobID,
    @name = N'Daily_0200',
    @enabled = 1,
    @freq_type = 4,
    @freq_interval = 1,
    @freq_subday_type = 1,
    @freq_subday_interval = 0,
    @freq_relative_interval = 0,
    @freq_recurrence_factor = 1,
    @active_start_date = 20240101,
    @active_end_date = 99991231,
    @active_start_time = 20000,
    @active_end_time = 235959;

EXEC sp_add_jobserver
    @job_id = @JobID,
    @server_name = N'(LOCAL)';

PRINT 'Created job: ChillerPlant_Index_Optimization (Daily 02:00)';
GO

-- ========================================
--  Job 2: Update Statistics (Every 6 hours)
-- ========================================
IF EXISTS (SELECT name FROM sysjobs WHERE name = 'ChillerPlant_Update_Statistics')
    EXEC sp_delete_job @job_name = 'ChillerPlant_Update_Statistics';
GO

DECLARE @JobID BINARY(16);

EXEC sp_add_job
    @job_name = N'ChillerPlant_Update_Statistics',
    @enabled = 1,
    @notify_level_eventlog = 2,
    @notify_level_email = 2,
    @notify_level_netsend = 2,
    @notify_level_page = 2,
    @delete_level = 0,
    @description = N'Update statistics every 6 hours',
    @category_name = N'Database Maintenance',
    @owner_login_name = N'sa',
    @job_id = @JobID OUTPUT;

EXEC sp_add_jobstep
    @job_id = @JobID,
    @step_name = N'Run Update Statistics',
    @step_id = 1,
    @cmdexec_success_code = 0,
    @on_success_action = 1,
    @on_success_step_id = 0,
    @on_fail_action = 2,
    @on_fail_step_id = 0,
    @retry_attempts = 2,
    @retry_interval = 5,
    @os_run_priority = 0,
    @subsystem = N'TSQL',
    @command = N':r /maintenance/02-update-statistics.sql',
    @database_name = N'ChillerPlantDB',
    @flags = 0;

EXEC sp_add_jobschedule
    @job_id = @JobID,
    @name = N'Every_6_Hours',
    @enabled = 1,
    @freq_type = 4,
    @freq_interval = 1,
    @freq_subday_type = 8,
    @freq_subday_interval = 6,
    @freq_relative_interval = 0,
    @freq_recurrence_factor = 1,
    @active_start_date = 20240101,
    @active_end_date = 99991231,
    @active_start_time = 0,
    @active_end_time = 235959;

EXEC sp_add_jobserver
    @job_id = @JobID,
    @server_name = N'(LOCAL)';

PRINT 'Created job: ChillerPlant_Update_Statistics (Every 6 hours)';
GO

-- ========================================
--  Job 3: Database Backup (Daily at 03:00)
-- ========================================
IF EXISTS (SELECT name FROM sysjobs WHERE name = 'ChillerPlant_Database_Backup')
    EXEC sp_delete_job @job_name = 'ChillerPlant_Database_Backup';
GO

DECLARE @JobID BINARY(16);

EXEC sp_add_job
    @job_name = N'ChillerPlant_Database_Backup',
    @enabled = 1,
    @notify_level_eventlog = 2,
    @notify_level_email = 2,
    @notify_level_netsend = 2,
    @notify_level_page = 2,
    @delete_level = 0,
    @description = N'Daily database backup',
    @category_name = N'Database Maintenance',
    @owner_login_name = N'sa',
    @job_id = @JobID OUTPUT;

EXEC sp_add_jobstep
    @job_id = @JobID,
    @step_name = N'Run Database Backup',
    @step_id = 1,
    @cmdexec_success_code = 0,
    @on_success_action = 1,
    @on_success_step_id = 0,
    @on_fail_action = 2,
    @on_fail_step_id = 0,
    @retry_attempts = 3,
    @retry_interval = 10,
    @os_run_priority = 0,
    @subsystem = N'TSQL',
    @command = N':r /maintenance/03-database-backup.sql',
    @database_name = N'master',
    @flags = 0;

EXEC sp_add_jobschedule
    @job_id = @JobID,
    @name = N'Daily_0300',
    @enabled = 1,
    @freq_type = 4,
    @freq_interval = 1,
    @freq_subday_type = 1,
    @freq_subday_interval = 0,
    @freq_relative_interval = 0,
    @freq_recurrence_factor = 1,
    @active_start_date = 20240101,
    @active_end_date = 99991231,
    @active_start_time = 30000,
    @active_end_time = 235959;

EXEC sp_add_jobserver
    @job_id = @JobID,
    @server_name = N'(LOCAL)';

PRINT 'Created job: ChillerPlant_Database_Backup (Daily 03:00)';
GO

-- ========================================
--  Job 4: Data Cleanup (Daily at 04:00)
-- ========================================
IF EXISTS (SELECT name FROM sysjobs WHERE name = 'ChillerPlant_Data_Cleanup')
    EXEC sp_delete_job @job_name = 'ChillerPlant_Data_Cleanup';
GO

DECLARE @JobID BINARY(16);

EXEC sp_add_job
    @job_name = N'ChillerPlant_Data_Cleanup',
    @enabled = 1,
    @notify_level_eventlog = 2,
    @notify_level_email = 2,
    @notify_level_netsend = 2,
    @notify_level_page = 2,
    @delete_level = 0,
    @description = N'Daily historical data cleanup',
    @category_name = N'Database Maintenance',
    @owner_login_name = N'sa',
    @job_id = @JobID OUTPUT;

EXEC sp_add_jobstep
    @job_id = @JobID,
    @step_name = N'Run Data Cleanup',
    @step_id = 1,
    @cmdexec_success_code = 0,
    @on_success_action = 1,
    @on_success_step_id = 0,
    @on_fail_action = 2,
    @on_fail_step_id = 0,
    @retry_attempts = 2,
    @retry_interval = 5,
    @os_run_priority = 0,
    @subsystem = N'TSQL',
    @command = N':r /maintenance/04-data-cleanup.sql',
    @database_name = N'ChillerPlantDB',
    @flags = 0;

EXEC sp_add_jobschedule
    @job_id = @JobID,
    @name = N'Daily_0400',
    @enabled = 1,
    @freq_type = 4,
    @freq_interval = 1,
    @freq_subday_type = 1,
    @freq_subday_interval = 0,
    @freq_relative_interval = 0,
    @freq_recurrence_factor = 1,
    @active_start_date = 20240101,
    @active_end_date = 99991231,
    @active_start_time = 40000,
    @active_end_time = 235959;

EXEC sp_add_jobserver
    @job_id = @JobID,
    @server_name = N'(LOCAL)';

PRINT 'Created job: ChillerPlant_Data_Cleanup (Daily 04:00)';
GO

PRINT '========================================';
PRINT '  All SQL Server Agent Jobs Created';
PRINT '  ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '========================================';
GO
