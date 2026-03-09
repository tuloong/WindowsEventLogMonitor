using Microsoft.Win32;
using System;
using System.IO;
using System.ServiceProcess;

namespace WindowsEventLogMonitor.Services
{
    public enum AutoStartMode { None, Gui, Service }
    public enum AutoStartStatus { Disabled, GuiEnabled, ServiceEnabled }

    public class AutoStartService
    {
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "WindowsEventLogMonitor";

        public AutoStartStatus GetStatus()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
                if (key == null) return AutoStartStatus.Disabled;

                var value = key.GetValue(AppName);
                if (value != null)
                {
                    return AutoStartStatus.GuiEnabled;
                }

                if (IsServiceAutoStartEnabled())
                {
                    return AutoStartStatus.ServiceEnabled;
                }

                return AutoStartStatus.Disabled;
            }
            catch
            {
                return AutoStartStatus.Disabled;
            }
        }

        private bool IsServiceAutoStartEnabled()
        {
            try
            {
                using var service = new ServiceController("SqlServerLogMonitor");
                return service.StartType == ServiceStartMode.Automatic;
            }
            catch
            {
                return false;
            }
        }

        public bool EnableGuiAutoStart(bool minimizeToTray = true)
        {
            try
            {
                // 先禁用服务模式（如果之前设置了）
                DisableServiceAutoStart();

                // 获取可执行文件路径
                var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                        "WindowsEventLogMonitor.exe");
                }

                // 构建启动参数
                var arguments = minimizeToTray ? "--autostart-gui --minimize" : "--autostart-gui";
                var command = $"\"{exePath}\" {arguments}";

                // 写入注册表
                using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
                key?.SetValue(AppName, command);

                // 更新配置
                var config = Config.GetCachedConfig() ?? new Config();
                config.AutoStart.Enabled = true;
                config.AutoStart.Mode = AutoStartMode.Gui;
                config.AutoStart.MinimizeToTray = minimizeToTray;
                Config.SaveConfig(config);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutoStartService] 启用 GUI 自启动失败: {ex.Message}");
                return false;
            }
        }

        public bool DisableAutoStart()
        {
            try
            {
                // 删除注册表项
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                if (key != null && key.GetValue(AppName) != null)
                {
                    key.DeleteValue(AppName);
                }

                // 禁用服务自动启动
                DisableServiceAutoStart();

                // 更新配置
                var config = Config.GetCachedConfig() ?? new Config();
                config.AutoStart.Enabled = false;
                config.AutoStart.Mode = AutoStartMode.None;
                Config.SaveConfig(config);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutoStartService] 禁用自启动失败: {ex.Message}");
                return false;
            }
        }

        private void DisableServiceAutoStart()
        {
            // Task 6 将实现
        }
    }
}
