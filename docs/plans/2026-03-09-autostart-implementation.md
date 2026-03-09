# 自启动管理功能实现计划

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 为 WindowsEventLogMonitor 添加 Windows 开机自启动管理功能，支持 GUI 模式和服务模式，提供 UI 和命令行两种控制方式。

**Architecture:** 使用注册表 HKCU 存储 GUI 模式自启动项，使用 Windows 服务管理器控制服务模式启动类型。核心逻辑封装在 AutoStartService 类中，配置存储在 config.json 的 AutoStartConfig 中。

**Tech Stack:** C# .NET 8, Windows Forms, Microsoft.Win32 (Registry), System.ServiceProcess

---

## 前置条件

- [ ] 确认项目可以正常编译 (`dotnet build`)
- [ ] 确认 Config 类已存在且可正常序列化/反序列化
- [ ] 确认 MainForm 有配置页面 (tabPageConfiguration)

---

## Task 1: 创建 AutoStartService 核心类

**Files:**
- Create: `WindowsEventLogMonitor/Services/AutoStartService.cs`

**Step 1: 创建枚举类型**

```csharp
namespace WindowsEventLogMonitor.Services
{
    public enum AutoStartMode { None, Gui, Service }
    public enum AutoStartStatus { Disabled, GuiEnabled, ServiceEnabled }
}
```

**Step 2: 创建 AutoStartService 类框架**

```csharp
using Microsoft.Win32;
using System;
using System.IO;
using System.ServiceProcess;

namespace WindowsEventLogMonitor.Services
{
    public class AutoStartService
    {
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "WindowsEventLogMonitor";

        /// <summary>
        /// 获取当前自启动状态
        /// </summary>
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

                // 检查服务模式
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

        /// <summary>
        /// 检查服务是否设置为自动启动
        /// </summary>
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
```

**Step 3: 编译验证**

Run: `dotnet build WindowsEventLogMonitor/WindowsEventLogMonitor.csproj`
Expected: PASS (0 errors)

**Step 4: Commit**

```bash
git add WindowsEventLogMonitor/Services/AutoStartService.cs
git commit -m "feat: add AutoStartService with GetStatus method"
```

---

## Task 2: 实现 GUI 模式自启动功能

**Files:**
- Modify: `WindowsEventLogMonitor/Services/AutoStartService.cs`

**Step 1: 添加 EnableGuiAutoStart 方法**

在 AutoStartService 类中添加：

```csharp
/// <summary>
/// 启用 GUI 模式自启动
/// </summary>
/// <param name="minimizeToTray">启动时是否最小化到托盘</param>
/// <returns>是否成功</returns>
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
```

**Step 2: 添加 DisableAutoStart 方法（GUI 部分）**

```csharp
/// <summary>
/// 禁用自启动
/// </summary>
/// <returns>是否成功</returns>
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
```

**Step 3: 编译验证**

Run: `dotnet build`
Expected: PASS

**Step 4: Commit**

```bash
git commit -am "feat: implement GUI mode auto-start functionality"
```

---

## Task 3: 实现服务模式自启动功能

**Files:**
- Modify: `WindowsEventLogMonitor/Services/AutoStartService.cs`

**Step 1: 添加 EnableServiceAutoStart 方法**

