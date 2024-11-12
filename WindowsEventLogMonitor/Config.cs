using Newtonsoft.Json;
using System;
using System.IO;

namespace WindowsEventLogMonitor;

internal class Config
{
    public string? ApiUrl { get; set; }

    private static Config? _cachedConfig;

    public static Config? LoadConfig()
    {
        if (_cachedConfig == null)
        {
            var json = File.ReadAllText("config.json");
            _cachedConfig = JsonConvert.DeserializeObject<Config?>(json);
        }
        return _cachedConfig;
    }

    public static void SaveConfig(Config config)
    {
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText("config.json", json);
        _cachedConfig = config; // 更新缓存
    }

    public static Config? GetCachedConfig()
    {
        return _cachedConfig;
    }
}