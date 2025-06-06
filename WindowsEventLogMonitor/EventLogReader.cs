using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsEventLogMonitor;

internal class EventLogReader
{
    private readonly EventLog eventLog;

    public EventLogReader(string logName)
    {
        eventLog = new EventLog(logName);
    }

    /// <summary>
    /// 连接到 Windows 事件日志
    /// </summary>
    /// <returns></returns>
    public bool IsConnected()
    {
        try
        {
            return eventLog.Entries.Count >= 0;
        }
        catch
        {
            return false;
        }
    }

    public EventLogEntryCollection ReadEntries()
    {
        return eventLog.Entries;
    }

    public List<EventLogEntry> FilterEventLogEntries(string source, string eventType)
    {
        if (string.IsNullOrEmpty(source))
            throw new ArgumentException("Source cannot be null or empty.", nameof(source));

        // Filtering using LINQ and directly converting to a List.
        List<EventLogEntry> filteredEntries = eventLog.Entries.Cast<EventLogEntry>()
            .Where(entry => entry.Source == source && (string.IsNullOrEmpty(eventType) || entry.EntryType.ToString() == eventType))
            .ToList();

        return filteredEntries;
    }

    /// <summary>
    /// 专门用于收集SQL Server相关的登录日志
    /// </summary>
    /// <param name="includeMSSQLSERVER">是否包含MSSQLSERVER源的日志</param>
    /// <param name="includeWindowsAuth">是否包含Windows身份验证相关日志</param>
    /// <returns></returns>
    public List<EventLogEntry> GetSQLServerLoginLogs(bool includeMSSQLSERVER = true, bool includeWindowsAuth = true)
    {
        var sqlServerLogs = new List<EventLogEntry>();

        // 收集MSSQLSERVER相关的日志（事件ID 18456=登录失败, 18453=登录成功, 18454=登录成功已验证）
        if (includeMSSQLSERVER)
        {
            var mssqlLogs = eventLog.Entries.Cast<EventLogEntry>()
                .Where(entry => entry.Source == "MSSQLSERVER" &&
                       (entry.InstanceId == 18456 || entry.InstanceId == 18453 || entry.InstanceId == 18454))
                .ToList();
            sqlServerLogs.AddRange(mssqlLogs);
        }

        // 收集Windows身份验证相关的日志（从Security日志）
        if (includeWindowsAuth)
        {
            try
            {
                using (var securityLog = new EventLog("Security"))
                {
                    var authLogs = securityLog.Entries.Cast<EventLogEntry>()
                        .Where(entry =>
                            (entry.InstanceId == 4624 || entry.InstanceId == 4625) && // 登录成功/失败
                            entry.Message != null &&
                            entry.Message.Contains("SQL", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    sqlServerLogs.AddRange(authLogs);
                }
            }
            catch (Exception ex)
            {
                // 可能没有权限访问Security日志，记录但不影响其他日志收集
                Console.WriteLine($"无法访问Security日志: {ex.Message}");
            }
        }

        return sqlServerLogs.OrderByDescending(log => log.TimeGenerated).ToList();
    }

    /// <summary>
    /// 根据时间范围过滤日志
    /// </summary>
    public List<EventLogEntry> FilterByTimeRange(List<EventLogEntry> logs, DateTime startTime, DateTime endTime)
    {
        return logs.Where(log => log.TimeGenerated >= startTime && log.TimeGenerated <= endTime).ToList();
    }

    /// <summary>
    /// 获取特定事件ID的日志
    /// </summary>
    public List<EventLogEntry> FilterByEventIds(string source, params long[] eventIds)
    {
        return eventLog.Entries.Cast<EventLogEntry>()
            .Where(entry => entry.Source == source && eventIds.Contains(entry.InstanceId))
            .ToList();
    }
}
