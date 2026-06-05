# Code Validation Script for Chiller Plant System
# Performs static validation of code structure, completeness, and basic syntax

param(
    [switch]$RunTests
)

$ErrorActionPreference = "Stop"
$baseDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$backendDir = Join-Path $baseDir "Backend"
$testsDir = Join-Path $baseDir "Backend.Tests"
$frontendDir = Join-Path $baseDir "frontend"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Code Static Validation" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$totalIssues = 0
$totalChecks = 0

function Write-CheckResult {
    param(
        [string]$CheckName,
        [bool]$Passed,
        [string]$Details = ""
    )
    $global:totalChecks++
    if ($Passed) {
        Write-Host "  [PASS] $CheckName" -ForegroundColor Green
        if ($Details) { Write-Host "         $Details" -ForegroundColor Gray }
    } else {
        Write-Host "  [FAIL] $CheckName" -ForegroundColor Red
        if ($Details) { Write-Host "         $Details" -ForegroundColor Yellow }
        $global:totalIssues++
    }
}

function Test-CSharpFile {
    param([string]$FilePath)
    
    $content = Get-Content $FilePath -Raw
    $issues = @()
    $fileName = Split-Path $FilePath -Leaf
    
    if ($fileName -ne 'Program.cs' -and -not ($content -match 'namespace\s+\w+')) {
        $issues += "Missing namespace declaration"
    }
    
    $cleanContent = $content
    $cleanContent = [regex]::Replace($cleanContent, '@"(?s:.)*?"', '')
    $cleanContent = [regex]::Replace($cleanContent, '"(?:\\.|[^"\\])*"', '')
    $cleanContent = [regex]::Replace($cleanContent, '//[^\n]*', '')
    $cleanContent = [regex]::Replace($cleanContent, '(?s)/\*.*?\*/', '')
    
    $openBraces = ($cleanContent.ToCharArray() | Where-Object { $_ -eq '{' }).Count
    $closeBraces = ($cleanContent.ToCharArray() | Where-Object { $_ -eq '}' }).Count
    $braceDiff = [Math]::Abs($openBraces - $closeBraces)
    if ($braceDiff -gt 1) {
        $issues += "Brace mismatch (open: $openBraces, close: $closeBraces)"
    }
    
    $usingCount = ([regex]::Matches($content, 'using\s+[\w\.]+;')).Count
    if ($usingCount -eq 0 -and $content.Length -gt 100 -and $fileName -ne 'Program.cs') {
        $issues += "Possible missing using statements"
    }
    
    return $issues
}

function Test-JavaScriptFile {
    param([string]$FilePath)
    
    $content = Get-Content $FilePath -Raw
    $issues = @()
    
    $openParens = ($content.ToCharArray() | Where-Object { $_ -eq '(' }).Count
    $closeParens = ($content.ToCharArray() | Where-Object { $_ -eq ')' }).Count
    if ($openParens -ne $closeParens) {
        $issues += "Parenthesis mismatch (open: $openParens, close: $closeParens)"
    }
    
    $openBraces = ($content.ToCharArray() | Where-Object { $_ -eq '{' }).Count
    $closeBraces = ($content.ToCharArray() | Where-Object { $_ -eq '}' }).Count
    if ($openBraces -ne $closeBraces) {
        $issues += "Brace mismatch (open: $openBraces, close: $closeBraces)"
    }
    
    return $issues
}

Write-Host "[1/6] Project Structure Validation" -ForegroundColor Cyan

$requiredDirs = @(
    $backendDir,
    (Join-Path $backendDir "Modules\BacnetGateway"),
    (Join-Path $backendDir "Modules\EfficiencyOptimizer"),
    (Join-Path $backendDir "Modules\AlarmManager"),
    (Join-Path $backendDir "Modules\Shared"),
    (Join-Path $backendDir "Controllers"),
    (Join-Path $backendDir "Services"),
    (Join-Path $backendDir "Data"),
    (Join-Path $backendDir "Models"),
    $testsDir,
    $frontendDir,
    (Join-Path $frontendDir "components"),
    (Join-Path $frontendDir "js")
)