```csharp
/// <summary>
/// 启用服务模式自启动
/// </summary>
/// <returns>是否成功</returns>
public bool EnableServiceAutoStart()
{
    try
    {
        // 检查服务是否已安装
        if (!IsServiceInstalled())
        {
            System.Diagnostics.Debug.WriteLine("[AutoStartService] 服务未安装");
            return false;
        }

        // 先禁用 GUI 自启动
        DisableGuiAutoStart();

        // 设置服务启动类型为自动
        using var service = new ServiceController("SqlServerLogMonitor");
        // 注意：修改服务启动类型需要管理员权限
        // 这里使用 sc.exe 命令
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = "config SqlServerLogMonitor start= auto",
            Verb = "runas", // 请求提升权限
            UseShellExecute = true,
            CreateNoWindow = true
        };

        try
        {
            var process = System.Diagnostics.Process.Start(startInfo);
            process?.WaitForExit(5000);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // 用户取消 UAC 提示
            System.Diagnostics.Debug.WriteLine("[AutoStartService] 用户取消权限提升");
            return false;
        }

        // 更新配置
        var config = Config.GetCachedConfig() ?? new Config();
        config.AutoStart.Enabled = true;
        config.AutoStart.Mode = AutoStartMode.Service;
        Config.SaveConfig(config);

        return true;
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[AutoStartService] 启用服务自启动失败: {ex.Message}");
        return false;
    }
}

/// <summary>
/// 检查服务是否已安装
/// </summary>
private bool IsServiceInstalled()
{
    try
    {
        using var service = new ServiceController("SqlServerLogMonitor");
        var status = service.Status;
        return true;
    }
    catch
    {
        return false;
    }
}

/// <summary>
/// 禁用 GUI 自启动（仅删除注册表，不修改配置）
/// </summary>
private void DisableGuiAutoStart()
{
    try
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
        if (key != null && key.GetValue(AppName) != null)
        {
            key.DeleteValue(AppName);
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[AutoStartService] 禁用 GUI 自启动失败: {ex.Message}");
    }
}

/// <summary>
/// 禁用服务自启动
/// </summary>
private void DisableServiceAutoStart()
{
    try
    {
        if (!IsServiceInstalled()) return;

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = "config SqlServerLogMonitor start= demand",
            Verb = "runas",
            UseShellExecute = true,
            CreateNoWindow = true
        };

        var process = System.Diagnostics.Process.Start(startInfo);
        process?.WaitForExit(5000);
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[AutoStartService] 禁用服务自启动失败: {ex.Message}");
    }
}
```

**Step 2: 编译验证**

Run: `dotnet build`
Expected: PASS

**Step 3: Commit**

```bash
git commit -am "feat: implement Service mode auto-start functionality"
```

---

## Task 4: 扩展配置类添加 AutoStartConfig

**Files:**
- Modify: `WindowsEventLogMonitor/Config.cs`

**Step 1: 添加 AutoStartConfig 类**

在 Config.cs 文件末尾添加：

```csharp
/// <summary>
/// 自启动配置
/// </summary>
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
    /// GUI 模式下启动时是否最小化到系统托盘
    /// </summary>
    public bool MinimizeToTray { get; set; } = true;
}

/// <summary>
/// 自启动模式枚举
/// </summary>
public enum AutoStartMode
{
    None,
    Gui,
    Service
}
```

**Step 2: 在 Config 类中添加 AutoStart 属性**

在 Config 类中添加：

```csharp
/// <summary>
/// 自启动配置
/// </summary>
public AutoStartConfig AutoStart { get; set; } = new AutoStartConfig();
```

**Step 3: 编译验证**

Run: `dotnet build`
Expected: PASS

**Step 4: Commit**

```bash
git commit -am "feat: add AutoStartConfig to Config class"
```

---

## Task 5: 在 MainForm 配置页面添加 UI 控件

**Files:**
- Modify: `WindowsEventLogMonitor/MainForm.Designer.cs`

**Step 1: 在 InitializeComponent 中添加自启动控件初始化调用**

找到 InitializeConfigurationTab 调用，在其后添加：

```csharp
InitializeAutoStartControls();
```

**Step 2: 添加 InitializeAutoStartControls 方法**

在 MainForm.Designer.cs 中添加新方法：

```csharp
private void InitializeAutoStartControls()
{
    // 自启动设置分组框
    var groupBoxAutoStart = new GroupBox
    {
        Text = "自启动设置",
        Location = new Point(20, 400),
        Size = new Size(600, 150)
    };

    // 启用自启动复选框
    checkBoxEnableAutoStart = new CheckBox
    {
        Text = "开机自动启动",
        Location = new Point(20, 30),
        AutoSize = true
    };
    checkBoxEnableAutoStart.CheckedChanged += CheckBoxEnableAutoStart_CheckedChanged;

    // GUI 模式单选按钮
    radioButtonGuiMode = new RadioButton
    {
        Text = "以图形界面模式启动",
        Location = new Point(40, 55),
        AutoSize = true,
        Enabled = false
    };
    radioButtonGuiMode.CheckedChanged += RadioButtonGuiMode_CheckedChanged;

    // 最小化到托盘复选框
    checkBoxMinimizeToTray = new CheckBox
    {
        Text = "启动时最小化到系统托盘",
        Location = new Point(60, 80),
        AutoSize = true,
        Enabled = false
    };

    // 服务模式单选按钮
    radioButtonServiceMode = new RadioButton
    {
        Text = "以服务模式启动",
        Location = new Point(40, 105),
        AutoSize = true,
        Enabled = false
    };

    // 服务模式提示标签
    var lblServiceHint = new Label
    {
        Text = "（需先安装 Windows 服务）",
        Location = new Point(160, 107),
        AutoSize = true,
        ForeColor = Color.Gray,
        Font = new Font(this.Font.FontFamily, 8)
    };

    groupBoxAutoStart.Controls.AddRange(new Control[]
    {
        checkBoxEnableAutoStart,
        radioButtonGuiMode,
        checkBoxMinimizeToTray,
        radioButtonServiceMode,
        lblServiceHint
    });

    tabPageConfiguration.Controls.Add(groupBoxAutoStart);
}
```

