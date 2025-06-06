@echo off
echo =====================================
echo SQL Server 日志监控服务卸载程序
echo =====================================
echo.

:: 检查管理员权限
net session >nul 2>&1
if %errorLevel% == 0 (
    echo 已检测到管理员权限，继续卸载...
) else (
    echo 错误：需要管理员权限！
    echo 请右键点击此批处理文件，选择"以管理员身份运行"
    echo.
    pause
    exit /b 1
)

:: 检查服务是否存在
sc query SqlServerLogMonitor >nul 2>&1
if %errorLevel% == 0 (
    echo 发现服务 SqlServerLogMonitor，正在卸载...
    echo.
    
    :: 停止服务
    echo 正在停止服务...
    sc stop SqlServerLogMonitor
    timeout /t 5 >nul
    
    :: 删除服务
    echo 正在删除服务...
    sc delete SqlServerLogMonitor
    
    if %errorLevel% == 0 (
        echo ✓ 服务卸载成功！
    ) else (
        echo ✗ 服务删除失败
    )
) else (
    echo 服务 SqlServerLogMonitor 不存在或已被删除
)

echo.
echo 按任意键退出...
pause 