# SQL Server登录审计配置脚本
# 用于启用SQL Server登录成功和失败的审计功能

param(
    [Parameter(Mandatory=$false)]
    [string]$ServerInstance = "localhost",
    
    [Parameter(Mandatory=$false)]
    [string]$DatabaseName = "master",
    
    [Parameter(Mandatory=$false)]
    [PSCredential]$Credential,
    
    [Parameter(Mandatory=$false)]
    [switch]$UseWindowsAuth = $true,
    
    [Parameter(Mandatory=$false)]
    [switch]$RestartService = $false
)

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Test-SQLServerConnection {
    param(
        [string]$ServerInstance,
        [string]$DatabaseName,
        [PSCredential]$Credential,
        [bool]$UseWindowsAuth
    )
    
    try {
        if ($UseWindowsAuth) {
            $connectionString = "Server=$ServerInstance;Database=$DatabaseName;Integrated Security=True;Connection Timeout=30;"
        } else {
            $username = $Credential.UserName
            $password = $Credential.GetNetworkCredential().Password
            $connectionString = "Server=$ServerInstance;Database=$DatabaseName;User Id=$username;Password=$password;Connection Timeout=30;"
        }
        
        $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        $connection.Open()
        $connection.Close()
        return $true
    }
    catch {
        Write-ColorOutput "数据库连接失败: $($_.Exception.Message)" "Red"
        return $false
    }
}

function Enable-SQLLoginAudit {
    param(
        [string]$ServerInstance,
        [string]$DatabaseName,
        [PSCredential]$Credential,
        [bool]$UseWindowsAuth
    )
    
    try {
        # 构建连接字符串
        if ($UseWindowsAuth) {
            $connectionString = "Server=$ServerInstance;Database=$DatabaseName;Integrated Security=True;Connection Timeout=30;"
        } else {
            $username = $Credential.UserName
            $password = $Credential.GetNetworkCredential().Password
            $connectionString = "Server=$ServerInstance;Database=$DatabaseName;User Id=$username;Password=$password;Connection Timeout=30;"
        }
        
        $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        $connection.Open()
        
        # 启用登录审计的SQL命令
        $sql = @"
-- 启用登录成功和失败的审计 (需要重启SQL Server服务)
EXEC xp_instance_regwrite 
    N'HKEY_LOCAL_MACHINE', 
    N'Software\Microsoft\MSSQLServer\MSSQLServer', 
    N'AuditLevel', 
    REG_DWORD, 
    3;  -- 0=None, 1=Failed logins only, 2=Successful logins only, 3=Both failed and successful logins

-- 显示当前审计设置
EXEC xp_instance_regread 
    N'HKEY_LOCAL_MACHINE', 
    N'Software\Microsoft\MSSQLServer\MSSQLServer', 
    N'AuditLevel';
"@
        
        $command = New-Object System.Data.SqlClient.SqlCommand($sql, $connection)
        $result = $command.ExecuteScalar()
        
        $connection.Close()
        
        Write-ColorOutput "SQL Server登录审计配置已更新" "Green"
        Write-ColorOutput "审计级别已设置为 3 (成功和失败的登录都会被记录)" "Green"
        Write-ColorOutput "注意: 需要重启SQL Server服务才能生效!" "Yellow"
        
        return $true
    }
    catch {
        Write-ColorOutput "配置审计失败: $($_.Exception.Message)" "Red"
        return $false
    }
}

function Restart-SQLServerService {
    param(
        [string]$ServerInstance
    )
    
    try {
        # 获取SQL Server服务名
        $serviceName = if ($ServerInstance -eq "localhost" -or $ServerInstance -eq ".") {
            "MSSQLSERVER"
        } else {
            "MSSQL`$$ServerInstance"
        }
        
        Write-ColorOutput "正在重启SQL Server服务: $serviceName" "Yellow"
        
        # 停止服务
        Stop-Service -Name $serviceName -Force
        Write-ColorOutput "SQL Server服务已停止" "Yellow"
        
        # 启动服务
        Start-Service -Name $serviceName
        Write-ColorOutput "SQL Server服务已启动" "Green"
        
        # 等待服务完全启动
        Start-Sleep -Seconds 10
        
        return $true
    }
    catch {
        Write-ColorOutput "重启SQL Server服务失败: $($_.Exception.Message)" "Red"
        return $false
    }
}

