using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace WindowsEventLogMonitor;

/// <summary>
/// SQL Server 日志监控器 - 专门用于收集和上传SQL Server登录相关的日志
/// </summary>
public class SqlServerLogMonitor : IDisposable
{
    private readonly EventLogReader applicationLogReader;
    private readonly EventLogReader securityLogReader;
    private readonly HttpService httpService;
    private readonly InMemoryDeduplicationStore _dedupStore;
    private bool isMonitoring = false;
    private bool disposed = false;

    // 缓存最新收集的日志，供UI显示使用
    private readonly List<SqlServerLogEntry> recentLogs = new List<SqlServerLogEntry>();
    private readonly object recentLogsLock = new object();
    
    // 可配置的缓存限制
    private int maxCacheLogs = 1000;

    // 启动时间管理
    private DateTime currentStartupTime;
    private DateTime lastProcessedTime;
    private readonly object startupTimeLock = new object();

    // 预编译正则表达式（避免每次调用重新编译）
    private static readonly Regex[] SqlUserNamePatterns =
    {
        new(@"Login name: '([^']+)'", RegexOptions.Compiled),
        new(@"用户名: '([^']+)'", RegexOptions.Compiled),
        new(@"User '([^']+)'", RegexOptions.Compiled),
        new(@"用户 '([^']+)'", RegexOptions.Compiled)
    };
    private static readonly Regex IpAddressPattern = new(@"\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b", RegexOptions.Compiled);
    private static readonly Regex[] SqlDatabaseNamePatterns =
    {
        new(@"Database: '([^']+)'", RegexOptions.Compiled),
        new(@"数据库: '([^']+)'", RegexOptions.Compiled),
        new(@"database '([^']+)'", RegexOptions.Compiled)
    };
    private static readonly Regex[] WindowsUserNamePatterns =
    {
        new(@"Account Name:\s*([^\r\n\t]+)", RegexOptions.Compiled),
        new(@"帐户名:\s*([^\r\n\t]+)", RegexOptions.Compiled)
    };
    private static readonly Regex[] WindowsClientIpPatterns =
    {
        new(@"Source Network Address:\s*([^\r\n\t]+)", RegexOptions.Compiled),
        new(@"源网络地址:\s*([^\r\n\t]+)", RegexOptions.Compiled)
    };

