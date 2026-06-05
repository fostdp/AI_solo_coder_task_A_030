-- ========================================
--  Historical Data Cleanup Script
-- ========================================

SET NOCOUNT ON;
GO

USE [ChillerPlantDB];
GO

PRINT '========================================';
PRINT '  Starting Historical Data Cleanup';
PRINT '  ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '========================================';
GO

DECLARE @RetentionDays INT = 90;
DECLARE @CutoffDate DATETIME = DATEADD(day, -@RetentionDays, GETDATE());
DECLARE @DeletedCount INT = 0;
DECLARE @TotalDeleted INT = 0;

-- Create cleanup log table if not exists
IF OBJECT_ID('dbo.DataCleanupLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DataCleanupLog
    (
        LogID INT IDENTITY(1,1) PRIMARY KEY,
        TableName NVARCHAR(255),
        RecordsDeleted INT,
        CutoffDate DATETIME,
        CleanupDate DATETIME DEFAULT GETDATE()
    );
    PRINT 'Created DataCleanupLog table';
END
GO

DECLARE @TableName NVARCHAR(255);
DECLARE @SQL NVARCHAR(MAX);

-- Table cleanup configuration
DECLARE cleanup_tables CURSOR FOR
SELECT table_name FROM (
    VALUES 
        ('DeviceDatas', 'Timestamp'),
        ('SystemEfficiencies', 'Timestamp'),
        ('OptimizationRecommendations', 'GeneratedAt'),
        ('AlarmWorkOrders', 'CreatedAt'),
        ('AlarmLogs', 'CreatedAt')
) AS t(table_name, date_column)
WHERE EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_NAME = t.table_name AND TABLE_SCHEMA = 'dbo'
);

OPEN cleanup_tables;
FETCH NEXT FROM cleanup_tables INTO @TableName;

WHILE @@FETCH_STATUS = 0
BEGIN
    BEGIN TRY
        DECLARE @DateColumn NVARCHAR(255);
        SELECT @DateColumn = date_column 
        FROM (VALUES 
            ('DeviceDatas', 'Timestamp'),
            ('SystemEfficiencies', 'Timestamp'),
            ('OptimizationRecommendations', 'GeneratedAt'),
            ('AlarmWorkOrders', 'CreatedAt'),
            ('AlarmLogs', 'CreatedAt')
        ) AS t(table_name, date_column)
        WHERE t.table_name = @TableName;

        SET @SQL = N'DELETE TOP (1000) FROM dbo.' + QUOTENAME(@TableName) + 
                   N' WHERE ' + QUOTENAME(@DateColumn) + N' < @CutoffDate';
        
        SET @DeletedCount = 1;
        SET @TotalDeleted = 0;
        
        WHILE @DeletedCount > 0
        BEGIN
            EXEC sp_executesql @SQL, N'@CutoffDate DATETIME', @CutoffDate;
            SET @DeletedCount = @@ROWCOUNT;
            SET @TotalDeleted += @DeletedCount;
            
            IF @DeletedCount > 0
            BEGIN
                WAITFOR DELAY '00:00:01';
            END
        END
        
        INSERT INTO dbo.DataCleanupLog (TableName, RecordsDeleted, CutoffDate)
        VALUES (@TableName, @TotalDeleted, @CutoffDate);
        
        PRINT 'Cleaned up ' + CAST(@TotalDeleted AS VARCHAR(20)) + ' records from ' + QUOTENAME(@TableName);
    END TRY
    BEGIN CATCH
        PRINT 'Error cleaning ' + QUOTENAME(@TableName) + ': ' + ERROR_MESSAGE();
    END CATCH

    FETCH NEXT FROM cleanup_tables INTO @TableName;
END

CLOSE cleanup_tables;
DEALLOCATE cleanup_tables;
GO

PRINT '========================================';
PRINT '  Historical Data Cleanup Completed';
PRINT '  ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '========================================';
GO