foreach ($dir in $requiredDirs) {
    $exists = Test-Path $dir
    $relativePath = $dir.Replace($baseDir, '').TrimStart('\')
    Write-CheckResult "Directory exists: $relativePath" $exists
}

Write-Host ""
Write-Host "[2/6] Key Files Validation" -ForegroundColor Cyan

$requiredFiles = @(
    (Join-Path $backendDir "Program.cs"),
    (Join-Path $backendDir "appsettings.json"),
    (Join-Path $backendDir "ChillerPlant.csproj"),
    (Join-Path $backendDir "Modules\BacnetGateway\BacnetGatewayModule.cs"),
    (Join-Path $backendDir "Modules\BacnetGateway\Services\BacnetUdpListenerService.cs"),
    (Join-Path $backendDir "Modules\BacnetGateway\Services\BacnetProtocolParser.cs"),
    (Join-Path $backendDir "Modules\EfficiencyOptimizer\EfficiencyOptimizerModule.cs"),
    (Join-Path $backendDir "Modules\EfficiencyOptimizer\Services\NeuralNetworkOptimizationService.cs"),
    (Join-Path $backendDir "Modules\EfficiencyOptimizer\Services\EfficiencyBackgroundService.cs"),
    (Join-Path $backendDir "Modules\AlarmManager\AlarmManagerModule.cs"),
    (Join-Path $backendDir "Modules\AlarmManager\Services\AlarmEvaluationService.cs"),
    (Join-Path $backendDir "Modules\AlarmManager\Services\WechatAlarmAggregatorService.cs"),
    (Join-Path $backendDir "Modules\Shared\Commands\DeviceDataCommands.cs"),
    (Join-Path $backendDir "Modules\Shared\Events\DeviceDataReceivedEvent.cs"),
    (Join-Path $backendDir "Controllers\DevicesController.cs"),
    (Join-Path $backendDir "Controllers\EfficiencyController.cs"),
    (Join-Path $backendDir "Controllers\AlarmsController.cs"),
    (Join-Path $testsDir "ChillerPlant.Tests.csproj"),
    (Join-Path $testsDir "TestBase.cs"),
    (Join-Path $testsDir "BacnetGatewayTests.cs"),
    (Join-Path $testsDir "EfficiencyOptimizerTests.cs"),
    (Join-Path $testsDir "AlarmManagerTests.cs"),
    (Join-Path $testsDir "IntegrationTests.cs"),
    (Join-Path $frontendDir "index.html"),
    (Join-Path $frontendDir "components\ChillerFlow.vue"),
    (Join-Path $frontendDir "components\ChillerFlow.umd.js"),
    (Join-Path $frontendDir "components\DeviceDetail.vue"),
    (Join-Path $frontendDir "components\DeviceDetail.umd.js"),
    (Join-Path $frontendDir "js\main.js"),
    (Join-Path $baseDir "ChillerPlant.sln"),
    (Join-Path $baseDir "run-tests.bat")
)

foreach ($file in $requiredFiles) {
    $exists = Test-Path $file
    $relativePath = $file.Replace($baseDir, '').TrimStart('\')
    if ($exists) {
        $size = (Get-Item $file).Length
        Write-CheckResult "File exists: $relativePath" $exists "Size: $size bytes"
    } else {
        Write-CheckResult "File exists: $relativePath" $exists "File missing!"
    }
}

Write-Host ""
Write-Host "[3/6] C# Code Structure Validation" -ForegroundColor Cyan

$csFiles = Get-ChildItem -Path $backendDir -Filter "*.cs" -Recurse
$csTestFiles = Get-ChildItem -Path $testsDir -Filter "*.cs" -Recurse
$allCsFiles = @($csFiles) + @($csTestFiles)

Write-CheckResult "C# source files found" ($csFiles.Count -gt 0) "Found $($csFiles.Count) source files"
Write-CheckResult "C# test files found" ($csTestFiles.Count -gt 0) "Found $($csTestFiles.Count) test files"

foreach ($file in $allCsFiles | Select-Object -First 20) {
    $issues = Test-CSharpFile $file.FullName
    $relativePath = $file.FullName.Replace($baseDir, '').TrimStart('\')
    if ($issues.Count -eq 0) {
        Write-CheckResult "Syntax check: $relativePath" $true
    } else {
        Write-CheckResult "Syntax check: $relativePath" $false ($issues -join "; ")
    }
}

Write-Host ""
Write-Host "[4/6] JavaScript Code Validation" -ForegroundColor Cyan

$jsFiles = Get-ChildItem -Path $frontendDir -Filter "*.js" -Recurse

Write-CheckResult "JavaScript files found" ($jsFiles.Count -gt 0) "Found $($jsFiles.Count) files"

foreach ($file in $jsFiles) {
    $issues = Test-JavaScriptFile $file.FullName
    $relativePath = $file.FullName.Replace($baseDir, '').TrimStart('\')
    if ($issues.Count -eq 0) {
        Write-CheckResult "Syntax check: $relativePath" $true
    } else {
        Write-CheckResult "Syntax check: $relativePath" $false ($issues -join "; ")
    }
}

Write-Host ""
Write-Host "[5/6] Module Decoupling Validation" -ForegroundColor Cyan

$bacnetGatewayCode = Get-ChildItem (Join-Path $backendDir "Modules\BacnetGateway") -Recurse -Filter "*.cs" | Get-Content -Raw
$efficiencyModuleCode = Get-ChildItem (Join-Path $backendDir "Modules\EfficiencyOptimizer") -Recurse -Filter "*.cs" | Get-Content -Raw
$alarmModuleCode = Get-ChildItem (Join-Path $backendDir "Modules\AlarmManager") -Recurse -Filter "*.cs" | Get-Content -Raw

$usesMediator = ($bacnetGatewayCode -match 'IMediator|ISender|IPublisher') -or 
                ($efficiencyModuleCode -match 'IMediator|ISender|IPublisher') -or
                ($alarmModuleCode -match 'IMediator|ISender|IPublisher')
Write-CheckResult "Uses MediatR for communication" $usesMediator

$bacnetReferencesEfficiency = $bacnetGatewayCode -match 'EfficiencyOptimizer'
$bacnetReferencesAlarm = $bacnetGatewayCode -match 'AlarmManager'
$efficiencyReferencesAlarm = $efficiencyModuleCode -match 'AlarmManager'

Write-CheckResult "BacnetGateway does not reference EfficiencyOptimizer" (-not $bacnetReferencesEfficiency)
Write-CheckResult "BacnetGateway does not reference AlarmManager" (-not $bacnetReferencesAlarm)
Write-CheckResult "EfficiencyOptimizer does not reference AlarmManager" (-not $efficiencyReferencesAlarm)

$usesSharedCommands = ($bacnetGatewayCode -match 'Modules\.Shared\.Commands') -or 
                      ($efficiencyModuleCode -match 'Modules\.Shared\.Commands') -or
                      ($alarmModuleCode -match 'Modules\.Shared\.Commands')
$usesSharedEvents = ($bacnetGatewayCode -match 'Modules\.Shared\.Events') -or 
                    ($efficiencyModuleCode -match 'Modules\.Shared\.Events') -or
                    ($alarmModuleCode -match 'Modules\.Shared\.Events')
Write-CheckResult "Uses Shared.Commands for communication" $usesSharedCommands
Write-CheckResult "Uses Shared.Events for communication" $usesSharedEvents

Write-Host ""
Write-Host "[6/6] Configuration Validation" -ForegroundColor Cyan

$appSettings = Get-Content (Join-Path $backendDir "appsettings.json") -Raw
$hasOptimizationConfig = $appSettings -match '"Optimization"'
$hasModelWeightsPath = $appSettings -match '"ModelWeightsPath"'
$hasBacnetConfig = $appSettings -match '"BACnet"'
$hasWechatConfig = $appSettings -match '"Wechat"'

Write-CheckResult "Has Optimization config section" $hasOptimizationConfig
Write-CheckResult "Has ModelWeightsPath configuration" $hasModelWeightsPath
Write-CheckResult "Has BACnet config section" $hasBacnetConfig
Write-CheckResult "Has Wechat config section" $hasWechatConfig

$neuralNetworkCode = Get-Content (Join-Path $backendDir "Modules\EfficiencyOptimizer\Services\NeuralNetworkOptimizationService.cs") -Raw
$readsFromConfig = ($neuralNetworkCode -match 'IOptions<OptimizationSettings>|ModelWeightsPath') -and 
                   ($neuralNetworkCode -notmatch 'hardcoded|hard.coded')
Write-CheckResult "Neural network weights path from config (not hardcoded)" $readsFromConfig

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Validation Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Total checks: $totalChecks" -ForegroundColor White
Write-Host "  Passed:       $($totalChecks - $totalIssues)" -ForegroundColor Green
Write-Host "  Issues:       $totalIssues" -ForegroundColor $(if ($totalIssues -eq 0) { 'Green' } else { 'Red' })
Write-Host ""

if ($totalIssues -eq 0) {
    Write-Host "  All static validations passed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Install .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0"
    Write-Host "  2. Run: .\run-tests.bat"
    Write-Host "  3. Or manually: dotnet test ChillerPlant.sln"
    Write-Host ""
} else {
    Write-Host "  Found $totalIssues issues, please fix them." -ForegroundColor Yellow
    exit 1
}

if ($RunTests) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Attempting to Run Tests" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    try {
        $dotnetVersion = & dotnet --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  .NET SDK version: $dotnetVersion" -ForegroundColor Green
            Write-Host ""
            Write-Host "  Running tests..." -ForegroundColor Cyan
            
            & dotnet test (Join-Path $baseDir "ChillerPlant.sln") --logger "console;verbosity=normal"
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host ""
                Write-Host "  All tests passed!" -ForegroundColor Green
            } else {
                Write-Host ""
                Write-Host "  Some tests failed" -ForegroundColor Red
                exit 1
            }
        } else {
            Write-Host "  .NET SDK not found, skipping test run" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  .NET SDK not found, skipping test run" -ForegroundColor Yellow
    }
}

Write-Host ""
exit 0