**Step 3: 在类成员声明区域添加控件字段**

```csharp
// 自启动控件
private CheckBox checkBoxEnableAutoStart;
private RadioButton radioButtonGuiMode;
private CheckBox checkBoxMinimizeToTray;
private RadioButton radioButtonServiceMode;
```

**Step 4: 编译验证**

Run: `dotnet build`
Expected: PASS

**Step 5: Commit**

```bash
git commit -am "feat: add auto-start UI controls to configuration page"
```

---

## Task 6: 实现 UI 事件处理逻辑

**Files:**
- Modify: `WindowsEventLogMonitor/MainForm.cs`

**Step 1: 添加事件处理方法**

```csharp
/// <summary>
/// 启用自启动复选框状态改变
/// </summary>
private void CheckBoxEnableAutoStart_CheckedChanged(object sender, EventArgs e)
{
    var enabled = checkBoxEnableAutoStart.Checked;
    radioButtonGuiMode.Enabled = enabled;
    radioButtonServiceMode.Enabled = enabled;

    if (enabled)
    {
        checkBoxMinimizeToTray.Enabled = radioButtonGuiMode.Checked;

        // 默认选中 GUI 模式
        if (!radioButtonGuiMode.Checked && !radioButtonServiceMode.Checked)
        {
            radioButtonGuiMode.Checked = true;
        }
    }
    else
    {
        checkBoxMinimizeToTray.Enabled = false;
    }
}

/// <summary>
/// GUI 模式单选按钮状态改变
/// </summary>
private void RadioButtonGuiMode_CheckedChanged(object sender, EventArgs e)
{
    checkBoxMinimizeToTray.Enabled = radioButtonGuiMode.Checked;
}
```

**Step 2: 修改 LoadConfiguration 方法加载自启动配置**

找到 LoadConfiguration 方法，在末尾添加：

```csharp
// 加载自启动配置
if (config.AutoStart != null)
{
    checkBoxEnableAutoStart.Checked = config.AutoStart.Enabled;

    if (config.AutoStart.Mode == AutoStartMode.Gui)
    {
        radioButtonGuiMode.Checked = true;
    }
    else if (config.AutoStart.Mode == AutoStartMode.Service)
    {
        radioButtonServiceMode.Checked = true;
    }

    checkBoxMinimizeToTray.Checked = config.AutoStart.MinimizeToTray;
}

// 检查实际注册表/服务状态是否与配置一致
var autoStartService = new Services.AutoStartService();
var actualStatus = autoStartService.GetStatus();
if (actualStatus == Services.AutoStartStatus.Disabled && config.AutoStart.Enabled)
{
    // 配置启用但实际未启用，提示用户
    lblAutoStartStatus.Text = "自启动配置与实际状态不一致";
    lblAutoStartStatus.ForeColor = Color.Orange;
}
```

**Step 3: 在 SaveConfiguration 方法中添加自启动配置保存**

找到 SaveConfiguration 方法，在保存其他配置后添加：

```csharp
// 保存自启动配置
config.AutoStart.Enabled = checkBoxEnableAutoStart.Checked;

if (radioButtonGuiMode.Checked)
{
    config.AutoStart.Mode = AutoStartMode.Gui;
}
else if (radioButtonServiceMode.Checked)
{
    config.AutoStart.Mode = AutoStartMode.Service;
}

config.AutoStart.MinimizeToTray = checkBoxMinimizeToTray.Checked;

// 应用自启动设置
var autoStartService = new Services.AutoStartService();
bool success;

if (config.AutoStart.Enabled)
{
    if (config.AutoStart.Mode == AutoStartMode.Gui)
    {
        success = autoStartService.EnableGuiAutoStart(config.AutoStart.MinimizeToTray);
    }
    else
    {
        success = autoStartService.EnableServiceAutoStart();
    }
}
else
{
    success = autoStartService.DisableAutoStart();
}

if (!success)
{
    MessageBox.Show("自启动设置保存失败，请检查权限或以管理员身份运行。",
        "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
```

