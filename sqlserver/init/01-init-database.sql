-- ========================================
--  Chiller Plant System - Database Initialization
-- ========================================

SET NOCOUNT ON;
GO

PRINT '========================================';
PRINT '  Initializing ChillerPlantDB Database';
PRINT '========================================';
GO

-- Create database if not exists
IF DB_ID('ChillerPlantDB') IS NULL
BEGIN
    CREATE DATABASE [ChillerPlantDB]
    ON PRIMARY
    (
        NAME = N'ChillerPlantDB_Data',
        FILENAME = N'/var/opt/mssql/data/ChillerPlantDB.mdf',
        SIZE = 1024 MB,
        MAXSIZE = UNLIMITED,
        FILEGROWTH = 256 MB
    )
    LOG ON
    (
        NAME = N'ChillerPlantDB_Log',
        FILENAME = N'/var/opt/mssql/data/ChillerPlantDB.ldf',
        SIZE = 512 MB,
        MAXSIZE = UNLIMITED,
        FILEGROWTH = 128 MB
    );
    PRINT 'Database ChillerPlantDB created';
END
ELSE
BEGIN
    PRINT 'Database ChillerPlantDB already exists';
END
GO

ALTER DATABASE [ChillerPlantDB] SET COMPATIBILITY_LEVEL = 160;
GO

ALTER DATABASE [ChillerPlantDB] SET AUTO_SHRINK OFF;
GO

ALTER DATABASE [ChillerPlantDB] SET AUTO_UPDATE_STATISTICS ON;
GO

ALTER DATABASE [ChillerPlantDB] SET AUTO_UPDATE_STATISTICS_ASYNC ON;
GO

ALTER DATABASE [ChillerPlantDB] SET RECOVERY SIMPLE;
GO

ALTER DATABASE [ChillerPlantDB] SET PAGE_VERIFY CHECKSUM;
GO

USE [ChillerPlantDB];
GO

PRINT 'Database configuration applied';
GO
