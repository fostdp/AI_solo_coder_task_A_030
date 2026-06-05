@echo off
echo ========================================
echo  智能建筑中央空调冷站群控系统 - 前端启动
echo ========================================
echo.

cd frontend

echo [1/2] 检查 Python 环境...
python --version >nul 2>&1
if errorlevel 1 (
    echo 错误: 未找到 Python，请先安装 Python 3.x
    echo 下载地址: https://www.python.org/downloads/
    pause
    exit /b 1
)
for /f "tokens=2" %%i in ('python --version 2^>^&1') do set PY_VER=%%i
echo ✓ Python %PY_VER% 已安装

echo.
echo [2/2] 启动前端静态文件服务器...
echo 前端地址: http://localhost:8080
echo.
echo 按 Ctrl+C 停止服务
echo ========================================
echo.

python -m http.server 8080
