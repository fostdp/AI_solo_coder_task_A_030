-- ========================================
--  SQL Server Index Optimization Script
-- ========================================

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

USE [ChillerPlantDB];
GO

DECLARE @TableName NVARCHAR(255);
DECLARE @IndexName NVARCHAR(255);
DECLARE @SchemaName NVARCHAR(255);
DECLARE @Fragmentation FLOAT;
DECLARE @SQL NVARCHAR(MAX);
DECLARE @PageCount INT;

PRINT '========================================';
PRINT '  Starting Index Optimization';
PRINT '  ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '========================================';
GO

-- Create optimization log table if not exists
IF OBJECT_ID('dbo.IndexOptimizationLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.IndexOptimizationLog
    (
        LogID INT IDENTITY(1,1) PRIMARY KEY,
        SchemaName NVARCHAR(255),
        TableName NVARCHAR(255),
        IndexName NVARCHAR(255),
        FragmentationBefore FLOAT,
        FragmentationAfter FLOAT,
        PageCount INT,
        ActionTaken NVARCHAR(50),
        StartTime DATETIME,
        EndTime DATETIME,
        ErrorMessage NVARCHAR(MAX)
    );
    PRINT 'Created IndexOptimizationLog table';
END
GO

-- Cursor for indexes with fragmentation > 5%
DECLARE index_cursor CURSOR FOR
SELECT 
    s.name AS SchemaName,
    t.name AS TableName,
    i.name AS IndexName,
    ps.avg_fragmentation_in_percent,
    ps.page_count
FROM 
    sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'SAMPLED') ps
INNER JOIN 
    sys.indexes i ON ps.object_id = i.object_id AND ps.index_id = i.index_id
INNER JOIN 
    sys.tables t ON i.object_id = t.object_id
INNER JOIN 
    sys.schemas s ON t.schema_id = s.schema_id
WHERE 
    ps.avg_fragmentation_in_percent > 5
    AND ps.page_count > 100
    AND i.index_id > 0
ORDER BY 
    ps.avg_fragmentation_in_percent DESC;

OPEN index_cursor;

FETCH NEXT FROM index_cursor INTO @SchemaName, @TableName, @IndexName, @Fragmentation, @PageCount;

WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @StartTime DATETIME = GETDATE();
    DECLARE @Action NVARCHAR(50);
    DECLARE @ErrorMessage NVARCHAR(MAX) = NULL;
    DECLARE @NewFragmentation FLOAT;

    BEGIN TRY
        IF @Fragmentation >= 30
        BEGIN
            SET @Action = 'REBUILD';
            SET @SQL = N'ALTER INDEX ' + QUOTENAME(@IndexName) + 
                       N' ON ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName) + 
                       N' REBUILD WITH (ONLINE = ON, SORT_IN_TEMPDB = ON, MAXDOP = 0)';
            
            PRINT 'Rebuilding index: ' + QUOTENAME(@SchemaName) + '.' + QUOTENAME(@TableName) + '.' + QUOTENAME(@IndexName) + 
                  ' (Fragmentation: ' + CAST(@Fragmentation AS VARCHAR(10)) + '%)';
        END
        ELSE
        BEGIN
            SET @Action = 'REORGANIZE';
            SET @SQL = N'ALTER INDEX ' + QUOTENAME(@IndexName) + 
                       N' ON ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName) + 
                       N' REORGANIZE';
            
            PRINT 'Reorganizing index: ' + QUOTENAME(@SchemaName) + '.' + QUOTENAME(@TableName) + '.' + QUOTENAME(@IndexName) + 
                  ' (Fragmentation: ' + CAST(@Fragmentation AS VARCHAR(10)) + '%)';
        END

        EXEC sp_executesql @SQL;

        SELECT @NewFragmentation = avg_fragmentation_in_percent
        FROM sys.dm_db_index_physical_stats(DB_ID(), OBJECT_ID(@SchemaName + '.' + @TableName), NULL, NULL, 'LIMITED')
        WHERE index_id = (SELECT index_id FROM sys.indexes WHERE name = @IndexName AND object_id = OBJECT_ID(@SchemaName + '.' + @TableName));

        INSERT INTO dbo.IndexOptimizationLog
        (SchemaName, TableName, IndexName, FragmentationBefore, FragmentationAfter, PageCount, ActionTaken, StartTime, EndTime)
        VALUES
        (@SchemaName, @TableName, @IndexName, @Fragmentation, @NewFragmentation, @PageCount, @Action, @StartTime, GETDATE());

        PRINT '  Completed: ' + @Action + 
              ', Fragmentation: ' + CAST(@Fragmentation AS VARCHAR(10)) + '% -> ' + CAST(@NewFragmentation AS VARCHAR(10)) + '%';

    END TRY
    BEGIN CATCH
        SET @ErrorMessage = ERROR_MESSAGE();
        
        INSERT INTO dbo.IndexOptimizationLog
        (SchemaName, TableName, IndexName, FragmentationBefore, FragmentationAfter, PageCount, ActionTaken, StartTime, EndTime, ErrorMessage)
        VALUES
        (@SchemaName, @TableName, @IndexName, @Fragmentation, NULL, @PageCount, @Action, @StartTime, GETDATE(), @ErrorMessage);
        
        PRINT '  Error: ' + @ErrorMessage;
    END CATCH

    FETCH NEXT FROM index_cursor INTO @SchemaName, @TableName, @IndexName, @Fragmentation, @PageCount;
END

CLOSE index_cursor;
DEALLOCATE index_cursor;
GO

PRINT '========================================';
PRINT '  Index Optimization Completed';
PRINT '  ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '========================================';
GO
