using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;


namespace WindowsEventLogMonitor;

internal class JsonService
{
    public string ConvertToJSON(List<EventLogEntry> entries)
    {
        var logData = entries.Select(entry => new
        {
            entry.TimeGenerated,
            entry.Source,
            entry.Message
        }).ToList();

        return JsonConvert.SerializeObject(logData);
    }
}
