using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace WindowsEventLogMonitor.Services
{
    public enum AutoStartMode { None, Gui }
    public enum AutoStartStatus { Disabled, GuiEnabled }
    
    /// <summary>
    /// 自启动方式
    /// </summary>
    public enum AutoStartMethod
    {
        /// <summary>
        /// 注册表 Run 键（默认，适用于大多数 Windows 系统）
        /// </summary>
        Registry,
        
        /// <summary>
        /// 任务计划程序（适用于 Windows Server 和企业版）
        /// </summary>
        TaskScheduler,
        
        /// <summary>
        /// 启动文件夹快捷方式（备用方案）
        /// </summary>
        StartupFolder
    }

    public class AutoStartService
    {
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "WindowsEventLogMonitor";
        private const string TaskName = "WindowsEventLogMonitor_AutoStart";

        #region Get Executable Path Helper
        
        private string GetExecutablePath()
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
            {
                exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WindowsEventLogMonitor.exe");
            }
            return exePath;
        }
        
        private string GetStartupFolderPath()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        }
        
        private string GetShortcutPath()
        {
            return Path.Combine(GetStartupFolderPath(), $"{AppName}.lnk");
        }
        
        #endregion

        #region Status Methods
        
        /// <summary>
        /// 获取指定方式的自启动状态
        /// </summary>
        public AutoStartStatus GetStatus(AutoStartMethod method)
        {
            return method switch
            {
                AutoStartMethod.Registry => GetRegistryStatus(),
                AutoStartMethod.TaskScheduler => GetTaskSchedulerStatus(),
                AutoStartMethod.StartupFolder => GetStartupFolderStatus(),
                _ => AutoStartStatus.Disabled
            };
        }

        /// <summary>
        /// 获取注册表方式的状态（原有方法，保持向后兼容）
        /// </summary>
        public AutoStartStatus GetStatus()
        {
            return GetRegistryStatus();
        }
        
        private AutoStartStatus GetRegistryStatus()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
                if (key == null) return AutoStartStatus.Disabled;

                var value = key.GetValue(AppName);
                return value != null ? AutoStartStatus.GuiEnabled : AutoStartStatus.Disabled;
            }
            catch
            {
                return AutoStartStatus.Disabled;
            }
        }
        
        private AutoStartStatus GetTaskSchedulerStatus()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = $"/query /tn \"{TaskName}\" /fo LIST",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(startInfo);
                process?.WaitForExit(5000);
                
                return process?.ExitCode == 0 ? AutoStartStatus.GuiEnabled : AutoStartStatus.Disabled;
            }
            catch
            {
                return AutoStartStatus.Disabled;
            }
        }
        
        private AutoStartStatus GetStartupFolderStatus()
        {
            try
            {
                return File.Exists(GetShortcutPath()) ? AutoStartStatus.GuiEnabled : AutoStartStatus.Disabled;
            }
            catch
            {
                return AutoStartStatus.Disabled;
            }
        }
        
        #endregion

        #region Enable Methods
        
        /// <summary>
        /// 启用指定方式的自启动
        /// </summary>
        public bool EnableAutoStart(AutoStartMethod method, bool minimizeToTray = true)
        {
            return method switch
            {
                AutoStartMethod.Registry => EnableRegistryAutoStart(minimizeToTray),
                AutoStartMethod.TaskScheduler => EnableTaskSchedulerAutoStart(minimizeToTray),
                AutoStartMethod.StartupFolder => EnableStartupFolderAutoStart(minimizeToTray),
                _ => false
            };
        }

        /// <summary>
        /// 启用 GUI 自启动（注册表方式，保持向后兼容）
        /// </summary>
        public bool EnableGuiAutoStart(bool minimizeToTray = true)
        {
            return EnableRegistryAutoStart(minimizeToTray);
        }
        
        private bool EnableRegistryAutoStart(bool minimizeToTray)
        {
            try
            {
                var exePath = GetExecutablePath();
                var arguments = minimizeToTray ? "--autostart-gui --minimize" : "--autostart-gui";
                var command = $"\"{exePath}\" {arguments}";

                using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
                key?.SetValue(AppName, command);

                UpdateConfig(true, AutoStartMethod.Registry, minimizeToTray);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoStartService] 启用注册表自启动失败: {ex.Message}");
                return false;
            }
        }
        
        private bool EnableTaskSchedulerAutoStart(bool minimizeToTray)
        {
            try
            {
                // 先删除现有任务（如果存在）
                DisableTaskSchedulerAutoStart();
                
                var exePath = GetExecutablePath();
                var arguments = minimizeToTray ? "--autostart-gui --minimize" : "--autostart-gui";
                
                // 使用 schtasks 创建任务
                // /sc onlogon - 用户登录时启动
                // /rl highest - 以最高权限运行（可选，可以帮助绕过某些限制）
                var startInfo = new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = $"/create /tn \"{TaskName}\" /tr \"\\\"{exePath}\\\" {arguments}\" /sc onlogon /rl highest /f",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(startInfo);
                var output = process?.StandardOutput.ReadToEnd();
                var error = process?.StandardError.ReadToEnd();
                process?.WaitForExit(10000);
                
                if (process?.ExitCode == 0)
                {
                    UpdateConfig(true, AutoStartMethod.TaskScheduler, minimizeToTray);
                    Debug.WriteLine($"[AutoStartService] 任务计划自启动已创建: {output}");
                    return true;
                }
                else
                {
                    Debug.WriteLine($"[AutoStartService] 创建任务计划失败: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoStartService] 启用任务计划自启动失败: {ex.Message}");
                return false;
            }
        }
        
        private bool EnableStartupFolderAutoStart(bool minimizeToTray)
        {
            try
            {
                var exePath = GetExecutablePath();
                var arguments = minimizeToTray ? "--autostart-gui --minimize" : "--autostart-gui";
                var shortcutPath = GetShortcutPath();
                
                // 使用 PowerShell 创建快捷方式（避免依赖 COM 组件）
                var psScript = $@"
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut('{shortcutPath}')
$Shortcut.TargetPath = '{exePath}'
$Shortcut.Arguments = '{arguments}'
$Shortcut.WorkingDirectory = '{Path.GetDirectoryName(exePath)}'
$Shortcut.Save()
";
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript.Replace("\"", "\\\"")}\"" ,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(startInfo);
                process?.WaitForExit(10000);
                
                if (File.Exists(shortcutPath))
                {
                    UpdateConfig(true, AutoStartMethod.StartupFolder, minimizeToTray);
                    Debug.WriteLine($"[AutoStartService] 启动文件夹快捷方式已创建: {shortcutPath}");
                    return true;
                }
                else
                {
                    Debug.WriteLine("[AutoStartService] 创建快捷方式失败");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoStartService] 启用启动文件夹自启动失败: {ex.Message}");
                return false;
            }
        }
        
        #endregion

        #region Disable Methods
        
        /// <summary>
        /// 禁用指定方式的自启动
        /// </summary>
        public bool DisableAutoStart(AutoStartMethod method)
        {
            return method switch
            {
                AutoStartMethod.Registry => DisableRegistryAutoStart(),
                AutoStartMethod.TaskScheduler => DisableTaskSchedulerAutoStart(),
                AutoStartMethod.StartupFolder => DisableStartupFolderAutoStart(),
                _ => false
            };
        }

        /// <summary>
        /// 禁用注册表自启动（保持向后兼容）
        /// </summary>
        public bool DisableAutoStart()
        {
            return DisableRegistryAutoStart();
        }
        
        /// <summary>
        /// 禁用所有方式的自启动
        /// </summary>
        public bool DisableAllAutoStart()
        {
            var r1 = DisableRegistryAutoStart();
            var r2 = DisableTaskSchedulerAutoStart();
            var r3 = DisableStartupFolderAutoStart();
            
            UpdateConfig(false, AutoStartMethod.Registry, false);
            
            return r1 && r2 && r3;
        }
        
        private bool DisableRegistryAutoStart()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                if (key != null && key.GetValue(AppName) != null)
                {
                    key.DeleteValue(AppName);
                }
                
                // 更新配置
                UpdateConfig(false, AutoStartMethod.Registry, false);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoStartService] 禁用注册表自启动失败: {ex.Message}");
                return false;
            }
        }
        
        private bool DisableTaskSchedulerAutoStart()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = $"/delete /tn \"{TaskName}\" /f",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(startInfo);
                process?.WaitForExit(5000);
                
                // 即使任务不存在，也认为成功
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoStartService] 禁用任务计划自启动失败: {ex.Message}");
                return false;
            }
        }
        
        private bool DisableStartupFolderAutoStart()
        {
            try
            {
                var shortcutPath = GetShortcutPath();
                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoStartService] 禁用启动文件夹自启动失败: {ex.Message}");
                return false;
            }
        }
        
        #endregion

        #region Config Helper
        
        private void UpdateConfig(bool enabled, AutoStartMethod method, bool minimizeToTray)
        {
            var config = Config.GetCachedConfig() ?? new Config();
            config.AutoStart.Enabled = enabled;
            config.AutoStart.Mode = enabled ? AutoStartMode.Gui : AutoStartMode.None;
            config.AutoStart.Method = method;
            config.AutoStart.MinimizeToTray = minimizeToTray;
            Config.SaveConfig(config);
        }
        
        #endregion
    }
}