**Step 4: 添加状态标签控件（可选）**

如果需要显示自启动状态，在配置页面添加一个标签：

```csharp
private Label lblAutoStartStatus;
```

在 InitializeAutoStartControls 中添加：

```csharp
lblAutoStartStatus = new Label
{
    Location = new Point(20, 560),
    AutoSize = true,
    Text = ""
};
tabPageConfiguration.Controls.Add(lblAutoStartStatus);
```

**Step 5: 编译验证**

Run: `dotnet build`
Expected: PASS

**Step 6: Commit**

```bash
git commit -am "feat: implement auto-start UI event handlers and config binding"
```

---

## Task 7: 实现命令行参数处理

**Files:**
- Modify: `WindowsEventLogMonitor/Program.cs`

**Step 1: 添加 --autostart-gui 参数处理**

在 Main 方法的 switch 语句中添加新 case：

```csharp
case "--autostart-gui":
    RunAsGuiWithAutoStart(args);
    return;
```

**Step 2: 添加 RunAsGuiWithAutoStart 方法**

```csharp
/// <summary>
/// 以 GUI 模式启动（自启动模式）
/// </summary>
private static void RunAsGuiWithAutoStart(string[] args)
{
    // 检查是否是最小化启动
    bool minimizeToTray = args.Contains("--minimize") || args.Contains("-m");

    ApplicationConfiguration.Initialize();

    var form = new MainForm();

    if (minimizeToTray)
    {
        // 隐藏窗口，只显示托盘图标
        form.WindowState = FormWindowState.Minimized;
        form.ShowInTaskbar = false;

        // 使用 BeginInvoke 在窗体加载后最小化到托盘
        form.BeginInvoke(new Action(() =>
        {
            form.Hide();
            // 触发 SQL Server 监控自动启动（如果配置启用）
            form.StartAutoMonitoring();
        }));
    }

    Application.Run(form);
}
```

**Step 3: 添加 set-autostart-mode 命令**

```csharp
case "set-autostart-mode":
    if (args.Length > 1)
    {
        var mode = args[1].ToLower();
        var minimize = args.Contains("--minimize") || args.Contains("-m");
        SetAutoStartMode(mode, minimize);
    }
    else
    {
        Console.WriteLine("用法: WindowsEventLogMonitor.exe set-autostart-mode gui|service [--minimize]");
    }
    return;
```

**Step 4: 添加 SetAutoStartMode 方法**

```csharp
/// <summary>
/// 设置自启动模式
/// </summary>
private static void SetAutoStartMode(string mode, bool minimizeToTray)
{
    var service = new Services.AutoStartService();
    bool success;

    switch (mode)
    {
        case "gui":
            success = service.EnableGuiAutoStart(minimizeToTray);
            Console.WriteLine(success
                ? $"GUI 模式自启动已启用{(minimizeToTray ? "（最小化到托盘）" : "")}"
                : "GUI 模式自启动启用失败");
            break;

        case "service":
            success = service.EnableServiceAutoStart();
            Console.WriteLine(success
                ? "服务模式自启动已启用"
                : "服务模式自启动启用失败（请确认服务已安装）");
            break;

        default:
            Console.WriteLine("无效的模式。可用模式: gui, service");
            break;
    }
}
```

**Step 5: 添加 disable-autostart 命令**

```csharp
case "disable-autostart":
    var autoStartService = new Services.AutoStartService();
    var disabled = autoStartService.DisableAutoStart();
    Console.WriteLine(disabled ? "自启动已禁用" : "禁用自启动失败");
    return;
```

**Step 6: 添加 query-autostart 命令**

```csharp
case "query-autostart":
    var queryService = new Services.AutoStartService();
    var status = queryService.GetStatus();
    Console.WriteLine($"当前自启动状态: {status}");

    var config = Config.GetCachedConfig();
    if (config?.AutoStart != null)
    {
        Console.WriteLine($"配置状态: {(config.AutoStart.Enabled ? "启用" : "禁用")}");
        Console.WriteLine($"配置模式: {config.AutoStart.Mode}");
        Console.WriteLine($"最小化到托盘: {config.AutoStart.MinimizeToTray}");
    }
    return;
```

**Step 7: 更新 ShowHelp 方法**

在帮助信息中添加新命令说明：

