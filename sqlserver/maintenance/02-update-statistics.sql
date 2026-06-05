-- ========================================
--  Update Statistics Script
-- ========================================

SET NOCOUNT ON;
GO

USE [ChillerPlantDB];
GO

PRINT '========================================';
PRINT '  Starting Statistics Update';
PRINT '  ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '========================================';
GO

DECLARE @TableName NVARCHAR(255);
DECLARE @SchemaName NVARCHAR(255);
DECLARE @SQL NVARCHAR(MAX);
DECLARE @RowCount INT;

DECLARE stats_cursor CURSOR FOR
SELECT 
    s.name AS SchemaName,
    t.name AS TableName,
    SUM(p.rows) AS RowCount
FROM 
    sys.tables t
INNER JOIN 
    sys.schemas s ON t.schema_id = s.schema_id
INNER JOIN 
    sys.partitions p ON t.object_id = p.object_id
WHERE 
    p.index_id IN (0, 1)
    AND p.rows > 1000
GROUP BY 
    s.name, t.name
ORDER BY 
    SUM(p.rows) DESC;

OPEN stats_cursor;

FETCH NEXT FROM stats_cursor INTO @SchemaName, @TableName, @RowCount;

WHILE @@FETCH_STATUS = 0
BEGIN
    BEGIN TRY
        SET @SQL = N'UPDATE STATISTICS ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName) + 
                   N' WITH FULLSCAN, MAXDOP = 0';
        
        PRINT 'Updating statistics for: ' + QUOTENAME(@SchemaName) + '.' + QUOTENAME(@TableName) + 
              ' (Rows: ' + CAST(@RowCount AS VARCHAR(20)) + ')';
        
        EXEC sp_executesql @SQL;
        
        PRINT '  Statistics updated successfully';
    END TRY
    BEGIN CATCH
        PRINT '  Error: ' + ERROR_MESSAGE();
    END CATCH

    FETCH NEXT FROM stats_cursor INTO @SchemaName, @TableName, @RowCount;
END

CLOSE stats_cursor;
DEALLOCATE stats_cursor;
GO

PRINT '========================================';
PRINT '  Statistics Update Completed';
PRINT '  ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '========================================';
GO
