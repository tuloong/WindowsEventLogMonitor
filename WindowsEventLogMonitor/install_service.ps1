# SQL Server 日志监控服务安装脚本 (PowerShell版本)
# 需要管理员权限运行

Write-Host "=====================================" -ForegroundColor Green
Write-Host "SQL Server 日志监控服务安装程序" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host ""

# 检查管理员权限
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
if (-NOT $isAdmin) {
    Write-Host "错误：需要管理员权限！" -ForegroundColor Red
    Write-Host "请右键点击PowerShell，选择'以管理员身份运行'，然后执行此脚本" -ForegroundColor Red
    Write-Host ""
    Read-Host "按任意键退出"
    exit 1
}

Write-Host "已检测到管理员权限，继续安装..." -ForegroundColor Green

# 设置变量
$ServiceName = "SqlServerLogMonitor"
$ServiceDisplayName = "SQL Server日志监控服务"
$ScriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$PublishPath = Join-Path $ScriptPath "bin\Release\net8.0-windows\win-x64\publish"
$ExePath = Join-Path $PublishPath "WindowsEventLogMonitor.exe"

Write-Host "脚本路径: $ScriptPath" -ForegroundColor Yellow
Write-Host "发布路径: $PublishPath" -ForegroundColor Yellow
Write-Host "可执行文件: $ExePath" -ForegroundColor Yellow
Write-Host ""

# 检查发布目录和可执行文件
if (-not (Test-Path $PublishPath)) {
    Write-Host "错误：找不到发布目录 $PublishPath" -ForegroundColor Red
    Write-Host "请确保程序已正确编译和发布" -ForegroundColor Red
    Read-Host "按任意键退出"
    exit 1
}

if (-not (Test-Path $ExePath)) {
    Write-Host "错误：找不到 WindowsEventLogMonitor.exe" -ForegroundColor Red
    Write-Host "路径: $ExePath" -ForegroundColor Red
    Write-Host "请确保程序已正确编译和发布" -ForegroundColor Red
    Read-Host "按任意键退出"
    exit 1
}

Write-Host "✓ 可执行文件检查通过" -ForegroundColor Green

# 检查现有服务
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "发现现有服务，正在停止并删除..." -ForegroundColor Yellow

    if ($existingService.Status -eq 'Running') {
        Write-Host "停止服务..." -ForegroundColor Yellow
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 3
    }

    Write-Host "删除现有服务..." -ForegroundColor Yellow
    & sc.exe delete $ServiceName
    Start-Sleep -Seconds 2
}

# 创建新服务
Write-Host "创建Windows服务..." -ForegroundColor Green
$binPath = "`"$ExePath`" service"
Write-Host "服务路径: $binPath" -ForegroundColor Yellow

$result = & sc.exe create $ServiceName binPath= $binPath start= auto DisplayName= $ServiceDisplayName

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ 服务创建成功！" -ForegroundColor Green
    Write-Host ""

    # 启动服务
    Write-Host "正在启动服务..." -ForegroundColor Green
    try {
        Start-Service -Name $ServiceName
        Start-Sleep -Seconds 3

        $service = Get-Service -Name $ServiceName
        if ($service.Status -eq 'Running') {
            Write-Host "✓ 服务启动成功！" -ForegroundColor Green
            Write-Host ""
            Write-Host "服务安装和启动完成。您现在可以：" -ForegroundColor Green
            Write-Host "1. 在服务管理器中查看服务状态" -ForegroundColor White
            Write-Host "2. 在应用程序界面中管理服务" -ForegroundColor White
        } else {
            Write-Host "⚠ 服务创建成功但启动失败" -ForegroundColor Yellow
            Write-Host "服务状态: $($service.Status)" -ForegroundColor Yellow
            Write-Host "请检查配置文件 config.json 是否正确" -ForegroundColor Yellow
            Write-Host "您可以稍后手动启动服务：Start-Service $ServiceName" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "⚠ 服务创建成功但启动失败" -ForegroundColor Yellow
        Write-Host "错误信息: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "请检查配置文件 config.json 是否正确" -ForegroundColor Yellow
        Write-Host "您可以稍后手动启动服务：Start-Service $ServiceName" -ForegroundColor Yellow
    }
} else {
    Write-Host "✗ 服务创建失败" -ForegroundColor Red
    Write-Host "错误代码: $LASTEXITCODE" -ForegroundColor Red
    Write-Host "输出: $result" -ForegroundColor Red
}

Write-Host ""
Read-Host "按任意键退出"