function Get-CurrentAuditSettings {
    param(
        [string]$ServerInstance,
        [string]$DatabaseName,
        [PSCredential]$Credential,
        [bool]$UseWindowsAuth
    )
    
    try {
        if ($UseWindowsAuth) {
            $connectionString = "Server=$ServerInstance;Database=$DatabaseName;Integrated Security=True;Connection Timeout=30;"
        } else {
            $username = $Credential.UserName
            $password = $Credential.GetNetworkCredential().Password
            $connectionString = "Server=$ServerInstance;Database=$DatabaseName;User Id=$username;Password=$password;Connection Timeout=30;"
        }
        
        $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        $connection.Open()
        
        $sql = @"
EXEC xp_instance_regread 
    N'HKEY_LOCAL_MACHINE', 
    N'Software\Microsoft\MSSQLServer\MSSQLServer', 
    N'AuditLevel';
"@
        
        $command = New-Object System.Data.SqlClient.SqlCommand($sql, $connection)
        $reader = $command.ExecuteReader()
        
        $auditLevel = $null
        if ($reader.Read()) {
            $auditLevel = $reader["Data"]
        }
        
        $reader.Close()
        $connection.Close()
        
        switch ($auditLevel) {
            0 { Write-ColorOutput "当前审计级别: 0 (不审计)" "Red" }
            1 { Write-ColorOutput "当前审计级别: 1 (仅审计失败的登录)" "Yellow" }
            2 { Write-ColorOutput "当前审计级别: 2 (仅审计成功的登录)" "Yellow" }
            3 { Write-ColorOutput "当前审计级别: 3 (审计成功和失败的登录)" "Green" }
            default { Write-ColorOutput "无法获取当前审计级别" "Red" }
        }
        
        return $auditLevel
    }
    catch {
        Write-ColorOutput "获取审计设置失败: $($_.Exception.Message)" "Red"
        return $null
    }
}

# 主脚本逻辑
Write-ColorOutput "=== SQL Server登录审计配置工具 ===" "Cyan"
Write-ColorOutput "服务器实例: $ServerInstance" "White"
Write-ColorOutput "数据库: $DatabaseName" "White"

# 检查是否以管理员权限运行
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
if (-not $isAdmin) {
    Write-ColorOutput "警告: 建议以管理员权限运行此脚本" "Yellow"
}

# 如果没有提供凭据且不使用Windows认证，则提示输入
if (-not $UseWindowsAuth -and -not $Credential) {
    $Credential = Get-Credential -Message "请输入SQL Server登录凭据"
}

# 测试数据库连接
Write-ColorOutput "`n正在测试数据库连接..." "Yellow"
if (-not (Test-SQLServerConnection -ServerInstance $ServerInstance -DatabaseName $DatabaseName -Credential $Credential -UseWindowsAuth $UseWindowsAuth)) {
    Write-ColorOutput "脚本执行终止" "Red"
    exit 1
}

Write-ColorOutput "数据库连接成功!" "Green"

# 获取当前审计设置
Write-ColorOutput "`n正在检查当前审计设置..." "Yellow"
$currentLevel = Get-CurrentAuditSettings -ServerInstance $ServerInstance -DatabaseName $DatabaseName -Credential $Credential -UseWindowsAuth $UseWindowsAuth

# 配置审计
Write-ColorOutput "`n正在配置SQL Server登录审计..." "Yellow"
if (Enable-SQLLoginAudit -ServerInstance $ServerInstance -DatabaseName $DatabaseName -Credential $Credential -UseWindowsAuth $UseWindowsAuth) {
    
    if ($RestartService) {
        Write-ColorOutput "`n正在重启SQL Server服务..." "Yellow"
        if (Restart-SQLServerService -ServerInstance $ServerInstance) {
            Write-ColorOutput "`n验证新的审计设置..." "Yellow"
            Start-Sleep -Seconds 5
            Get-CurrentAuditSettings -ServerInstance $ServerInstance -DatabaseName $DatabaseName -Credential $Credential -UseWindowsAuth $UseWindowsAuth
        }
    } else {
        Write-ColorOutput "`n请手动重启SQL Server服务以使配置生效" "Yellow"
        Write-ColorOutput "或使用 -RestartService 参数自动重启服务" "Yellow"
    }
    
    Write-ColorOutput "`n=== 配置完成 ===" "Cyan"
    Write-ColorOutput "SQL Server现在将记录以下事件到Windows事件日志:" "White"
    Write-ColorOutput "• 事件ID 18453: 登录成功" "Green"
    Write-ColorOutput "• 事件ID 18456: 登录失败" "Green"
    Write-ColorOutput "`n可以在Windows事件查看器的'应用程序'日志中查看这些事件" "White"
    Write-ColorOutput "事件源: MSSQLSERVER" "White"
    
} else {
    Write-ColorOutput "配置失败，请检查权限和连接设置" "Red"
    exit 1
}

Write-ColorOutput "`n脚本执行完成!" "Cyan" 