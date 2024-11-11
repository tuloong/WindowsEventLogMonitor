using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsEventLogMonitor;

internal class Config
{
    public string? ApiUrl { get; set; }

    public static Config? LoadConfig()
    {
        var json = File.ReadAllText("config.json");
        return JsonConvert.DeserializeObject<Config?>(json);
    }

    public static void SaveConfig(Config config)
    {
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText("config.json", json);
    }
}
