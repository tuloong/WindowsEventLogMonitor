using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsEventLogMonitor;

internal class EventLogReader
{
    private EventLog eventLog;

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

    /// <summary>
    /// 筛选 MSSQLSERVER 的事件
    /// </summary>
    /// <param name="source"></param>
    /// <param name="eventType"></param>
    /// <returns></returns>
    public List<EventLogEntry> GetFilteredEntries(string source, string eventType)
    {
        HashSet<EventLogEntry> filteredEntries = new HashSet<EventLogEntry>();

        foreach (EventLogEntry entry in eventLog.Entries)
        {
            if (entry.Source == source && (string.IsNullOrEmpty(eventType) || entry.EntryType.ToString() == eventType))
            {
                filteredEntries.Add(entry);
            }
        }

        return filteredEntries.ToList();
    }
}
