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
    }
}
