# SQL Server 登录日志监控解决方案

## 概述

这是一个完整的 SQL Server 登录日志收集和监控解决方案，可以自动收集 SQL Server 的登录成功和失败事件，并将这些日志上传到远程服务器进行集中审计。

## 问题解答

### 1. 为什么默认情况下没有记录登录成功的日志？

**原因分析：**
- SQL Server 默认只记录登录失败事件（事件ID 18456），不记录登录成功事件（事件ID 18453）
- 这是出于性能和存储空间的考虑，因为登录成功事件通常比失败事件多得多
- 需要手动配置审计级别来启用登录成功的记录

**解决方法：**
使用提供的 PowerShell 脚本 `Enable-SQLServerAudit.ps1` 来配置审计：

```powershell
# 基本用法
.\Enable-SQLServerAudit.ps1

# 指定服务器实例并自动重启服务
.\Enable-SQLServerAudit.ps1 -ServerInstance "MyServer\SQLEXPRESS" -RestartService

# 使用SQL Server身份验证
.\Enable-SQLServerAudit.ps1 -UseWindowsAuth:$false
```

或者直接在 SQL Server Management Studio 中执行：

```sql
-- 启用登录成功和失败的审计
EXEC xp_instance_regwrite 
    N'HKEY_LOCAL_MACHINE', 
    N'Software\Microsoft\MSSQLServer\MSSQLServer', 
    N'AuditLevel', 
    REG_DWORD, 
    3;  -- 3表示记录成功和失败的登录

-- 重启SQL Server服务后生效
```

### 2. 如何自动化地收集这些日志并通过API接口上传至远程日志服务器？

**解决方案架构：**

1. **事件日志收集器** (`EventLogReader.cs`)
   - 从 Windows 事件日志读取 SQL Server 相关事件
   - 支持过滤特定事件ID和时间范围

2. **SQL Server专用监控器** (`SqlServerLogMonitor.cs`)
   - 专门处理 SQL Server 登录日志
   - 自动提取用户名、客户端IP、数据库名等关键信息
   - 支持批量处理和增量收集

3. **HTTP上传服务** (`HttpService.cs`)
   - 支持重试机制和错误处理
   - 可配置超时和API密钥
   - 批量上传优化

4. **Windows服务** (`SqlServerLogService.cs`)
   - 可作为系统服务在后台运行
   - 自动启动和故障恢复
   - 详细的服务日志记录

**使用方式：**

#### 方式1：图形界面模式
运行 `WindowsEventLogMonitor.exe`，在界面中配置监控参数。

#### 方式2：Windows服务模式
```cmd
# 安装服务（以管理员身份运行）
WindowsEventLogMonitor.exe install

# 启动服务
net start SqlServerLogMonitor

# 停止服务
net stop SqlServerLogMonitor

# 卸载服务
WindowsEventLogMonitor.exe uninstall
```

#### 方式3：控制台模式（调试用）
```cmd
WindowsEventLogMonitor.exe console
```

**配置文件 (config.json)：**

```json
{
  "ApiUrl": "https://your-log-server.com/api/logs",
  "SqlServerMonitoring": {
    "Enabled": true,
    "MonitorIntervalSeconds": 30,
    "BatchSize": 10,
    "IncludeMSSQLSERVER": true,
    "IncludeWindowsAuth": true
  },
  "RetryPolicy": {
    "MaxRetries": 3,
    "RetryDelaySeconds": 5,
    "EnableRetry": true
  },
  "Security": {
    "UseHttps": true,
    "ApiKey": "your-api-key",
    "TimeoutSeconds": 30
  }
}
```

### 3. 性能影响和安全建议

#### 性能影响：

**影响因素：**
- 启用登录审计会增加少量CPU和I/O开销（通常<5%）
- 事件日志文件大小会增加
- 网络带宽消耗（取决于登录频率）

**优化建议：**
1. **合理设置监控间隔**：
   ```json
   "MonitorIntervalSeconds": 30  // 根据业务需求调整
   ```

2. **批量处理**：
   ```json
   "BatchSize": 10  // 批量上传减少网络请求
   ```

3. **日志轮转**：
   ```json
   "LogRetention": {
     "RetentionDays": 7,
     "MaxLogFileSizeKB": 500
   }
   ```

#### 安全建议：

1. **网络安全**：
   ```json
   "Security": {
     "UseHttps": true,  // 强制使用HTTPS
     "ApiKey": "xxx"    // 使用API密钥认证
   }
   ```

2. **权限控制**：
   - 以最小权限运行监控服务
   - 限制对配置文件的访问权限
   - 定期轮换API密钥

3. **数据保护**：
   - 敏感信息脱敏处理
   - 本地日志文件加密存储
   - 定期清理本地缓存

4. **监控告警**：
   - 监控服务运行状态
   - 设置日志上传失败告警
   - 定期检查审计配置

## 监控的事件类型

### SQL Server事件
- **事件ID 18453**: 登录成功
- **事件ID 18456**: 登录失败

### Windows Security事件（可选）
- **事件ID 4624**: Windows登录成功
- **事件ID 4625**: Windows登录失败

## 日志格式示例

```json
[
  {
    "UniqueKey": "18453_1673856000_637845000000000_MSSQLSERVER",
    "TimeGenerated": "2023-01-16T10:30:00",
    "EventId": 18453,
    "Source": "MSSQLSERVER",
    "EntryType": "Information",
    "LogType": "SQL登录成功",
    "UserName": "myuser",
    "ClientIP": "192.168.1.100",
    "DatabaseName": "MyDatabase",
    "Message": "Login succeeded for user 'myuser'. Connection made using SQL Server authentication."
  }
]
```

## 故障排除

### 常见问题：

1. **看不到登录成功日志**
   - 确认已启用审计级别为3
   - 确认已重启SQL Server服务
   - 检查Windows事件查看器中的应用程序日志

2. **日志上传失败**
   - 检查网络连接和API地址
   - 验证API密钥是否正确
   - 查看服务日志文件

3. **服务无法启动**
   - 确认以管理员权限安装服务
   - 检查配置文件格式是否正确
   - 查看Windows事件日志中的服务错误

### 调试步骤：

1. **测试API连接**：
   ```csharp
   var httpService = new HttpService();
   var result = await httpService.TestConnectionAsync("your-api-url");
   ```

2. **检查事件日志**：
   ```csharp
   var logReader = new EventLogReader("Application");
   var logs = logReader.FilterByEventIds("MSSQLSERVER", 18453, 18456);
   ```

3. **查看服务日志**：
   检查 `service_log.txt` 文件了解详细错误信息。

## 部署清单

- [ ] 配置SQL Server审计设置
- [ ] 安装和配置监控程序
- [ ] 设置API接口和密钥
- [ ] 安装Windows服务
- [ ] 测试日志收集和上传
- [ ] 设置监控告警
- [ ] 文档化部署配置

## 技术支持

如有问题，请检查：
1. Windows事件查看器 → 应用程序日志
2. 服务日志文件 `service_log.txt`
3. SQL Server错误日志
4. 网络连接和防火墙设置 