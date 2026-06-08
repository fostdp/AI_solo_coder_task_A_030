-- =============================================
-- 智能索引维护脚本
-- 根据碎片程度自动选择重组或重建
-- 建议每周日凌晨2:00执行
-- =============================================

USE [ChillerPlantDB]
GO

SET NOCOUNT ON
SET QUOTED_IDENTIFIER ON
GO

DECLARE @TableSchema sysname
DECLARE @TableName sysname
DECLARE @IndexName sysname
DECLARE @FragmentationPercent FLOAT
DECLARE @Command NVARCHAR(MAX)
DECLARE @PageCount INT

PRINT N'============================================='
PRINT N'索引维护开始: ' + CONVERT(NVARCHAR, GETDATE(), 120)
PRINT N'============================================='

-- 游标遍历所有碎片大于5%的索引
DECLARE IndexCursor CURSOR FOR
SELECT 
    s.name AS SchemaName,
    t.name AS TableName,
    i.name AS IndexName,
    ips.avg_fragmentation_in_percent AS FragmentationPercent,
    ips.page_count AS PageCount
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
INNER JOIN sys.indexes i ON ips.object_id = i.object_id AND ips.index_id = i.index_id
INNER JOIN sys.tables t ON ips.object_id = t.object_id
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE ips.avg_fragmentation_in_percent > 5.0
    AND ips.page_count > 100
    AND i.name IS NOT NULL
    AND i.type_desc IN ('CLUSTERED', 'NONCLUSTERED')
ORDER BY ips.avg_fragmentation_in_percent DESC

OPEN IndexCursor
FETCH NEXT FROM IndexCursor INTO @TableSchema, @TableName, @IndexName, @FragmentationPercent, @PageCount

WHILE @@FETCH_STATUS = 0
BEGIN
    PRINT N'处理索引: [' + @TableSchema + '].[' + @TableName + '].[' + @IndexName + N']'
    PRINT N'  碎片率: ' + CONVERT(NVARCHAR(20), @FragmentationPercent) + N'%, 页数: ' + CONVERT(NVARCHAR(20), @PageCount)

    -- 碎片率 > 30%: 重建索引 (REBUILD)
    -- 碎片率 5-30%: 重组索引 (REORGANIZE)
    IF @FragmentationPercent >= 30.0
    BEGIN
        SET @Command = N'ALTER INDEX [' + @IndexName + N'] ON [' + @TableSchema + '].[' + @TableName + N'] REBUILD WITH (ONLINE = OFF, SORT_IN_TEMPDB = ON, FILLFACTOR = 90, MAXDOP = 4)'
        PRINT N'  执行: REBUILD'
    END
    ELSE IF @FragmentationPercent >= 5.0
    BEGIN
        SET @Command = N'ALTER INDEX [' + @IndexName + N'] ON [' + @TableSchema + '].[' + @TableName + N'] REORGANIZE'
        PRINT N'  执行: REORGANIZE'
    END

    BEGIN TRY
        EXEC sp_executesql @Command
        PRINT N'  完成'
    END TRY
    BEGIN CATCH
        PRINT N'  错误: ' + ERROR_MESSAGE()
    END CATCH

    PRINT N''
    FETCH NEXT FROM IndexCursor INTO @TableSchema, @TableName, @IndexName, @FragmentationPercent, @PageCount
END

CLOSE IndexCursor
DEALLOCATE IndexCursor

PRINT N'============================================='
PRINT N'索引维护完成: ' + CONVERT(NVARCHAR, GETDATE(), 120)
PRINT N'============================================='
GO
