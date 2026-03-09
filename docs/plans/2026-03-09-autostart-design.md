# WindowsEventLogMonitor 自启动管理设计文档

**日期**: 2026-03-09
**功能**: 添加开机自启动管理
**状态**: 已批准，待实现

---

## 1. 需求概述

### 1.1 目标
为 WindowsEventLogMonitor 添加开机自启动管理功能，允许用户配置应用程序在 Windows 启动时自动运行。

### 1.2 功能范围
- ✅ 支持 GUI 模式开机自启动
- ✅ 支持 Windows 服务模式开机自启动
- ✅ 提供 UI 界面（配置页面）控制自启动设置
- ✅ 提供命令行参数控制自启动
- ✅ GUI 模式可配置启动时是否最小化到系统托盘

### 1.3 不在本次范围内
- ❌ 任务计划程序集成（过于复杂）
- ❌ 延迟启动功能
- ❌ 特定用户/所有用户区分（默认当前用户）

---

## 2. 架构设计

### 2.1 新增组件

```
WindowsEventLogMonitor/
├── Services/
│   └── AutoStartService.cs      # 自启动管理核心服务
├── Config.cs                     # 扩展：添加 AutoStartConfig
├── MainForm.cs                   # 扩展：配置页面添加自启动选项
└── Program.cs                    # 扩展：添加 autostart 命令行参数
```

### 2.2 组件职责

| 组件 | 职责 |
|------|------|
| `AutoStartService` | 管理注册表项的增删查，支持 GUI/Service 两种模式 |
| `Config.AutoStartConfig` | 存储用户自启动配置（是否启用、启动模式、是否最小化） |
| `MainForm` | 配置页面添加 UI 控件，处理用户交互 |
| `Program` | 处理 `autostart` 命令行参数，控制启动行为 |

---

## 3. 组件详细设计

### 3.1 AutoStartService 类

```csharp
namespace WindowsEventLogMonitor.Services
{
    public class AutoStartService
    {
        private const string RegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "WindowsEventLogMonitor";

        /// <summary>
        /// 获取当前自启动状态
        /// </summary>
        public AutoStartStatus GetStatus();

        /// <summary>
        /// 启用 GUI 模式自启动
        /// </summary>
        /// <param name="minimizeToTray">启动时是否最小化到托盘</param>
        public bool EnableGuiAutoStart(bool minimizeToTray = true);

        /// <summary>
        /// 启用服务模式自启动
        /// </summary>
        public bool EnableServiceAutoStart();

        /// <summary>
        /// 禁用自启动
        /// </summary>
        public bool DisableAutoStart();

        /// <summary>
        /// 处理命令行快捷操作
        /// </summary>
        public static void ProcessCommand(string command, string[] args);
    }

    public enum AutoStartMode { None, Gui, Service }
    public enum AutoStartStatus { Disabled, GuiEnabled, ServiceEnabled }
}
```

### 3.2 配置类扩展

```csharp
public class Config
{
    // 现有配置...

    /// <summary>
    /// 自启动配置
    /// </summary>
    public AutoStartConfig AutoStart { get; set; } = new AutoStartConfig();
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
    /// GUI 模式下启动时是否最小化到系统托盘
    /// </summary>
    public bool MinimizeToTray { get; set; } = true;
}
```

### 3.3 UI 界面设计

在"配置"页面（`tabPageConfiguration`）新增"自启动设置"分组框：

```
┌─ 自启动设置 ──────────────────────────┐
│                                        │
│ ☐ 开机自动启动                         │
│                                        │
│    ( ) 以图形界面模式启动              │
│        ☐ 启动时最小化到系统托盘        │
│                                        │
│    ( ) 以服务模式启动                  │
│      （需先安装服务）                  │
│                                        │
└────────────────────────────────────────┘
```

**控件清单：**
- `checkBoxEnableAutoStart` - 启用/禁用自启动
- `radioButtonGuiMode` - GUI 模式单选按钮
- `checkBoxMinimizeToTray` - 最小化到托盘复选框
- `radioButtonServiceMode` - 服务模式单选按钮
- `lblServiceModeHint` - 服务模式提示文本

---

## 4. 数据流与控制流程

### 4.1 启用 GUI 模式自启动流程

```
用户勾选"开机自动启动"并选择"图形界面模式"
    ↓
用户点击"保存配置"
    ↓
MainForm.SaveConfiguration()
    ↓
AutoStartService.EnableGuiAutoStart(minimizeToTray)
    ↓
1. 写入注册表 HKCU\Software\Microsoft\Windows\CurrentVersion\Run
   值名称: WindowsEventLogMonitor
   值数据: "C:\...\WindowsEventLogMonitor.exe" --autostart-gui
    ↓
2. 更新 config.json
   AutoStart.Enabled = true
   AutoStart.Mode = Gui
   AutoStart.MinimizeToTray = true
    ↓
3. 显示成功提示"开机自启动已启用"
```

### 4.2 开机启动执行流程（GUI 模式）

