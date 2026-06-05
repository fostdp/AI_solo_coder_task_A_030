-- ========================================
--  Database Backup Script
-- ========================================

SET NOCOUNT ON;
GO

USE [master];
GO

PRINT '========================================';
PRINT '  Starting Database Backup';
PRINT '  ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '========================================';
GO

DECLARE @DatabaseName NVARCHAR(255) = N'ChillerPlantDB';
DECLARE @BackupPath NVARCHAR(255) = N'/var/opt/mssql/backups/';
DECLARE @BackupFileName NVARCHAR(255);
DECLARE @BackupType NVARCHAR(10);
DECLARE @SQL NVARCHAR(MAX);
DECLARE @DayOfWeek INT = DATEPART(WEEKDAY, GETDATE());

EXEC master.dbo.xp_create_subdir @BackupPath;

IF @DayOfWeek = 1
BEGIN
    SET @BackupType = 'FULL';
    SET @BackupFileName = @DatabaseName + '_FULL_' + 
                         REPLACE(CONVERT(VARCHAR, GETDATE(), 112) + '_' + 
                         REPLACE(CONVERT(VARCHAR, GETDATE(), 108), ':', ''), '.', '') + '.bak';
    
    SET @SQL = N'BACKUP DATABASE ' + QUOTENAME(@DatabaseName) + 
               N' TO DISK = N''' + @BackupPath + @BackupFileName + N''' ' +
               N'WITH COMPRESSION, STATS = 10, CHECKSUM, FORMAT';
    
    PRINT 'Performing FULL backup: ' + @BackupFileName;
    
    EXEC sp_executesql @SQL;
    
    PRINT 'FULL backup completed successfully';
END
ELSE
BEGIN
    SET @BackupType = 'DIFF';
    SET @BackupFileName = @DatabaseName + '_DIFF_' + 
                         REPLACE(CONVERT(VARCHAR, GETDATE(), 112) + '_' + 
                         REPLACE(CONVERT(VARCHAR, GETDATE(), 108), ':', ''), '.', '') + '.bak';
    
    SET @SQL = N'BACKUP DATABASE ' + QUOTENAME(@DatabaseName) + 
               N' TO DISK = N''' + @BackupPath + @BackupFileName + N''' ' +
               N'WITH COMPRESSION, STATS = 10, CHECKSUM, DIFFERENTIAL';
    
    PRINT 'Performing DIFFERENTIAL backup: ' + @BackupFileName;
    
    EXEC sp_executesql @SQL;
    
    PRINT 'DIFFERENTIAL backup completed successfully';
END

DECLARE @LogBackupFileName NVARCHAR(255);
SET @LogBackupFileName = @DatabaseName + '_LOG_' + 
                         REPLACE(CONVERT(VARCHAR, GETDATE(), 112) + '_' + 
                         REPLACE(CONVERT(VARCHAR, GETDATE(), 108), ':', ''), '.', '') + '.trn';

SET @SQL = N'BACKUP LOG ' + QUOTENAME(@DatabaseName) + 
           N' TO DISK = N''' + @BackupPath + @LogBackupFileName + N''' ' +
           N'WITH COMPRESSION, STATS = 10, CHECKSUM';

PRINT 'Performing LOG backup: ' + @LogBackupFileName;
EXEC sp_executesql @SQL;
PRINT 'LOG backup completed successfully';

DECLARE @DeleteDate DATETIME = DATEADD(day, -7, GETDATE());
SET @SQL = N'DECLARE @DeleteDate DATETIME = DATEADD(day, -7, GETDATE()); ' +
           N'EXEC master.dbo.xp_delete_file 0, N''' + @BackupPath + N''', N''.bak'', @DeleteDate, 1; ' +
           N'EXEC master.dbo.xp_delete_file 0, N''' + @BackupPath + N''', N''.trn'', @DeleteDate, 1;';

PRINT 'Deleting backups older than 7 days...';
EXEC sp_executesql @SQL;
PRINT 'Old backups deleted successfully';

PRINT '========================================';
PRINT '  Database Backup Completed';
PRINT '  ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '========================================';
GO