```csharp
Console.WriteLine("  WindowsEventLogMonitor.exe set-autostart-mode gui [--minimize]  - 设置GUI模式自启动");
Console.WriteLine("  WindowsEventLogMonitor.exe set-autostart-mode service          - 设置服务模式自启动");
Console.WriteLine("  WindowsEventLogMonitor.exe disable-autostart                   - 禁用自启动");
Console.WriteLine("  WindowsEventLogMonitor.exe query-autostart                     - 查询自启动状态");
```

**Step 8: 编译验证**

Run: `dotnet build`
Expected: PASS

**Step 9: Commit**

```bash
git commit -am "feat: add command-line arguments for auto-start management"
```

---

## Task 8: 在 MainForm 中添加 StartAutoMonitoring 方法

**Files:**
- Modify: `WindowsEventLogMonitor/MainForm.cs`

**Step 1: 添加公共方法供 Program.cs 调用**

```csharp
/// <summary>
/// 自启动时自动开始监控
/// </summary>
public void StartAutoMonitoring()
{
    // 如果配置启用了 SQL Server 监控，自动开始
    if (config.SqlServerMonitoring.Enabled && !isSQLServerMonitoring)
    {
        BeginInvoke(new Action(() =>
        {
            try
            {
                StartSQLServerMonitoring();
                System.Diagnostics.Debug.WriteLine("[MainForm] 自启动监控已自动开始");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainForm] 自启动监控启动失败: {ex.Message}");
            }
        }));
    }
}
```

**Step 2: 编译验证**

Run: `dotnet build`
Expected: PASS

**Step 3: Commit**

```bash
git commit -am "feat: add StartAutoMonitoring method for auto-start behavior"
```

---

## Task 9: 修复潜在的 using 引用问题

**Files:**
- Modify: `WindowsEventLogMonitor/Program.cs`
- Modify: `WindowsEventLogMonitor/MainForm.cs`

**Step 1: 确认 using 语句**

确保 Program.cs 顶部有以下 using：

```csharp
using System;
using System.Linq;
using System.ServiceProcess;
using System.Windows.Forms;
```

确保 MainForm.cs 顶部有（如果没有则添加）：

```csharp
using System.Drawing;
```

**Step 2: 编译验证**

Run: `dotnet build`
Expected: PASS (0 errors, minimal warnings)

**Step 3: Commit**

```bash
git commit -am "chore: fix using statements for auto-start feature"
```

---

## Task 10: 最终验证和测试

**Step 1: 完整构建**

Run: `dotnet build --configuration Release`
Expected: SUCCESS

**Step 2: 运行帮助命令验证**

Run: `dotnet run -- --help`
Expected: 显示帮助信息，包含新的自启动命令

**Step 3: 测试查询命令**

Run: `dotnet run -- query-autostart`
Expected: 显示当前自启动状态

**Step 4: 手动 UI 测试清单**

- [ ] 打开配置页面，看到"自启动设置"分组框
- [ ] 勾选"开机自动启动"，GUI 模式和服务模式单选按钮启用
- [ ] 选择 GUI 模式，"最小化到托盘"复选框启用
- [ ] 取消勾选，所有子控件禁用
- [ ] 保存配置后重启应用，配置正确加载

**Step 5: Commit 最终版本**

```bash
git add -A
git commit -m "feat: complete auto-start management implementation

- Add AutoStartService for registry and service management
- Add AutoStartConfig to configuration system
- Add UI controls for auto-start settings in configuration page
- Implement command-line arguments for auto-start control
- Support both GUI and Service mode auto-start
- Support minimize-to-tray on auto-start

Closes #autostart-feature"
```

---

## 附录：代码清单

### 修改/新增文件汇总

| 文件 | 操作 | 说明 |
|------|------|------|
| `Services/AutoStartService.cs` | 创建 | 自启动管理核心服务 |
| `Config.cs` | 修改 | 添加 AutoStartConfig 和 AutoStartMode |
| `MainForm.Designer.cs` | 修改 | 添加 UI 控件初始化 |
| `MainForm.cs` | 修改 | 添加事件处理和配置绑定 |
| `Program.cs` | 修改 | 添加命令行参数处理 |

### 依赖检查

确保项目引用包含：
- `Microsoft.Win32.Registry` (通常已包含在 Windows 目标框架中)
- `System.ServiceProcess.ServiceController` (已存在)

---

**计划完成日期**: 2026-03-09
**预计实现时间**: 2-3 小时
**测试环境**: Windows 10/11, .NET 8.0
