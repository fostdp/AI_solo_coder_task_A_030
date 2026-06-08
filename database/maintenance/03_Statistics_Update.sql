-- =============================================
-- 统计信息更新脚本
-- 建议每天凌晨1:00执行
-- =============================================

USE [ChillerPlantDB]
GO

SET NOCOUNT ON
GO

PRINT N'============================================='
PRINT N'统计信息更新开始: ' + CONVERT(NVARCHAR, GETDATE(), 120)
PRINT N'============================================='

-- 更新所有表的统计信息，使用全表扫描（FULLSCAN）
-- 对于大表（>100万行）使用采样10%
DECLARE @TableName sysname
DECLARE @SchemaName sysname
DECLARE @RowCount BIGINT
DECLARE @Command NVARCHAR(MAX)

DECLARE TableCursor CURSOR FOR
SELECT 
    s.name AS SchemaName,
    t.name AS TableName,
    SUM(p.rows) AS RowCount
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
INNER JOIN sys.partitions p ON t.object_id = p.object_id
WHERE p.index_id IN (0, 1)
GROUP BY s.name, t.name
ORDER BY RowCount DESC

OPEN TableCursor
FETCH NEXT FROM TableCursor INTO @SchemaName, @TableName, @RowCount

WHILE @@FETCH_STATUS = 0
BEGIN
    PRINT N'处理表: [' + @SchemaName + '].[' + @TableName + N'], 行数: ' + CONVERT(NVARCHAR(20), @RowCount)

    IF @RowCount > 1000000
    BEGIN
        SET @Command = N'UPDATE STATISTICS [' + @SchemaName + '].[' + @TableName + N'] WITH SAMPLE 10 PERCENT'
        PRINT N'  执行: UPDATE STATISTICS ... WITH SAMPLE 10 PERCENT'
    END
    ELSE
    BEGIN
        SET @Command = N'UPDATE STATISTICS [' + @SchemaName + '].[' + @TableName + N'] WITH FULLSCAN'
        PRINT N'  执行: UPDATE STATISTICS ... WITH FULLSCAN'
    END

    BEGIN TRY
        EXEC sp_executesql @Command
        PRINT N'  完成'
    END TRY
    BEGIN CATCH
        PRINT N'  错误: ' + ERROR_MESSAGE()
    END CATCH

    PRINT N''
    FETCH NEXT FROM TableCursor INTO @SchemaName, @TableName, @RowCount
END

CLOSE TableCursor
DEALLOCATE TableCursor

PRINT N'============================================='
PRINT N'统计信息更新完成: ' + CONVERT(NVARCHAR, GETDATE(), 120)
PRINT N'============================================='
GO
