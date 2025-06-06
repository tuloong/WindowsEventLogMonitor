using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace WindowsEventLogMonitor;

/// <summary>
/// SQL Server 日志监控器 - 专门用于收集和上传SQL Server登录相关的日志
/// </summary>
public class SqlServerLogMonitor
{
    private readonly EventLogReader applicationLogReader;
    private readonly EventLogReader securityLogReader;
    private readonly HttpService httpService;
    private readonly string logFilePath = "sql_server_push_log.ini";
    private bool isMonitoring = false;

    // 缓存最新收集的日志，供UI显示使用
    private readonly List<SqlServerLogEntry> recentLogs = new List<SqlServerLogEntry>();
    private readonly object recentLogsLock = new object();

    public SqlServerLogMonitor()
    {
        applicationLogReader = new EventLogReader("Application");
        securityLogReader = new EventLogReader("Security");
        httpService = new HttpService();
    }

    /// <summary>
    /// 开始监控SQL Server日志
    /// </summary>
    public async Task StartMonitoringAsync(string apiUrl, int intervalSeconds = 30)
    {
        isMonitoring = true;

        while (isMonitoring)
        {
            try
            {
                await CollectAndPushSQLServerLogsAsync(apiUrl);
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
            }
            catch (Exception ex)
            {
                // 记录错误但继续监控
                Console.WriteLine($"监控过程中发生错误: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
            }
        }
    }

    /// <summary>
    /// 停止监控
    /// </summary>
    public void StopMonitoring()
    {
        isMonitoring = false;
    }

    /// <summary>
    /// 获取最近收集的日志（供UI显示）
    /// </summary>
    /// <param name="maxCount">最大返回数量，默认100条</param>
    /// <returns>最近的SQL Server日志列表</returns>
    public List<SqlServerLogEntry> GetRecentLogs(int maxCount = 100)
    {
        lock (recentLogsLock)
        {
            return recentLogs.Take(maxCount).ToList();
        }
    }

    /// <summary>
    /// 清空最近日志缓存
    /// </summary>
    public void ClearRecentLogs()
    {
        lock (recentLogsLock)
        {
            recentLogs.Clear();
        }
    }

    /// <summary>
    /// 立即收集可用的SQL Server日志（用于刷新功能）
    /// </summary>
    /// <param name="hoursBack">收集最近几小时的日志，默认24小时</param>
    /// <returns>收集到的日志数量</returns>
    public async Task<int> CollectAvailableLogsAsync(int hoursBack = 24)
    {
        try
        {
            var startTime = DateTime.Now.AddHours(-hoursBack);

            // 先测试能否直接获取到任何SQL Server日志
            Console.WriteLine("开始测试SQL Server日志读取...");
            var testLogs = await TestDirectLogReading();
            Console.WriteLine($"直接读取测试结果: {testLogs} 条日志");

            var newLogs = await GetAvailableLogsAsync(startTime);

            if (newLogs.Count > 0)
            {
                // 更新缓存
                UpdateRecentLogsCache(newLogs);
                Console.WriteLine($"立即收集到 {newLogs.Count} 条SQL Server日志");
            }
            else
            {
                Console.WriteLine("没有找到新的SQL Server日志");
            }

            return newLogs.Count;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"立即收集日志时发生错误: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// 测试直接读取SQL Server日志
    /// </summary>
    private async Task<int> TestDirectLogReading()
    {
        return await Task.Run(() =>
        {
            try
            {
                var eventLogReader = new EventLogReader("Application");
                // 直接调用EventLogReader的方法，不加任何过滤
                var allLogs = eventLogReader.GetSQLServerLoginLogs(includeMSSQLSERVER: true, includeWindowsAuth: false);
                Console.WriteLine($"EventLogReader.GetSQLServerLoginLogs返回 {allLogs.Count} 条日志");

                // 显示最近几条日志的信息
                var recentLogs = allLogs.Take(5).ToList();
                foreach (var log in recentLogs)
                {
                    Console.WriteLine($"日志: {log.TimeGenerated} - 事件ID: {log.InstanceId} - 来源: {log.Source}");
                }

                return allLogs.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"测试直接读取时发生错误: {ex.Message}");
                return 0;
            }
        });
    }

    /// <summary>
    /// 获取指定时间段内可用的SQL Server日志
    /// </summary>
    private async Task<List<SqlServerLogEntry>> GetAvailableLogsAsync(DateTime startTime)
    {
        var allLogs = new List<SqlServerLogEntry>();

        // 从Application日志收集MSSQLSERVER日志
        await Task.Run(() =>
        {
            try
            {
                var eventLogReader = new EventLogReader("Application");
                var allMssqlLogs = eventLogReader.FilterByEventIds("MSSQLSERVER", 18456, 18453, 18454);
                var mssqlLogs = allMssqlLogs.Where(log => log.TimeGenerated >= startTime).ToList();

                Console.WriteLine($"从Application日志中总共找到 {allMssqlLogs.Count} 条MSSQLSERVER日志");
                Console.WriteLine($"时间过滤后剩余 {mssqlLogs.Count} 条日志 (起始时间: {startTime:yyyy-MM-dd HH:mm:ss})");

                foreach (var log in mssqlLogs)
                {
                    allLogs.Add(new SqlServerLogEntry
                    {
                        UniqueKey = GenerateUniqueKey(log),
                        TimeGenerated = log.TimeGenerated,
                        EventId = (int)log.InstanceId,
                        Source = log.Source,
                        EntryType = log.EntryType.ToString(),
                        Message = log.Message,
                        LogType = GetLogType(log.InstanceId),
                        UserName = ExtractUserNameFromMessage(log.Message),
                        ClientIP = ExtractClientIPFromMessage(log.Message),
                        DatabaseName = ExtractDatabaseNameFromMessage(log.Message)
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取Application日志时发生错误: {ex.Message}");
            }
        });

        // 从Security日志收集Windows身份验证日志
        try
        {
            await Task.Run(() =>
            {
                try
                {
                    var authLogs = securityLogReader.FilterByEventIds("Microsoft-Windows-Security-Auditing", 4624, 4625)
                        .Where(log => log.TimeGenerated >= startTime &&
                                     log.Message != null &&
                                     log.Message.Contains("SQL", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    Console.WriteLine($"从Security日志中找到 {authLogs.Count} 条Windows身份验证日志");

                    foreach (var log in authLogs)
                    {
                        allLogs.Add(new SqlServerLogEntry
                        {
                            UniqueKey = GenerateUniqueKey(log),
                            TimeGenerated = log.TimeGenerated,
                            EventId = (int)log.InstanceId,
                            Source = log.Source,
                            EntryType = log.EntryType.ToString(),
                            Message = log.Message,
                            LogType = log.InstanceId == 4624 ? "Windows登录成功" : "Windows登录失败",
                            UserName = ExtractWindowsUserNameFromMessage(log.Message),
                            ClientIP = ExtractWindowsClientIPFromMessage(log.Message),
                            DatabaseName = ""
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"读取Security日志时发生错误: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"无法访问Security日志: {ex.Message}");
        }

        return allLogs.OrderByDescending(log => log.TimeGenerated).ToList();
    }

    /// <summary>
    /// 收集并推送SQL Server日志
    /// </summary>
    public async Task CollectAndPushSQLServerLogsAsync(string apiUrl)
    {
        var pushedLogIds = LoadPushedLogIds();
        var lastProcessedTime = GetLastProcessedTime();

        // 收集SQL Server登录日志
        var sqlServerLogs = await GetNewSQLServerLogsAsync(lastProcessedTime, pushedLogIds);

        if (sqlServerLogs.Count > 0)
        {
            // 更新最近日志缓存（供UI显示）
            UpdateRecentLogsCache(sqlServerLogs);

            // 批量处理日志
            await ProcessLogsInBatchesAsync(sqlServerLogs, apiUrl, batchSize: 10);

            // 更新已处理日志记录
            await UpdateProcessedLogsAsync(sqlServerLogs);
        }
    }

    /// <summary>
    /// 获取新的SQL Server日志
    /// </summary>
    private async Task<List<SqlServerLogEntry>> GetNewSQLServerLogsAsync(DateTime lastProcessedTime, HashSet<string> pushedLogIds)
    {
        var newLogs = new List<SqlServerLogEntry>();

        // 从Application日志收集MSSQLSERVER日志
        await Task.Run(() =>
        {
            var eventLogReader = new EventLogReader("Application");
            var allMssqlLogs = eventLogReader.FilterEventLogEntries("MSSQLSERVER", string.Empty);

            var mssqlLogs = allMssqlLogs
                .Where(log => log.TimeGenerated > lastProcessedTime &&
                             !pushedLogIds.Contains(GenerateUniqueKey(log)))
                .ToList();

            Console.WriteLine($"找到 {allMssqlLogs.Count} 条MSSQLSERVER日志，过滤后剩余 {mssqlLogs.Count} 条新日志");

            foreach (var log in mssqlLogs)
            {
                newLogs.Add(new SqlServerLogEntry
                {
                    UniqueKey = GenerateUniqueKey(log),
                    TimeGenerated = log.TimeGenerated,
                    EventId = (int)log.InstanceId,
                    Source = log.Source,
                    EntryType = log.EntryType.ToString(),
                    Message = log.Message,
                    LogType = GetLogType(log.InstanceId),
                    UserName = ExtractUserNameFromMessage(log.Message),
                    ClientIP = ExtractClientIPFromMessage(log.Message),
                    DatabaseName = ExtractDatabaseNameFromMessage(log.Message)
                });
            }
        });

        // 如果有权限，也从Security日志收集相关的Windows身份验证日志
        try
        {
            await Task.Run(() =>
            {
                var authLogs = securityLogReader.FilterByEventIds("Microsoft-Windows-Security-Auditing", 4624, 4625)
                    .Where(log => log.TimeGenerated > lastProcessedTime &&
                                 log.Message != null &&
                                 log.Message.Contains("SQL", StringComparison.OrdinalIgnoreCase) &&
                                 !pushedLogIds.Contains(GenerateUniqueKey(log)))
                    .ToList();

                foreach (var log in authLogs)
                {
                    newLogs.Add(new SqlServerLogEntry
                    {
                        UniqueKey = GenerateUniqueKey(log),
                        TimeGenerated = log.TimeGenerated,
                        EventId = (int)log.InstanceId,
                        Source = log.Source,
                        EntryType = log.EntryType.ToString(),
                        Message = log.Message,
                        LogType = log.InstanceId == 4624 ? "Windows登录成功" : "Windows登录失败",
                        UserName = ExtractWindowsUserNameFromMessage(log.Message),
                        ClientIP = ExtractWindowsClientIPFromMessage(log.Message),
                        DatabaseName = ""
                    });
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"无法访问Security日志: {ex.Message}");
        }

        return newLogs.OrderBy(log => log.TimeGenerated).ToList();
    }

    /// <summary>
    /// 批量处理日志
    /// </summary>
    private async Task ProcessLogsInBatchesAsync(List<SqlServerLogEntry> logs, string apiUrl, int batchSize = 10)
    {
        for (int i = 0; i < logs.Count; i += batchSize)
        {
            var batch = logs.Skip(i).Take(batchSize).ToList();
            var json = JsonConvert.SerializeObject(batch, Formatting.Indented);

            try
            {
                await httpService.PushLogsToAPIAsync(json, apiUrl);
                Console.WriteLine($"成功推送 {batch.Count} 条SQL Server日志");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"推送日志失败: {ex.Message}");
                // 可以考虑重试机制或将失败的日志保存到本地
            }
        }
    }

    private string GetLogType(long eventId)
    {
        return eventId switch
        {
            18456 => "SQL登录失败",
            18453 => "SQL登录成功",
            18454 => "SQL登录成功(已验证)",
            4624 => "Windows登录成功",
            4625 => "Windows登录失败",
            _ => "未知"
        };
    }

    private string ExtractUserNameFromMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return "";

        // SQL Server日志中用户名通常在单引号中
        var patterns = new[]
        {
            @"Login name: '([^']+)'",
            @"用户名: '([^']+)'",
            @"User '([^']+)'",
            @"用户 '([^']+)'"
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(message, pattern);
            if (match.Success)
                return match.Groups[1].Value;
        }

        return "";
    }

    private string ExtractClientIPFromMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return "";

        // 提取IP地址
        var ipPattern = @"\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b";
        var match = System.Text.RegularExpressions.Regex.Match(message, ipPattern);
        return match.Success ? match.Value : "";
    }

    private string ExtractDatabaseNameFromMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return "";

        // SQL Server日志中数据库名通常在特定位置
        var patterns = new[]
        {
            @"Database: '([^']+)'",
            @"数据库: '([^']+)'",
            @"database '([^']+)'"
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(message, pattern);
            if (match.Success)
                return match.Groups[1].Value;
        }

        return "";
    }

    private string ExtractWindowsUserNameFromMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return "";

        // Windows Security日志格式不同
        var patterns = new[]
        {
            @"Account Name:\s*([^\r\n\t]+)",
            @"帐户名:\s*([^\r\n\t]+)"
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(message, pattern);
            if (match.Success)
                return match.Groups[1].Value.Trim();
        }

        return "";
    }

    private string ExtractWindowsClientIPFromMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return "";

        var patterns = new[]
        {
            @"Source Network Address:\s*([^\r\n\t]+)",
            @"源网络地址:\s*([^\r\n\t]+)"
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(message, pattern);
            if (match.Success)
            {
                var value = match.Groups[1].Value.Trim();
                // 验证是否为有效IP
                if (System.Net.IPAddress.TryParse(value, out _))
                    return value;
            }
        }

        return "";
    }

    private string GenerateUniqueKey(EventLogEntry log)
    {
        var timeStamp = new DateTimeOffset(log.TimeGenerated).ToUnixTimeSeconds();
        return $"{log.InstanceId}_{timeStamp}_{log.TimeGenerated.Ticks}_{log.Source}";
    }

    private HashSet<string> LoadPushedLogIds()
    {
        var pushedLogIds = new HashSet<string>();
        if (File.Exists(logFilePath))
        {
            var lines = File.ReadAllLines(logFilePath);
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length > 0)
                {
                    var logIdPart = parts[0].Split(':');
                    if (logIdPart.Length > 1)
                    {
                        pushedLogIds.Add(logIdPart[1].Trim());
                    }
                }
            }
        }
        return pushedLogIds;
    }

    private DateTime GetLastProcessedTime()
    {
        DateTime maxProcessedTime = DateTime.MinValue;
        string pattern = @"Generated at: (?<generatedTime>[\d\-/\s:]+)";

        if (File.Exists(logFilePath))
        {
            var lines = File.ReadAllLines(logFilePath);
            foreach (var line in lines)
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, pattern);
                if (match.Success)
                {
                    if (DateTime.TryParse(match.Groups["generatedTime"].Value, out var generatedDate))
                    {
                        if (generatedDate > maxProcessedTime)
                        {
                            maxProcessedTime = generatedDate;
                        }
                    }
                }
            }
        }

        return maxProcessedTime;
    }

    /// <summary>
    /// 更新最近日志缓存
    /// </summary>
    private void UpdateRecentLogsCache(List<SqlServerLogEntry> newLogs)
    {
        lock (recentLogsLock)
        {
            // 将新日志添加到缓存前面（最新的在前面）
            recentLogs.InsertRange(0, newLogs.OrderByDescending(log => log.TimeGenerated));

            // 只保留最近1000条日志，避免内存过大
            if (recentLogs.Count > 1000)
            {
                var excessCount = recentLogs.Count - 1000;
                recentLogs.RemoveRange(1000, excessCount);
            }
        }
    }

    private async Task UpdateProcessedLogsAsync(List<SqlServerLogEntry> logs)
    {
        var logTime = DateTime.Now;
        using (var writer = new StreamWriter(logFilePath, true))
        {
            foreach (var log in logs)
            {
                var logEntry = $"Log ID: {log.UniqueKey}, Generated at: {log.TimeGenerated}, Pushed at: {logTime}";
                await writer.WriteLineAsync(logEntry);
            }
        }
    }
}

/// <summary>
/// SQL Server 日志条目
/// </summary>
public class SqlServerLogEntry
{
    public string UniqueKey { get; set; } = "";
    public DateTime TimeGenerated { get; set; }
    public int EventId { get; set; }
    public string Source { get; set; } = "";
    public string EntryType { get; set; } = "";
    public string Message { get; set; } = "";
    public string LogType { get; set; } = "";
    public string UserName { get; set; } = "";
    public string ClientIP { get; set; } = "";
    public string DatabaseName { get; set; } = "";
}