@echo off
echo ========================================
echo  智能建筑中央空调冷站群控系统 - 回归测试
echo ========================================
echo.

echo [1/5] 检查 .NET 8 SDK...
dotnet --list-sdks | findstr /C:"8.0" >nul
if errorlevel 1 (
    echo 错误: 未找到 .NET 8 SDK，请先安装 .NET 8 SDK
    echo 下载地址: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)
echo ✓ .NET 8 SDK 已安装

echo.
echo [2/5] 还原解决方案依赖包...
dotnet restore ChillerPlant.sln
if errorlevel 1 (
    echo 错误: 依赖包还原失败
    pause
    exit /b 1
)
echo ✓ 依赖包还原完成

echo.
echo [3/5] 编译解决方案...
dotnet build ChillerPlant.sln --no-restore --configuration Debug
if errorlevel 1 (
    echo 错误: 编译失败
    pause
    exit /b 1
)
echo ✓ 编译成功

echo.
echo [4/5] 运行回归测试...
echo.
echo ----------------------------------------
echo  测试概述
echo ----------------------------------------
echo  - BacnetGateway 模块测试: 11 个测试用例
echo  - EfficiencyOptimizer 模块测试: 11 个测试用例
echo  - AlarmManager 模块测试: 14 个测试用例
echo  - 集成测试: 10 个测试用例
echo  总计: 46 个测试用例
echo ----------------------------------------
echo.

dotnet test ChillerPlant.sln --no-build --configuration Debug --logger "console;verbosity=normal" --logger "trx;LogFileName=TestResults.trx"

if errorlevel 1 (
    echo.
    echo [错误] 部分测试失败，请查看上方日志
    echo 测试结果已保存到: TestResults\TestResults.trx
    pause
    exit /b 1
)

echo.
echo ========================================
echo  ✓ 所有测试通过！
echo ========================================
echo.
echo 测试结果已保存到: TestResults\TestResults.trx
echo.
pause
