# 智能建筑中央空调冷站群控系统 - 一键启动脚本
# 使用说明: 右键 -> 使用PowerShell运行

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " 智能建筑中央空调冷站群控系统 - 一键启动" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

# 检查管理员权限
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "提示: 建议以管理员身份运行此脚本" -ForegroundColor Yellow
    Write-Host ""
}

# 启动后端服务
Write-Host "[1/3] 启动后端服务..." -ForegroundColor Green
$backendJob = Start-Job -ScriptBlock {
    param($workDir)
    Set-Location "$workDir\Backend"
    dotnet run --urls "http://localhost:5000"
} -ArgumentList $scriptPath

# 等待后端启动
Write-Host "  等待后端服务启动 (15秒)..." -ForegroundColor Gray
Start-Sleep -Seconds 15

# 启动前端服务
Write-Host "[2/3] 启动前端服务..." -ForegroundColor Green
$frontendJob = Start-Job -ScriptBlock {
    param($workDir)
    Set-Location "$workDir\frontend"
    python -m http.server 8080
} -ArgumentList $scriptPath

# 启动BACnet模拟器
Write-Host "[3/3] 启动BACnet设备模拟器..." -ForegroundColor Green
$simulatorJob = Start-Job -ScriptBlock {
    param($workDir)
    Set-Location "$workDir\BACnetSimulator"
    python bacnet_simulator.py
} -ArgumentList $scriptPath

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " 所有服务启动完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host " 后端API:     http://localhost:5000" -ForegroundColor White
Write-Host " Swagger文档: http://localhost:5000/swagger" -ForegroundColor White
Write-Host " 前端界面:   http://localhost:8080" -ForegroundColor White
Write-Host ""
Write-Host " 后端任务ID: $($backendJob.Id)" -ForegroundColor Gray
Write-Host " 前端任务ID: $($frontendJob.Id)" -ForegroundColor Gray
Write-Host " 模拟器任务ID: $($simulatorJob.Id)" -ForegroundColor Gray
Write-Host ""
Write-Host " 按 Ctrl+C 停止所有服务..." -ForegroundColor Yellow
Write-Host " 或运行 .\stop-all.ps1 停止服务" -ForegroundColor Yellow
Write-Host ""

# 等待用户中断
try {
    while ($true) {
        # 检查任务状态
        $allRunning = ($backendJob.State -eq "Running" -and $frontendJob.State -eq "Running" -and $simulatorJob.State -eq "Running")
        if (-not $allRunning) {
            Write-Host ""
            Write-Host "警告: 部分服务已停止" -ForegroundColor Red
            if ($backendJob.State -ne "Running") { Write-Host "  后端服务已停止" -ForegroundColor Red }
            if ($frontendJob.State -ne "Running") { Write-Host "  前端服务已停止" -ForegroundColor Red }
            if ($simulatorJob.State -ne "Running") { Write-Host "  模拟器已停止" -ForegroundColor Red }
            break
        }
        Start-Sleep -Seconds 5
    }
} finally {
    Write-Host ""
    Write-Host "正在停止所有服务..." -ForegroundColor Yellow
    Stop-Job -Id $backendJob.Id -Force
    Stop-Job -Id $frontendJob.Id -Force
    Stop-Job -Id $simulatorJob.Id -Force
    Remove-Job -Id $backendJob.Id -Force
    Remove-Job -Id $frontendJob.Id -Force
    Remove-Job -Id $simulatorJob.Id -Force
    Write-Host "所有服务已停止" -ForegroundColor Green
}
