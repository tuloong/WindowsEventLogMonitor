using System;
using System.Linq;
using System.ServiceProcess;
using System.Windows.Forms;

namespace WindowsEventLogMonitor
{
    internal static class Program
    {
        /// <summary>
        ///  应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // 解析命令行参数
            if (args.Length > 0)
            {
                var command = args[0].ToLower();

                switch (command)
                {
                    case "install":
                        ServiceInstaller.InstallService();
                        return;

                    case "uninstall":
                        ServiceInstaller.UninstallService();
                        return;

                    case "console":
                        RunAsConsole();
                        return;

                    case "service":
                        RunAsService();
                        return;

                    case "test-logid":
                        LogIdTestTool.TestLogIdExtraction();
                        return;

                    case "analyze-duplicates":
                        var logType = args.Length > 1 ? args[1] : "sql_server_push_log";
                        LogIdTestTool.AnalyzeDuplicateIds(logType);
                        return;

                    case "cleanup-duplicates":
                        var cleanupLogType = args.Length > 1 ? args[1] : "sql_server_push_log";
                        Console.WriteLine("警告：此操作将删除重复的日志记录！");
                        Console.Write("确认继续？(y/N): ");
                        var confirm = Console.ReadLine();
                        if (confirm?.ToLower() == "y" || confirm?.ToLower() == "yes")
                        {
                            LogIdTestTool.CleanupDuplicateRecords(cleanupLogType);
                        }
                        else
                        {
                            Console.WriteLine("操作已取消");
                        }
                        return;

                    case "--help":
                    case "-h":
                        ShowHelp();
                        return;

                    default:
                        Console.WriteLine($"未知参数: {command}");
                        ShowHelp();
                        return;
                }
            }

            // 默认以图形界面模式运行
            RunAsGui();
        }

        /// <summary>
        /// 以图形界面模式运行
        /// </summary>
        private static void RunAsGui()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }

        /// <summary>
        /// 以Windows服务模式运行
        /// </summary>
        private static void RunAsService()
        {
            try
            {
                var servicesToRun = new ServiceBase[]
                {
                    new SqlServerLogService()
                };

                ServiceBase.Run(servicesToRun);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"服务运行失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 以控制台模式运行（调试用）
        /// </summary>
        private static void RunAsConsole()
        {
            Console.WriteLine("=== SQL Server 日志监控器 (控制台模式) ===");
            Console.WriteLine("正在启动监控服务...");

            try
            {
                var service = new SqlServerLogService();
                service.RunAsConsole();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"控制台模式运行失败: {ex.Message}");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// 显示帮助信息
        /// </summary>
        private static void ShowHelp()
        {
            Console.WriteLine("SQL Server 日志监控器 v2.0");
            Console.WriteLine("");
            Console.WriteLine("用法:");
            Console.WriteLine("  WindowsEventLogMonitor.exe                          - 图形界面模式");
            Console.WriteLine("  WindowsEventLogMonitor.exe install                  - 安装Windows服务");
            Console.WriteLine("  WindowsEventLogMonitor.exe uninstall                - 卸载Windows服务");
            Console.WriteLine("  WindowsEventLogMonitor.exe service                  - 以服务模式运行");
            Console.WriteLine("  WindowsEventLogMonitor.exe console                  - 以控制台模式运行");
            Console.WriteLine("  WindowsEventLogMonitor.exe test-logid               - 测试日志ID提取功能");
            Console.WriteLine("  WindowsEventLogMonitor.exe analyze-duplicates [type] - 分析重复的日志记录");
            Console.WriteLine("  WindowsEventLogMonitor.exe cleanup-duplicates [type] - 清理重复的日志记录");
            Console.WriteLine("  WindowsEventLogMonitor.exe --help                   - 显示此帮助信息");
            Console.WriteLine("");
            Console.WriteLine("参数说明:");
            Console.WriteLine("  [type] - 日志类型，可选值: sql_server_push_log, push_log (默认: sql_server_push_log)");
            Console.WriteLine("");
            Console.WriteLine("注意:");
            Console.WriteLine("- 安装/卸载服务需要管理员权限");
            Console.WriteLine("- 服务模式由Windows服务管理器控制");
            Console.WriteLine("- 控制台模式用于调试和测试");
            Console.WriteLine("- 配置文件: config.json");
            Console.WriteLine("- cleanup-duplicates 会备份原文件");
            Console.WriteLine("");
            Console.WriteLine("示例:");
            Console.WriteLine("  # 安装并启动服务");
            Console.WriteLine("  WindowsEventLogMonitor.exe install");
            Console.WriteLine("  net start SqlServerLogMonitor");
            Console.WriteLine("");
            Console.WriteLine("  # 调试模式运行");
            Console.WriteLine("  WindowsEventLogMonitor.exe console");
            Console.WriteLine("");
            Console.WriteLine("  # 测试和修复重复日志问题");
            Console.WriteLine("  WindowsEventLogMonitor.exe test-logid");
            Console.WriteLine("  WindowsEventLogMonitor.exe analyze-duplicates");
            Console.WriteLine("  WindowsEventLogMonitor.exe cleanup-duplicates sql_server_push_log");
        }
    }
}