@echo off
echo ========================================
echo  BACnet/IP 设备模拟器启动
echo ========================================
echo.

cd BACnetSimulator

echo [1/3] 检查 Python 环境...
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
echo [2/3] 检查依赖包...
python -c "import requests" >nul 2>&1
if errorlevel 1 (
    echo 正在安装 requests 库...
    pip install requests
)
python -c "import numpy" >nul 2>&1
if errorlevel 1 (
    echo 正在安装 numpy 库...
    pip install numpy
)
echo ✓ 依赖包检查完成

echo.
echo [3/3] 启动 BACnet/IP 模拟器...
echo 模拟设备: 3台离心式冷水机组、2台螺杆式冷水机组
echo           8台冷却塔、12台冷冻水泵、12台冷却水泵
echo 上报间隔: 30秒
echo 目标API: http://localhost:5000/api/devices/data
echo.
echo 按 Ctrl+C 停止模拟器
echo ========================================
echo.

python bacnet_simulator.py
