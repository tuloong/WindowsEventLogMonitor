@echo off
echo =====================================
echo SQL Server 日志监控服务安装程序
echo =====================================
echo.

:: 检查管理员权限
net session >nul 2>&1
if %errorLevel% == 0 (
    echo 已检测到管理员权限，继续安装...
) else (
    echo 错误：需要管理员权限！
    echo 请右键点击此批处理文件，选择"以管理员身份运行"
    echo.
    pause
    exit /b 1
)

:: 切换到程序目录
cd /d "%~dp0\bin\Release\net8.0-windows\win-x64\publish"

:: 检查可执行文件是否存在
if not exist "WindowsEventLogMonitor.exe" (
    echo 错误：找不到 WindowsEventLogMonitor.exe
    echo 请确保程序已正确编译
    pause
    exit /b 1
)

echo.
echo 正在安装服务...

:: 删除现有服务（如果存在）
sc query SqlServerLogMonitor >nul 2>&1
if %errorLevel% == 0 (
    echo 发现现有服务，正在停止并删除...
    sc stop SqlServerLogMonitor >nul 2>&1
    timeout /t 3 >nul
    sc delete SqlServerLogMonitor
    timeout /t 2 >nul
)

:: 创建新服务
echo 创建Windows服务...
sc create SqlServerLogMonitor binPath= "%cd%\WindowsEventLogMonitor.exe service" start= auto DisplayName= "SQL Server日志监控服务"

if %errorLevel% == 0 (
    echo.
    echo ✓ 服务安装成功！
    echo.
    
    :: 启动服务
    echo 正在启动服务...
    sc start SqlServerLogMonitor
    
    if %errorLevel% == 0 (
        echo ✓ 服务启动成功！
        echo.
        echo 服务安装和启动完成。您现在可以：
        echo 1. 在服务管理器中查看服务状态
        echo 2. 在应用程序界面中管理服务
    ) else (
        echo ⚠ 服务安装成功但启动失败
        echo 请检查配置文件 config.json 是否正确
        echo 您可以稍后手动启动服务：net start SqlServerLogMonitor
    )
) else (
    echo ✗ 服务安装失败
    echo 请检查是否有足够的权限
)

echo.
echo 按任意键退出...
pause 