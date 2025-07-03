using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WindowsEventLogMonitor;

internal class Config
{
    public string ApiUrl { get; set; } = "http://localhost:5000/api/aa/WindowsEventMonitor/SaveEventLog";
    public SqlServerMonitoringConfig SqlServerMonitoring { get; set; } = new();
    public RetryPolicyConfig RetryPolicy { get; set; } = new();
    public LogRetentionConfig LogRetention { get; set; } = new();
    public SecurityConfig Security { get; set; } = new();

    private static Config? cachedConfig;

    public static void SaveConfig(Config config)
    {
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText("config.json", json);
        cachedConfig = config;
    }

    public static Config? GetCachedConfig()
    {
        if (cachedConfig == null)
        {
            LoadConfig();
        }
        return cachedConfig;
    }

    private static void LoadConfig()
    {
        try
        {
            if (File.Exists("config.json"))
            {
                var json = File.ReadAllText("config.json");
                cachedConfig = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
            }
            else
            {
                cachedConfig = new Config();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载配置文件失败: {ex.Message}，使用默认配置");
            cachedConfig = new Config();
        }
    }
}

public class SqlServerMonitoringConfig
{
    public bool Enabled { get; set; } = true;
    public int MonitorIntervalSeconds { get; set; } = 30;
    public int UIRefreshIntervalSeconds { get; set; } = 10;
    public int BatchSize { get; set; } = 10;
    public bool IncludeMSSQLSERVER { get; set; } = true;
    public bool IncludeWindowsAuth { get; set; } = true;
    public EventIdsConfig EventIds { get; set; } = new();
}

public class EventIdsConfig
{
    public int SQLLoginSuccess { get; set; } = 18453;
    public int SQLLoginFailure { get; set; } = 18456;
    public int WindowsLoginSuccess { get; set; } = 4624;
    public int WindowsLoginFailure { get; set; } = 4625;
}

public class RetryPolicyConfig
{
    public int MaxRetries { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 5;
    public bool EnableRetry { get; set; } = true;
}

public class LogRetentionConfig
{
    public int RetentionDays { get; set; } = 7;
    public int MaxLogFileSizeKB { get; set; } = 500;
}

public class SecurityConfig
{
    public bool UseHttps { get; set; } = false;
    public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
}