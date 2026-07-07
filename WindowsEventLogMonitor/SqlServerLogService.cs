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
    private readonly string serviceLogType = "service_log";

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
            // 注意：OnStart 必须快速返回，否则 SCM 会认为服务无响应
            // 所有初始化工作都在后台线程中完成
            WriteLog("SQL Server日志监控服务正在启动...");

            // 在后台线程中完成初始化，避免阻塞 OnStart
            Task.Run(() => InitializeAndStartMonitoring());

            WriteLog("SQL Server日志监控服务启动中...");
        }
        catch (Exception ex)
        {
            WriteLog($"服务启动失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 初始化并启动监控（在后台线程中执行）
    /// </summary>
    private void InitializeAndStartMonitoring()
    {
        try
        {
            // 初始化日志文件管理器
            LogFileManager.Initialize();

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
            WriteLog($"服务初始化失败: {ex.Message}");
            // 服务初始化失败，但 OnStart 已经返回，记录错误并退出
            Environment.Exit(1);
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
            LogFileManager.WriteLogEntry(serviceLogType, message);
        }
        catch
        {
            // 忽略日志写入错误，避免影响主要功能
        }
    }

    /// <summary>
    /// 管理日志文件大小（现在由LogFileManager自动处理）
    /// </summary>
    private void ManageLogFileSize()
    {
        // 日志文件管理现在由LogFileManager自动处理
        // 这个方法保留为空以保持兼容性
    }

    /// <summary>
    /// 用于控制台模式运行（调试时使用）
    /// </summary>
    public void RunAsConsole()
    {
        Console.WriteLine("按 Ctrl+C 退出...");

        // 先初始化 cancellationTokenSource，避免 RunAsConsole 模式下 OnStart 的后台初始化竞态导致空引用
        cancellationTokenSource = new CancellationTokenSource();
        var runLoop = true;

        OnStart(null);

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            runLoop = false;
            try { OnStop(); } catch { /* 已停止则忽略 */ }
        };

        // 保持控制台应用程序运行
        // 使用本地 bool 标志控制循环，避免 cancellationTokenSource 被 OnStop Dispose 后访问 .Token 抛 ObjectDisposedException
        while (runLoop)
        {
            Thread.Sleep(1000);
            // 若 OnStart 内部因配置禁用调用了 Stop()，cancellationTokenSource 可能已被 Dispose
            try
            {
                if (cancellationTokenSource == null || cancellationTokenSource.IsCancellationRequested)
                {
                    break;
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }
}