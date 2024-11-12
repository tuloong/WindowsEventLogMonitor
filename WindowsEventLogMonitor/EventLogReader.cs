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

}
