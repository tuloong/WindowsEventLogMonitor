using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WindowsEventLogMonitor.Services;

namespace WindowsEventLogMonitor;

public class Config
{
    public string ApiUrl { get; set; } = "https://localhost:5000/api/aa/WindowsEventMonitor/SaveEventLog";
    public SqlServerMonitoringConfig SqlServerMonitoring { get; set; } = new();
    public RetryPolicyConfig RetryPolicy { get; set; } = new();
    public LogRetentionConfig LogRetention { get; set; } = new();
    public SecurityConfig Security { get; set; } = new();
    public AutoStartConfig AutoStart { get; set; } = new();

    private static Config? cachedConfig;
    private static readonly object configLock = new object();

    /// <summary>
    /// 获取配置文件路径（程序所在目录）
    /// </summary>
    private static string GetConfigFilePath()
    {
        var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var exeDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(exeDir, "config.json");
    }

    public static void SaveConfig(Config config)
    {
        var configPath = GetConfigFilePath();
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        lock (configLock)
        {
            File.WriteAllText(configPath, json);
            cachedConfig = config;
        }
    }

    public static Config? GetCachedConfig()
    {
        if (cachedConfig == null)
        {
            lock (configLock)
            {
                if (cachedConfig == null)
                {
                    LoadConfig();
                }
            }
        }
        return cachedConfig;
    }

    private static void LoadConfig()
    {
        try
        {
            var configPath = GetConfigFilePath();
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
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
    
    /// <summary>
    /// UI显示的最大日志数量（避免界面卡顿）
    /// </summary>
    public int MaxDisplayLogs { get; set; } = 500;
    
    /// <summary>
    /// 内存缓存的最大日志数量（避免内存溢出）
    /// </summary>
    public int MaxCacheLogs { get; set; } = 1000;
    
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
    public bool UseHttps { get; set; } = true;
    public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
}

public class AutoStartConfig
{
    /// <summary>
    /// 是否启用开机自启动
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 自启动模式：GUI 或 Service
    /// </summary>
    public AutoStartMode Mode { get; set; } = AutoStartMode.Gui;

    /// <summary>
    /// 自启动方式：注册表、任务计划、启动文件夹
    /// </summary>
    public AutoStartMethod Method { get; set; } = AutoStartMethod.Registry;

    /// <summary>
    /// GUI 模式下启动时是否最小化到系统托盘
    /// </summary>
    public bool MinimizeToTray { get; set; } = true;
}