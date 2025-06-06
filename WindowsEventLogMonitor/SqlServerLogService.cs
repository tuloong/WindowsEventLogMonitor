using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace WindowsEventLogMonitor;

/// <summary>
/// SQL Server 日志监控 Windows 服务
/// </summary>
public class SqlServerLogService : ServiceBase
{
    private SqlServerLogMonitor logMonitor;
    private Config config;
    private CancellationTokenSource cancellationTokenSource;
    private Task monitoringTask;
    private readonly string logFile = "service_log.txt";

    public SqlServerLogService()
    {
        ServiceName = "SqlServerLogMonitor";
        CanStop = true;
        CanPauseAndContinue = false;
        AutoLog = true;
    }

    /// <summary>
    /// 服务启动
    /// </summary>
    protected override void OnStart(string[] args)
    {
        try
        {
            WriteLog("SQL Server日志监控服务正在启动...");

            // 加载配置
            config = Config.GetCachedConfig() ?? new Config();

            if (!config.SqlServerMonitoring.Enabled)
            {
                WriteLog("SQL Server监控已禁用，服务将停止");
                Stop();
                return;
            }

            // 初始化监控器
            logMonitor = new SqlServerLogMonitor();
            cancellationTokenSource = new CancellationTokenSource();

            // 启动监控任务
            monitoringTask = Task.Run(async () => await StartMonitoringLoop(), cancellationTokenSource.Token);

            WriteLog("SQL Server日志监控服务已成功启动");
        }
        catch (Exception ex)
        {
            WriteLog($"服务启动失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 服务停止
    /// </summary>
    protected override void OnStop()
    {
        try
        {
            WriteLog("SQL Server日志监控服务正在停止...");

            cancellationTokenSource?.Cancel();
            logMonitor?.StopMonitoring();

            // 等待监控任务完成（最多10秒）
            if (monitoringTask != null)
            {
                monitoringTask.Wait(TimeSpan.FromSeconds(10));
            }

            WriteLog("SQL Server日志监控服务已停止");
        }
        catch (Exception ex)
        {
            WriteLog($"服务停止时发生错误: {ex.Message}");
        }
        finally
        {
            cancellationTokenSource?.Dispose();
        }
    }

    /// <summary>
    /// 监控循环
    /// </summary>
    private async Task StartMonitoringLoop()
    {
        while (!cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                await logMonitor.CollectAndPushSQLServerLogsAsync(config.ApiUrl);

                // 等待指定的间隔时间
                await Task.Delay(
                    TimeSpan.FromSeconds(config.SqlServerMonitoring.MonitorIntervalSeconds),
                    cancellationTokenSource.Token
                );
            }
            catch (OperationCanceledException)
            {
                // 正常取消，退出循环
                break;
            }
            catch (Exception ex)
            {
                WriteLog($"监控过程中发生错误: {ex.Message}");

                // 发生错误时等待较短时间再重试
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        WriteLog("监控循环已退出");
    }

    /// <summary>
    /// 写入日志文件
    /// </summary>
    private void WriteLog(string message)
    {
        try
        {
            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            File.AppendAllText(logFile, logMessage + Environment.NewLine);

            // 保持日志文件大小在合理范围内
            ManageLogFileSize();
        }
        catch
        {
            // 忽略日志写入错误，避免影响主要功能
        }
    }

    /// <summary>
    /// 管理日志文件大小
    /// </summary>
    private void ManageLogFileSize()
    {
        try
        {
            if (File.Exists(logFile))
            {
                var fileInfo = new FileInfo(logFile);
                var maxSizeBytes = config?.LogRetention?.MaxLogFileSizeKB * 1024 ?? 500 * 1024;

                if (fileInfo.Length > maxSizeBytes)
                {
                    // 保留最后一半的内容
                    var lines = File.ReadAllLines(logFile);
                    var keepLines = lines.Skip(lines.Length / 2).ToArray();
                    File.WriteAllLines(logFile, keepLines);
                }
            }
        }
        catch
        {
            // 忽略日志管理错误
        }
    }

    /// <summary>
    /// 用于控制台模式运行（调试时使用）
    /// </summary>
    public void RunAsConsole()
    {
        Console.WriteLine("按 Ctrl+C 退出...");

        OnStart(null);

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            OnStop();
        };

        // 保持控制台应用程序运行
        while (!cancellationTokenSource.Token.IsCancellationRequested)
        {
            Thread.Sleep(1000);
        }
    }
}

/// <summary>
/// 服务安装器
/// </summary>
public static class ServiceInstaller
{
    /// <summary>
    /// 安装服务
    /// </summary>
    public static void InstallService()
    {
        try
        {
            var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var installCommand = $"sc create SqlServerLogMonitor binPath= \"{exePath}\" start= auto";

            var processInfo = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c {installCommand}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = System.Diagnostics.Process.Start(processInfo))
            {
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    Console.WriteLine("服务安装成功");
                    Console.WriteLine("请使用以下命令启动服务:");
                    Console.WriteLine("net start SqlServerLogMonitor");
                }
                else
                {
                    Console.WriteLine($"服务安装失败: {error}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"安装服务时发生错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 卸载服务
    /// </summary>
    public static void UninstallService()
    {
        try
        {
            var uninstallCommand = "sc delete SqlServerLogMonitor";

            var processInfo = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c {uninstallCommand}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = System.Diagnostics.Process.Start(processInfo))
            {
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    Console.WriteLine("服务卸载成功");
                }
                else
                {
                    Console.WriteLine($"服务卸载失败: {error}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"卸载服务时发生错误: {ex.Message}");
        }
    }
}