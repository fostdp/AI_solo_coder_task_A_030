@echo off
setlocal enabledelayedexpansion

echo ========================================
echo  Chiller Plant System - Deploy Script
echo ========================================
echo.

REM Check Docker
docker --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Docker is not installed or not running
    echo Please install Docker Desktop and start it first
    pause
    exit /b 1
)
echo [OK] Docker is running

REM Check Docker Compose
docker compose version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Docker Compose is not available
    pause
    exit /b 1
)
echo [OK] Docker Compose is available

echo.
echo [1/5] Checking environment configuration...
if not exist .env (
    echo.
    echo .env file not found, creating from .env.example...
    copy .env.example .env >nul
    echo [OK] .env file created, please edit it if needed
) else (
    echo [OK] .env file exists
)

echo.
echo [2/5] Creating required directories...
if not exist "sqlserver\init" mkdir sqlserver\init
if not exist "sqlserver\maintenance" mkdir sqlserver\maintenance
if not exist "nginx\conf.d" mkdir nginx\conf.d
if not exist "nginx\certs" mkdir nginx\certs
echo [OK] Directories created

echo.
echo [3/5] Stopping existing containers...
docker compose down 2>nul
echo [OK] Existing containers stopped

echo.
echo [4/5] Building and starting containers...
echo This may take several minutes for the first build...
echo.

docker compose up -d --build
if errorlevel 1 (
    echo.
    echo [ERROR] Failed to start containers
    echo Please check the error messages above
    pause
    exit /b 1
)

echo.
echo [5/5] Waiting for services to be healthy...
echo.

set /a max_attempts=30
set /a attempt=0

:wait_loop
set /a attempt+=1
if %attempt% gtr %max_attempts% (
    echo [WARNING] Timeout waiting for services to be healthy
    echo Please check container logs: docker compose logs
    goto show_urls
)

echo Attempt %attempt%/%max_attempts%: Checking service health...

for /f "tokens=*" %%i in ('docker compose ps --format "{{.Name}} {{.Status}}" ^| findstr "healthy"') do (
    echo   %%i
)

docker compose ps --format "{{.Name}} {{.Status}}" | findstr /c:"unhealthy" /c:"starting" >nul
if %errorlevel% equ 0 (
    timeout /t 5 >nul
    goto wait_loop
)

echo.
echo [OK] All services are healthy!

:show_urls
echo.
echo ========================================
echo  Deployment Complete!
echo ========================================
echo.
echo  Service URLs:
echo  - Frontend:        http://localhost:8080
echo  - Backend API:     http://localhost:5000
echo  - Swagger UI:      http://localhost:5000/swagger
echo  - Health Check:    http://localhost:5000/health
echo.
echo  Useful Commands:
echo  - View logs:       docker compose logs -f
echo  - Stop services:   docker compose down
echo  - Restart:         docker compose restart
echo  - Status:          docker compose ps
echo.
echo  Simulator Configuration:
echo  - 3 centrifugal chillers (BACnet: 300001-300003)
echo  - 2 screw chillers (BACnet: 300004-300005)
echo  - 8 cooling towers (BACnet: 300006-300013)
echo  - 12 chilled pumps (BACnet: 300014-300025)
echo  - 12 cooling pumps (BACnet: 300026-300037)
echo  - Send interval: 30 seconds
echo.
pause
