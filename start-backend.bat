@echo off
echo ========================================
echo  智能建筑中央空调冷站群控系统 - 后端启动
echo ========================================
echo.

cd Backend

echo [1/3] 检查 .NET 8 SDK...
dotnet --list-sdks | findstr /C:"8.0" >nul
if errorlevel 1 (
    echo 错误: 未找到 .NET 8 SDK，请先安装 .NET 8 SDK
    echo 下载地址: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)
echo ✓ .NET 8 SDK 已安装

echo.
echo [2/3] 还原依赖包...
dotnet restore
if errorlevel 1 (
    echo 错误: 依赖包还原失败
    pause
    exit /b 1
)
echo ✓ 依赖包还原完成

echo.
echo [3/3] 启动后端服务...
echo 服务地址: http://localhost:5000
echo Swagger文档: http://localhost:5000/swagger
echo SignalR Hub: http://localhost:5000/realtimeHub
echo.
echo 按 Ctrl+C 停止服务
echo ========================================
echo.

dotnet run --urls "http://localhost:5000"