```
Windows 启动 → 执行注册表中的程序
    ↓
WindowsEventLogMonitor.exe --autostart-gui
    ↓
Program.Main 解析到 --autostart-gui 参数
    ↓
读取 config.json 中的 AutoStart 配置
    ↓
if AutoStart.MinimizeToTray == true
    启动时隐藏主窗口，只显示托盘图标
    自动开始 SQL Server 监控（如果配置启用）
else
    正常显示主窗口
    ↓
程序正常运行
```

### 4.3 启用服务模式自启动流程

```
用户选择"以服务模式启动"
    ↓
AutoStartService.EnableServiceAutoStart()
    ↓
1. 检查 Windows 服务是否已安装
   使用 ServiceController 检查 SqlServerLogMonitor 服务
   未安装 → 提示"请先安装 Windows 服务"
    ↓
2. 设置服务启动类型为 Automatic
   使用 sc config SqlServerLogMonitor start= auto
   或使用 ServiceController 修改 StartType
    ↓
3. 更新 config.json
   AutoStart.Enabled = true
   AutoStart.Mode = Service
    ↓
4. 可选：立即启动服务
    ↓
5. 显示成功提示
```

### 4.4 禁用自启动流程

```
用户取消勾选"开机自动启动"
    ↓
AutoStartService.DisableAutoStart()
    ↓
1. 删除注册表项 HKCU\...\Run\WindowsEventLogMonitor
2. 设置服务启动类型为 Manual（如果之前是 Service 模式）
    ↓
3. 更新 config.json
   AutoStart.Enabled = false
    ↓
4. 显示成功提示
```

---

## 5. 命令行参数设计

### 5.1 用户命令

| 命令 | 说明 | 示例 |
|------|------|------|
| `autostart-gui` | 以 GUI 模式自启动 | `WindowsEventLogMonitor.exe autostart-gui --minimize` |
| `set-autostart-mode gui` | 设置自启动为 GUI 模式 | `WindowsEventLogMonitor.exe set-autostart-mode gui --minimize` |
| `set-autostart-mode service` | 设置自启动为服务模式 | `WindowsEventLogMonitor.exe set-autostart-mode service` |
| `disable-autostart` | 禁用自启动 | `WindowsEventLogMonitor.exe disable-autostart` |
| `query-autostart` | 查询当前自启动状态 | `WindowsEventLogMonitor.exe query-autostart` |

### 5.2 参数选项

```
autostart-gui [options]
  --minimize, -m    启动时最小化到系统托盘（默认）
  --show, -s        启动时显示主窗口

set-autostart-mode gui [options]
  --minimize, -m    设置启动时最小化（默认 true）
  --no-minimize     设置启动时显示窗口
```

---

## 6. 错误处理

### 6.1 常见错误场景

| 场景 | 处理方式 | 用户提示 |
|------|----------|----------|
| 注册表写入失败（权限不足） | 提示以管理员身份运行 | "设置开机自启动需要管理员权限" |
| 服务未安装却选择服务模式 | 禁用服务模式选项并提示 | "请先安装 Windows 服务" |
| 服务启动类型修改失败 | 记录日志，提示手动设置 | "请手动设置服务启动类型为自动" |
| 配置文件保存失败 | 回滚注册表更改 | "配置保存失败，自启动设置未生效" |

### 6.2 日志记录

所有自启动相关操作应记录到应用程序日志：
- 启用/禁用自启动的时间、模式、结果
- 命令行参数处理记录
- 错误和异常信息

---

## 7. 测试要点

### 7.1 功能测试

- [ ] 启用 GUI 模式自启动，重启验证
- [ ] 启用服务模式自启动，重启验证
- [ ] 切换自启动模式（GUI ↔ Service）
- [ ] 禁用自启动后重启验证
- [ ] 最小化到托盘选项生效
- [ ] 命令行参数正确解析执行

### 7.2 边界测试

- [ ] 服务未安装时选择服务模式
- [ ] 无管理员权限时修改注册表
- [ ] 配置文件损坏时的容错处理
- [ ] 注册表项被手动删除后的状态检测

---

## 8. 实现计划

1. **Phase 1**: 创建 AutoStartService 类和单元测试
2. **Phase 2**: 扩展 Config 类添加 AutoStartConfig
3. **Phase 3**: 修改 MainForm 添加 UI 控件和事件处理
4. **Phase 4**: 扩展 Program.cs 添加命令行参数支持
5. **Phase 5**: 集成测试和验证

---

## 9. 注意事项

1. **权限要求**: 修改注册表 HKCU 不需要管理员权限，但修改服务启动类型可能需要
2. **路径处理**: 注册表中的程序路径应使用绝对路径，包含引号处理空格
3. **配置同步**: UI 状态应与实际注册表/服务状态保持一致，启动时检测
4. **向后兼容**: 新增配置项应有默认值，不影响现有用户

---

**设计者**: Claude Code
**批准状态**: 已批准
**下一步**: 创建实现计划 (writing-plans)