    public SqlServerLogMonitor()
    {
        applicationLogReader = new EventLogReader("Application");
        securityLogReader = new EventLogReader("Security");
        httpService = new HttpService();
        _dedupStore = new InMemoryDeduplicationStore();

        // 初始化启动时间
        lock (startupTimeLock)
        {
            currentStartupTime = DateTime.Now;
            lastProcessedTime = currentStartupTime;
        }

        Console.WriteLine($"SQL Server监控器启动时间: {currentStartupTime:yyyy-MM-dd HH:mm:ss}");
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
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                applicationLogReader?.Dispose();
                securityLogReader?.Dispose();
            }
            disposed = true;
        }
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
    /// 设置缓存最大日志数量
    /// </summary>
    public void SetMaxCacheLogs(int maxLogs)
    {
        maxCacheLogs = Math.Max(100, Math.Min(maxLogs, 10000)); // 限制在100-10000之间
    }

    /// <summary>
    /// 获取当前缓存最大日志数量
    /// </summary>
    public int GetMaxCacheLogs() => maxCacheLogs;

    /// <summary>
    /// 获取当前启动时间
    /// </summary>
    public DateTime GetCurrentStartupTime()
    {
        lock (startupTimeLock)
        {
            return currentStartupTime;
        }
    }

    /// <summary>
    /// 获取最后处理时间
    /// </summary>
    public DateTime GetLastProcessedTime()
    {
        lock (startupTimeLock)
        {
            return lastProcessedTime;
        }
    }

    /// <summary>
    /// 重置启动时间（当需要重新开始处理时）
    /// </summary>
    public void ResetStartupTime()
    {
        lock (startupTimeLock)
        {
            currentStartupTime = DateTime.Now;
            lastProcessedTime = currentStartupTime;
        }
        Console.WriteLine($"重置启动时间为: {currentStartupTime:yyyy-MM-dd HH:mm:ss}");
    }

    /// <summary>
    /// 立即收集可用的SQL Server日志（用于刷新功能）
    /// </summary>
    /// <param name="minutesBack">收集最近几分钟的日志，默认24小时</param>
    /// <returns>收集到的日志数量</returns>
    public async Task<int> CollectAvailableLogsAsync(int minutesBack = 1)
    {
        try
        {
            var endTime = DateTime.Now;
            var startTime = endTime.AddMinutes(-minutesBack);

            Console.WriteLine($"立即收集日志 - 时间范围: {startTime:yyyy-MM-dd HH:mm:ss} 到 {endTime:yyyy-MM-dd HH:mm:ss}");

            var newLogs = await GetNewSQLServerLogsByTimeRangeAsync(startTime, endTime);

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
    /// 收集并推送SQL Server日志 - 按启动时间管理，避免重复
    /// </summary>
    /// <returns>(采集到的日志条数, 成功推送的日志条数)</returns>
    public async Task<(int collected, int pushed)> CollectAndPushSQLServerLogsAsync(string apiUrl)
    {
        DateTime processingStartTime;
        DateTime currentLastProcessedTime;

        // 获取当前处理的时间范围
        lock (startupTimeLock)
        {
            processingStartTime = DateTime.Now;
            currentLastProcessedTime = lastProcessedTime;
        }

        Console.WriteLine($"开始收集日志 - 处理时间范围: {currentLastProcessedTime:yyyy-MM-dd HH:mm:ss} 到 {processingStartTime:yyyy-MM-dd HH:mm:ss}");

        // 第一步：按当前启动时间开始，从缓存中加载日志
        var sqlServerLogs = await GetNewSQLServerLogsByTimeRangeAsync(currentLastProcessedTime, processingStartTime);

        if (sqlServerLogs.Count == 0)
        {
            Console.WriteLine("没有新的SQL Server日志需要推送");
            return (0, 0);
        }

        Console.WriteLine($"收集到 {sqlServerLogs.Count} 条新的SQL Server日志 (已去重存储: {_dedupStore.Count} 条)");

        // 第二步：加载完记录的日志 - 更新缓存供UI显示
        UpdateRecentLogsCache(sqlServerLogs);

        // 第三步：更新启动时间（在推送前更新，确保即使推送失败也不会重复处理）
        lock (startupTimeLock)
        {
            lastProcessedTime = processingStartTime;
        }
        Console.WriteLine($"更新最后处理时间为: {processingStartTime:yyyy-MM-dd HH:mm:ss}");

        // 第四步：推送日志
        var config = Config.GetCachedConfig() ?? new Config();
        int pushedCount = await ProcessLogsInBatchesAsync(sqlServerLogs, apiUrl, batchSize: config.SqlServerMonitoring.BatchSize);

        if (pushedCount == sqlServerLogs.Count)
        {
            // 第五步：推送成功，日志处理完成
            Console.WriteLine("推送成功，日志处理完成");
            Console.WriteLine("准备下次处理周期...");
        }
        else if (pushedCount == 0)
        {
            // 推送全部失败，回滚最后处理时间
            lock (startupTimeLock)
            {
                lastProcessedTime = currentLastProcessedTime;
            }
            Console.WriteLine($"推送失败，回滚最后处理时间为: {currentLastProcessedTime:yyyy-MM-dd HH:mm:ss}");
        }
        else
        {
            // 部分成功：不回滚时间（已成功的批次已记入去重存储，下次不会重复推送）
            Console.WriteLine($"部分推送成功: {pushedCount}/{sqlServerLogs.Count}");
        }

        return (sqlServerLogs.Count, pushedCount);
    }

    /// <summary>
    /// 按时间范围获取SQL Server日志（使用 XPath 结构化查询，避免全量遍历事件日志）
    /// </summary>
    private async Task<List<SqlServerLogEntry>> GetNewSQLServerLogsByTimeRangeAsync(DateTime startTime, DateTime endTime)
    {
        var newLogs = new List<SqlServerLogEntry>();

        Console.WriteLine($"收集时间范围内的日志: {startTime:yyyy-MM-dd HH:mm:ss} 到 {endTime:yyyy-MM-dd HH:mm:ss}");

        // 从Application日志收集MSSQLSERVER日志（XPath 内核侧过滤，仅迭代命中记录）
        await Task.Run(() =>
        {
            try
            {
                // XPath: Provider=MSSQLSERVER 且 EventID in (18453,18454,18456)
                var xpath = "*[System[Provider[@Name='MSSQLSERVER'] and (EventID=18453 or EventID=18454 or EventID=18456)]]";
                var allMssqlLogs = applicationLogReader.QueryEvents(xpath);

                var mssqlLogs = allMssqlLogs
                    .Where(log => log.TimeGenerated >= startTime && log.TimeGenerated < endTime)
                    .ToList();

                Console.WriteLine($"从Application日志中找到 {mssqlLogs.Count} 条MSSQLSERVER日志（时间范围内）");

                foreach (var log in mssqlLogs)
                {
                    var uniqueKey = GenerateUniqueKey(log);

                    // Skip if already pushed in this session
                    if (_dedupStore.Contains(uniqueKey))
                    {
                        Console.WriteLine($"Skipping duplicate log: {uniqueKey}");
                        continue;
                    }

                    newLogs.Add(new SqlServerLogEntry
                    {
                        UniqueKey = uniqueKey,
                        TimeGenerated = log.TimeGenerated,
                        EventId = (int)log.InstanceId,
                        Source = log.Source,
                        EntryType = log.EntryType,
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

        // 从Security日志收集Windows身份验证日志（XPath 内核侧过滤）
        try
        {
            await Task.Run(() =>
            {
                try
                {
                    // XPath: Provider=Microsoft-Windows-Security-Auditing 且 EventID in (4624,4625)
                    var xpath = "*[System[Provider[@Name='Microsoft-Windows-Security-Auditing'] and (EventID=4624 or EventID=4625)]]";
                    var authLogs = securityLogReader.QueryEvents(xpath)
                        .Where(log => log.TimeGenerated >= startTime &&
                                     log.TimeGenerated < endTime &&
                                     !string.IsNullOrEmpty(log.Message) &&
                                     log.Message.Contains("SQL", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    Console.WriteLine($"从Security日志中找到 {authLogs.Count} 条Windows身份验证日志（时间范围内）");

                    foreach (var log in authLogs)
                    {
                        var uniqueKey = GenerateUniqueKey(log);

                        // Skip if already pushed in this session
                        if (_dedupStore.Contains(uniqueKey))
                        {
                            Console.WriteLine($"Skipping duplicate log: {uniqueKey}");
                            continue;
                        }

                        newLogs.Add(new SqlServerLogEntry
                        {
                            UniqueKey = uniqueKey,
                            TimeGenerated = log.TimeGenerated,
                            EventId = (int)log.InstanceId,
                            Source = log.Source,
                            EntryType = log.EntryType,
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

        return newLogs.OrderBy(log => log.TimeGenerated).ToList();
    }

    /// <summary>
    /// 批量处理日志
    /// </summary>
    /// <returns>成功推送的日志条数</returns>
    private async Task<int> ProcessLogsInBatchesAsync(List<SqlServerLogEntry> logs, string apiUrl, int batchSize = 10)
    {
        int pushedCount = 0;

        for (int i = 0; i < logs.Count; i += batchSize)
        {
            var batch = logs.Skip(i).Take(batchSize).ToList();
            var json = JsonConvert.SerializeObject(batch, Formatting.None);

            try
            {
                await httpService.PushLogsToAPIAsync(json, apiUrl);
                Console.WriteLine($"成功推送 {batch.Count} 条SQL Server日志");

                // Track successfully pushed logs
                foreach (var log in batch)
                {
                    _dedupStore.TryAdd(log.UniqueKey);
                }
                pushedCount += batch.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"推送日志失败: {ex.Message}");
                // 继续处理剩余批次，但标记为失败
            }
        }

        return pushedCount;
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

        foreach (var regex in SqlUserNamePatterns)
        {
            var match = regex.Match(message);
            if (match.Success)
                return match.Groups[1].Value;
        }

        return "";
    }

    private string ExtractClientIPFromMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return "";

        var match = IpAddressPattern.Match(message);
        return match.Success ? match.Value : "";
    }

    private string ExtractDatabaseNameFromMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return "";

        foreach (var regex in SqlDatabaseNamePatterns)
        {
            var match = regex.Match(message);
            if (match.Success)
                return match.Groups[1].Value;
        }

        return "";
    }

    private string ExtractWindowsUserNameFromMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return "";

        foreach (var regex in WindowsUserNamePatterns)
        {
            var match = regex.Match(message);
            if (match.Success)
                return match.Groups[1].Value.Trim();
        }

        return "";
    }

    private string ExtractWindowsClientIPFromMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return "";

        foreach (var regex in WindowsClientIpPatterns)
        {
            var match = regex.Match(message);
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

    /// <summary>
    /// 为 EventLogItem 生成唯一键（XPath 查询路径使用）
    /// </summary>
    private string GenerateUniqueKey(EventLogItem log)
    {
        var timeStamp = new DateTimeOffset(log.TimeGenerated).ToUnixTimeSeconds();
        return $"{log.InstanceId}_{timeStamp}_{log.TimeGenerated.Ticks}_{log.Source}";
    }

    /// <summary>
    /// 更新最近日志缓存
    /// </summary>
    private void UpdateRecentLogsCache(List<SqlServerLogEntry> newLogs)
    {
        lock (recentLogsLock)
        {
            // 先限制新日志数量，避免一次性插入过多导致内存压力
            var trimmedNewLogs = newLogs.OrderByDescending(log => log.TimeGenerated)
                .Take(maxCacheLogs)
                .ToList();

            // 将新日志添加到缓存前面（最新的在前面）
            recentLogs.InsertRange(0, trimmedNewLogs);

            // 只保留最近N条日志，避免内存过大
            if (recentLogs.Count > maxCacheLogs)
            {
                var excessCount = recentLogs.Count - maxCacheLogs;
                recentLogs.RemoveRange(maxCacheLogs, excessCount);
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