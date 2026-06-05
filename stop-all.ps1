# 停止所有服务脚本

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " 停止冷站群控系统所有服务" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 停止所有相关的后台任务
$jobs = Get-Job | Where-Object { $_.Name -like "*Chiller*" -or $_.Command -like "*dotnet run*" -or $_.Command -like "*python*" }

if ($jobs.Count -eq 0) {
    Write-Host "未找到运行中的服务任务" -ForegroundColor Yellow
} else {
    Write-Host "找到 $($jobs.Count) 个运行中的任务" -ForegroundColor Green
    foreach ($job in $jobs) {
        Write-Host "  停止任务 $($job.Id)..." -ForegroundColor Gray
        Stop-Job -Id $job.Id -Force
        Remove-Job -Id $job.Id -Force
    }
    Write-Host "所有任务已停止" -ForegroundColor Green
}

Write-Host ""

# 停止相关进程
$processes = @(
    @{ Name = "dotnet"; Port = 5000 },
    @{ Name = "python"; Port = 8080 }
)

foreach ($proc in $processes) {
    $netstatOutput = netstat -ano | findstr ":$($proc.Port)"
    if ($netstatOutput) {
        $lines = $netstatOutput -split "`n"
        foreach ($line in $lines) {
            if ($line -match ':(\d+)\s+\S+\s+LISTENING\s+(\d+)') {
                $port = $matches[1]
                $pid = $matches[2]
                if ($port -eq $proc.Port) {
                    try {
                        $process = Get-Process -Id $pid -ErrorAction SilentlyContinue
                        if ($process -and $process.ProcessName -eq $proc.Name) {
                            Write-Host "停止 $($proc.Name) 进程 (PID: $pid, 端口: $port)..." -ForegroundColor Yellow
                            Stop-Process -Id $pid -Force
                            Write-Host "  ✓ 已停止" -ForegroundColor Green
                        }
                    } catch {
                        Write-Host "  无法停止进程 $pid" -ForegroundColor Red
                    }
                }
            }
        }
    }
}

# 检查Python模拟器进程
$pythonProcs = Get-Process python -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like "*bacnet*" -or $_.Path -like "*simulator*" }
foreach ($proc in $pythonProcs) {
    Write-Host "停止 BACnet模拟器进程 (PID: $($proc.Id)..." -ForegroundColor Yellow
    Stop-Process -Id $proc.Id -Force
    Write-Host "  ✓ 已停止" -ForegroundColor Green
}

Write-Host ""
Write-Host "停止完成" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
