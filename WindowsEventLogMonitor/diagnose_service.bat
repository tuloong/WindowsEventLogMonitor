@echo off
echo =====================================
echo 服务诊断工具
echo =====================================
echo.

:: 检查管理员权限
net session >nul 2>&1
if %errorLevel% == 0 (
    echo 已检测到管理员权限
) else (
    echo 警告：没有管理员权限，某些操作可能失败
)

echo.
echo 1. 检查发布目录和文件...
cd /d "%~dp0\bin\Release\net8.0-windows\win-x64\publish"
echo 当前目录：%cd%

if exist "WindowsEventLogMonitor.exe" (
    echo ✓ 找到 WindowsEventLogMonitor.exe
) else (
    echo ✗ 未找到 WindowsEventLogMonitor.exe
    goto :end
)

if exist "config.json" (
    echo ✓ 找到 config.json
) else (
    echo ✗ 未找到 config.json
)

echo.
echo 2. 测试控制台模式运行...
echo 执行：WindowsEventLogMonitor.exe console
timeout /t 2 >nul
start /wait cmd /c "WindowsEventLogMonitor.exe console & timeout /t 5 >nul"

echo.
echo 3. 检查服务状态...
sc query SqlServerLogMonitor
echo 错误代码：%errorLevel%

echo.
echo 4. 尝试创建服务...
echo 执行命令：sc create SqlServerLogMonitor binPath="%cd%\WindowsEventLogMonitor.exe service" start=auto DisplayName="SQL Server日志监控服务"

:: 先删除现有服务
sc delete SqlServerLogMonitor >nul 2>&1

:: 创建服务
sc create SqlServerLogMonitor binPath="%cd%\WindowsEventLogMonitor.exe service" start=auto DisplayName="SQL Server日志监控服务"
echo 创建服务错误代码：%errorLevel%

if %errorLevel% == 0 (
    echo ✓ 服务创建成功
    echo.
    echo 5. 尝试启动服务...
    sc start SqlServerLogMonitor
    echo 启动服务错误代码：%errorLevel%
    
    echo.
    echo 6. 检查服务状态...
    sc query SqlServerLogMonitor
    
    echo.
    echo 7. 检查事件日志...
    echo 查看系统事件日志中的相关错误：
    powershell -Command "Get-EventLog -LogName System -Source 'Service Control Manager' -Newest 5 | Where-Object {$_.Message -like '*SqlServerLogMonitor*'} | Format-Table TimeGenerated, EntryType, Message -Wrap"
    
    echo.
    echo 查看应用程序事件日志中的相关错误：
    powershell -Command "Get-EventLog -LogName Application -Newest 10 | Where-Object {$_.Source -like '*SqlServerLogMonitor*' -or $_.Message -like '*SqlServerLogMonitor*'} | Format-Table TimeGenerated, EntryType, Source, Message -Wrap"
    
) else (
    echo ✗ 服务创建失败
)

:end
echo.
echo 诊断完成。
pause
