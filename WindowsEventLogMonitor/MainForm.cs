using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic.Logging;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Threading;
using WindowsEventLogMonitor.Services;

namespace WindowsEventLogMonitor
{
    public partial class MainForm : Form
    {
        private EventLogReader eventLogReader;
        private SqlServerLogMonitor sqlServerLogMonitor;
        private JsonService jsonService;
        private HttpService httpService;
        private Config config;
        private const string LogType = "push_log";
        private bool isMonitoring = false;
        private bool isSQLServerMonitoring = false;
        private System.Threading.Timer logCleanTimer;
        private System.Threading.Timer statusUpdateTimer;
        private System.Threading.Timer sqlServerLogRefreshTimer;

        // 添加SQL Server监控的取消令牌和任务
        private CancellationTokenSource sqlServerMonitoringCancellationTokenSource;
        private Task sqlServerMonitoringTask;

        // 统计信息
        private int totalLogsProcessed = 0;
        private int logsUploaded = 0;
        private int uploadErrors = 0;
        private DateTime lastUploadTime = DateTime.MinValue;
        private List<ErrorInfo> recentErrors = new List<ErrorInfo>();

        public MainForm()
        {
            InitializeComponent();
            InitializeServices();
            LoadConfiguration();
            InitializeTimers();

            // 初始化日志文件管理器
            LogFileManager.Initialize();

            // 初始化自动刷新状态显示
            UpdateAutoRefreshStatusDisplay();

            // 订阅 Shown 事件以在窗体显示后自动启动监控
            this.Shown += MainForm_Shown;
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            // 窗体显示后自动启动SQL Server监控
            if (config.SqlServerMonitoring.Enabled && !isSQLServerMonitoring)
            {
                LogMessage("应用启动，自动开始SQL Server监控...");
                BtnStartSQLServerMonitoring_Click(null, null);
            }
        }

        private void InitializeServices()
        {
            eventLogReader = new EventLogReader("Application");
            sqlServerLogMonitor = new SqlServerLogMonitor();
            jsonService = new JsonService();
            httpService = new HttpService();
        }

        private void LoadConfiguration()
        {
            config = Config.GetCachedConfig() ?? new Config();

            // 加载配置到UI控件
            textBoxApiUrl.Text = config.ApiUrl;
            textBoxApiKey.Text = config.Security.ApiKey;
            checkBoxUseHttps.Checked = config.Security.UseHttps;
            numericUpDownTimeout.Value = config.Security.TimeoutSeconds;

            checkBoxEnableRetry.Checked = config.RetryPolicy.EnableRetry;
            numericUpDownMaxRetries.Value = config.RetryPolicy.MaxRetries;
            numericUpDownRetryDelay.Value = config.RetryPolicy.RetryDelaySeconds;

            numericUpDownRetentionDays.Value = config.LogRetention.RetentionDays;
            numericUpDownMaxLogFileSize.Value = config.LogRetention.MaxLogFileSizeKB;

            checkBoxEnableSQLServerMonitoring.Checked = config.SqlServerMonitoring.Enabled;
            numericUpDownMonitorInterval.Value = config.SqlServerMonitoring.MonitorIntervalSeconds;
            numericUpDownBatchSize.Value = config.SqlServerMonitoring.BatchSize;
            checkBoxIncludeMSSQLSERVER.Checked = config.SqlServerMonitoring.IncludeMSSQLSERVER;
            checkBoxIncludeWindowsAuth.Checked = config.SqlServerMonitoring.IncludeWindowsAuth;
            
            // 应用缓存限制配置到日志监控器
            sqlServerLogMonitor.SetMaxCacheLogs(config.SqlServerMonitoring.MaxCacheLogs);
        }

        private void InitializeTimers()
        {
            // 日志清理定时器 - 每天执行一次
            logCleanTimer = new System.Threading.Timer(_ => CleanLogFile(), null, TimeSpan.Zero, TimeSpan.FromDays(1));

            // 状态更新定时器
            statusUpdateTimer = new System.Threading.Timer(_ => UpdateStatusDisplay(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

            // SQL Server日志自动刷新定时器 - 使用配置文件中的间隔
            InitializeSQLServerRefreshTimer();
        }

        private void InitializeSQLServerRefreshTimer()
        {
            // 先清理现有的定时器
            sqlServerLogRefreshTimer?.Dispose();

            // 创建新的定时器
            var refreshInterval = config?.SqlServerMonitoring?.UIRefreshIntervalSeconds ?? 10;
            sqlServerLogRefreshTimer = new System.Threading.Timer(_ => AutoRefreshSQLServerLogs(), null, TimeSpan.FromSeconds(refreshInterval), TimeSpan.FromSeconds(refreshInterval));

            // 更新状态显示
            UpdateAutoRefreshStatusDisplay();

            // 加载自启动配置
            if (config.AutoStart != null)
            {
                // 先设置下级控件状态，再设置复选框状态（避免事件触发时的冲突）
                if (config.AutoStart.Enabled)
                {
                    radioButtonGuiMode.Enabled = true;
                    radioButtonGuiMode.Checked = true;
                    checkBoxMinimizeToTray.Enabled = true;
                    checkBoxMinimizeToTray.Checked = config.AutoStart.MinimizeToTray;
                    comboBoxAutoStartMethod.Enabled = true;
                    comboBoxAutoStartMethod.SelectedIndex = (int)config.AutoStart.Method;
                }
                else
                {
                    radioButtonGuiMode.Enabled = false;
                    radioButtonGuiMode.Checked = false;
                    checkBoxMinimizeToTray.Enabled = false;
                    checkBoxMinimizeToTray.Checked = config.AutoStart.MinimizeToTray;
                    comboBoxAutoStartMethod.Enabled = false;
                    comboBoxAutoStartMethod.SelectedIndex = (int)config.AutoStart.Method;
                }

                checkBoxEnableAutoStart.Checked = config.AutoStart.Enabled;
            }

            // 检查实际注册表/服务状态是否与配置一致
            var autoStartService = new Services.AutoStartService();
            var configMethod = config.AutoStart?.Method ?? Services.AutoStartMethod.Registry;
            var actualStatus = autoStartService.GetStatus(configMethod);
            
            if (actualStatus == Services.AutoStartStatus.Disabled && config.AutoStart.Enabled)
            {
                // 配置启用但实际未启用，提示用户
                MessageBox.Show("自启动配置与实际状态不一致，请重新保存配置。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (actualStatus == Services.AutoStartStatus.GuiEnabled && !config.AutoStart.Enabled)
            {
                // 配置禁用但还有条目，自动清理
                autoStartService.DisableAllAutoStart();
                System.Diagnostics.Debug.WriteLine("[MainForm] 自动清理了遗留的自启动条目");
            }
        }

        /// <summary>
        /// 更新自动刷新状态显示
        /// </summary>
        private void UpdateAutoRefreshStatusDisplay()
        {
            if (lblAutoRefreshStatus.InvokeRequired)
            {
                lblAutoRefreshStatus.Invoke(new Action(UpdateAutoRefreshStatusDisplay));
                return;
            }

            var refreshInterval = config?.SqlServerMonitoring?.UIRefreshIntervalSeconds ?? 10;
            lblAutoRefreshStatus.Text = $"自动刷新: 每{refreshInterval}秒";
            lblAutoRefreshStatus.ForeColor = isSQLServerMonitoring ? Color.Green : Color.Gray;
        }

        #region SQL Server 监控事件处理

        private void CheckBoxEnableSQLServerMonitoring_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = checkBoxEnableSQLServerMonitoring.Checked;

            checkBoxIncludeMSSQLSERVER.Enabled = enabled;
            checkBoxIncludeWindowsAuth.Enabled = enabled;
            numericUpDownMonitorInterval.Enabled = enabled;
            numericUpDownBatchSize.Enabled = enabled;
            btnStartSQLServerMonitoring.Enabled = enabled && !isSQLServerMonitoring;
        }

        private void BtnStartSQLServerMonitoring_Click(object sender, EventArgs e)
        {
            if (!checkBoxEnableSQLServerMonitoring.Checked)
            {
                MessageBox.Show("请先启用SQL Server监控", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // 立即更新UI状态，不等待监控启动完成
                isSQLServerMonitoring = true;
                btnStartSQLServerMonitoring.Enabled = false;
                btnStopSQLServerMonitoring.Enabled = true;
                lblMonitorStatus.Text = "状态: 正在启动...";
                lblMonitorStatus.ForeColor = Color.Orange;

                // 更新自动刷新状态显示
                UpdateAutoRefreshStatusDisplay();

                // 异步启动监控，不阻塞UI线程
                StartSQLServerMonitoringAsync();

                LogMessage("SQL Server监控启动中...");
            }
            catch (Exception ex)
            {
                isSQLServerMonitoring = false;
                btnStartSQLServerMonitoring.Enabled = true;
                btnStopSQLServerMonitoring.Enabled = false;
                lblMonitorStatus.Text = "状态: 启动失败";
                lblMonitorStatus.ForeColor = Color.Red;

                // 更新自动刷新状态显示
                UpdateAutoRefreshStatusDisplay();

                MessageBox.Show($"启动SQL Server监控失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AddError("SQL Server监控启动失败", ex.Message);
            }
        }

        private void BtnStopSQLServerMonitoring_Click(object sender, EventArgs e)
        {
            StopSQLServerMonitoring();
        }

        private async void BtnRefreshSQLServerLogs_Click(object sender, EventArgs e)
        {
            try
            {
                btnRefreshSQLServerLogs.Enabled = false;
                btnRefreshSQLServerLogs.Text = "刷新中...";

                LogMessage("开始刷新SQL Server日志...");

                // 立即收集可用的日志数据
                var collectedCount = await sqlServerLogMonitor.CollectAvailableLogsAsync(1); // 收集最近1分钟的日志

                // 更新显示
                await UpdateSQLServerLogDisplay();

                if (collectedCount > 0)
                {
                    LogMessage($"刷新完成，收集到 {collectedCount} 条SQL Server日志");
                }
                else
                {
                    LogMessage("刷新完成，没有找到新的SQL Server日志");

                    // 如果没有找到日志，提示用户可能的原因
                    if (dataGridViewSQLServerLogs.Rows.Count == 0)
                    {
                        MessageBox.Show(
                            "没有找到SQL Server日志，可能的原因：\n\n" +
                            "1. SQL Server登录审计未启用\n" +
                            "2. 最近24小时内没有登录活动\n" +
                            "3. 需要管理员权限才能读取日志\n\n" +
                            "请确认SQL Server审计已启用（运行Enable-SQLServerAudit.ps1脚本）",
                            "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刷新日志失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AddError("刷新SQL Server日志失败", ex.Message);
                LogMessage($"刷新失败: {ex.Message}");
            }
            finally
            {
                btnRefreshSQLServerLogs.Enabled = true;
                btnRefreshSQLServerLogs.Text = "刷新日志";
            }
        }

        private void StartSQLServerMonitoringAsync()
        {
            // 清空之前的日志缓存，确保显示最新数据
            sqlServerLogMonitor.ClearRecentLogs();

            // 清空UI中的日志显示
            dataGridViewSQLServerLogs.Rows.Clear();
            lblLogCount.Text = "日志数量: 0";

            // 创建取消令牌
            sqlServerMonitoringCancellationTokenSource = new CancellationTokenSource();

            // 启动监控任务（不等待）
            sqlServerMonitoringTask = Task.Run(async () => await StartSQLServerMonitoringLoop(), sqlServerMonitoringCancellationTokenSource.Token);

            // 监听任务完成状态
            _ = Task.Run(async () =>
            {
                try
                {
                    await sqlServerMonitoringTask;

                    // 正常完成时更新UI
                    this.Invoke(new Action(() =>
                    {
                        if (!sqlServerMonitoringCancellationTokenSource.Token.IsCancellationRequested)
                        {
                            lblMonitorStatus.Text = "状态: 已停止";
                            lblMonitorStatus.ForeColor = Color.Red;
                            LogMessage("SQL Server监控已停止");
                        }
                    }));
                }
                catch (Exception ex)
                {
                    // 异常时更新UI
                    this.Invoke(new Action(() =>
                    {
                        isSQLServerMonitoring = false;
                        btnStartSQLServerMonitoring.Enabled = true;
                        btnStopSQLServerMonitoring.Enabled = false;
                        lblMonitorStatus.Text = "状态: 运行异常";
                        lblMonitorStatus.ForeColor = Color.Red;
                        AddError("SQL Server监控运行异常", ex.Message);
                    }));
                }
            });

            // 短暂延迟后更新状态为运行中，并立即收集一次历史日志
            _ = Task.Delay(1000).ContinueWith(async _ =>
            {
                if (!sqlServerMonitoringCancellationTokenSource.Token.IsCancellationRequested)
                {
                    this.Invoke(new Action(() =>
                    {
                        lblMonitorStatus.Text = "状态: 运行中";
                        lblMonitorStatus.ForeColor = Color.Green;
                        LogMessage("SQL Server监控已启动");
                    }));

                    // 启动后立即收集一次历史日志
                    try
                    {
                        LogMessage("正在收集历史SQL Server日志...");
                        var count = await sqlServerLogMonitor.CollectAvailableLogsAsync(1); // 收集最近1分钟的日志

                        this.Invoke(new Action(async () =>
                        {
                            await UpdateSQLServerLogDisplay();
                            if (count > 0)
                            {
                                LogMessage($"启动时收集到 {count} 条历史日志");
                            }
                            else
                            {
                                LogMessage("启动时没有找到历史日志");
                            }
                        }));
                    }
                    catch (Exception ex)
                    {
                        this.Invoke(new Action(() =>
                        {
                            LogMessage($"收集历史日志失败: {ex.Message}");
                        }));
                    }
                }
            });
        }

        private async Task StartSQLServerMonitoringLoop()
        {
            var intervalSeconds = (int)numericUpDownMonitorInterval.Value;
            var cancellationToken = sqlServerMonitoringCancellationTokenSource.Token;

            while (!cancellationToken.IsCancellationRequested && isSQLServerMonitoring)
            {
                try
                {
                    await sqlServerLogMonitor.CollectAndPushSQLServerLogsAsync(config.ApiUrl);

                    // 更新日志显示
                    await UpdateSQLServerLogDisplay();

                    totalLogsProcessed++;
                    logsUploaded++;
                    lastUploadTime = DateTime.Now;

                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // 监控被取消，正常退出
                    break;
                }
                catch (Exception ex)
                {
                    uploadErrors++;
                    AddError("SQL Server日志处理错误", ex.Message);

                    // 发生错误时等待较短时间再重试
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task UpdateSQLServerLogDisplay()
        {
            if (dataGridViewSQLServerLogs.InvokeRequired)
            {
                dataGridViewSQLServerLogs.Invoke(new Action(async () => await UpdateSQLServerLogDisplay()));
                return;
            }

            try
            {
                // 使用配置的最大显示数量
                var maxDisplayLogs = config?.SqlServerMonitoring?.MaxDisplayLogs ?? 500;
                
                // 异步获取最新日志数据
                var recentLogs = await Task.Run(() => sqlServerLogMonitor.GetRecentLogs(maxDisplayLogs));

                LogMessage($"从缓存中获取到 {recentLogs.Count} 条日志");

                // 更新DataGridView
                dataGridViewSQLServerLogs.Rows.Clear();

                foreach (var log in recentLogs)
                {
                    var row = dataGridViewSQLServerLogs.Rows.Add();
                    dataGridViewSQLServerLogs.Rows[row].Cells["TimeGenerated"].Value = log.TimeGenerated.ToString("yyyy-MM-dd HH:mm:ss");
                    dataGridViewSQLServerLogs.Rows[row].Cells["LogType"].Value = log.LogType;
                    dataGridViewSQLServerLogs.Rows[row].Cells["UserName"].Value = log.UserName;
                    dataGridViewSQLServerLogs.Rows[row].Cells["ClientIP"].Value = log.ClientIP;
                    dataGridViewSQLServerLogs.Rows[row].Cells["DatabaseName"].Value = log.DatabaseName;
                    dataGridViewSQLServerLogs.Rows[row].Cells["EventId"].Value = log.EventId.ToString();
                    dataGridViewSQLServerLogs.Rows[row].Cells["Message"].Value = log.Message;

                    // 根据日志类型设置行颜色
                    if (log.LogType.Contains("失败"))
                    {
                        dataGridViewSQLServerLogs.Rows[row].DefaultCellStyle.BackColor = Color.FromArgb(255, 240, 240); // 淡红色
                    }
                    else if (log.LogType.Contains("成功"))
                    {
                        dataGridViewSQLServerLogs.Rows[row].DefaultCellStyle.BackColor = Color.FromArgb(240, 255, 240); // 淡绿色
                    }
                }

                // 更新日志数量显示（显示当前数量/最大限制）
                lblLogCount.Text = $"日志数量: {recentLogs.Count}/{maxDisplayLogs}";

                // 自动滚动到最新记录
                if (dataGridViewSQLServerLogs.Rows.Count > 0)
                {
                    dataGridViewSQLServerLogs.FirstDisplayedScrollingRowIndex = 0;
                }

                LogMessage($"已更新SQL Server日志显示，共 {recentLogs.Count} 条记录");
            }
            catch (Exception ex)
            {
                AddError("更新SQL Server日志显示失败", ex.Message);
                LogMessage($"更新日志显示失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 自动刷新SQL Server日志显示
        /// </summary>
        private async void AutoRefreshSQLServerLogs()
        {
            // 只有在SQL Server监控启动时才自动刷新
            if (!isSQLServerMonitoring)
                return;

            try
            {
                // 在UI线程上更新显示
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        Task.Run(async () => await UpdateSQLServerLogDisplay());
                    }));
                    return;
                }

                // 更新显示（不收集新日志，只更新UI显示）
                await UpdateSQLServerLogDisplay();
            }
            catch (Exception ex)
            {
                // 静默处理错误，避免影响用户体验
                LogMessage($"自动刷新日志显示时发生错误: {ex.Message}");
            }
        }

        #endregion

        #region 通用日志监控事件处理

        private async void BtnStartLogs_Click(object sender, EventArgs e)
        {
            if (btnStartLogs.Text == "开始监控")
            {
                await Task.Run(() => StartLogOutput());
            }
            else
            {
                StopLogOutput();
            }
        }

        private void BtnLoadLogs_Click(object sender, EventArgs e)
        {
            var selectedSource = comboBoxEventSource.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedSource))
            {
                MessageBox.Show("请先选择事件源", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                eventLogReader = new EventLogReader("Application");
                var logs = eventLogReader.FilterEventLogEntries(selectedSource, string.Empty);

                dataGridViewLogs.DataSource = logs.Select(log => new
                {
                    log.InstanceId,
                    log.EntryType,
                    log.Site,
                    log.TimeGenerated,
                    log.TimeWritten,
                    log.Source,
                    log.Message
                }).ToList();

                LogMessage($"已加载 {logs.Count} 条日志记录");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载日志失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AddError("加载日志失败", ex.Message);
            }
        }

        private async void BtnPushLogs_Click(object sender, EventArgs e)
        {
            var selectedSource = comboBoxEventSource.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedSource))
            {
                MessageBox.Show("请先选择事件源", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var logs = eventLogReader.FilterEventLogEntries(selectedSource, string.Empty);
                var pushedLogIds = LoadPushedLogIds();
                var newLogs = logs.Where(log => !pushedLogIds.Contains(GenerateUniqueKey(log))).ToList();

                LogMessage($"从 {selectedSource} 加载了 {logs.Count} 条日志，其中 {pushedLogIds.Count} 条已推送，{newLogs.Count} 条新日志");

                if (newLogs.Count == 0)
                {
                    MessageBox.Show("没有新的日志需要推送", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var jsonData = jsonService.ConvertToJSON(newLogs);
                await httpService.PushLogsToAPIAsync(jsonData, config.ApiUrl);

                // 记录推送信息
                var logTime = DateTime.Now;
                foreach (var log in newLogs)
                {
                    var logEntry = $"Log ID: {GenerateUniqueKey(log)}, Generated at: {log.TimeGenerated}, Pushed at: {logTime}";
                    LogFileManager.WriteLogEntry(LogType, logEntry);
                }

                logsUploaded += newLogs.Count;
                lastUploadTime = DateTime.Now;

                MessageBox.Show($"成功推送 {newLogs.Count} 条日志", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LogMessage($"成功推送 {newLogs.Count} 条日志");
            }
            catch (Exception ex)
            {
                uploadErrors++;
                MessageBox.Show($"推送日志失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AddError("推送日志失败", ex.Message);
            }
        }

        #endregion

        #region 配置管理事件处理

        private void BtnSaveConfig_Click(object sender, EventArgs e)
        {
            try
            {
                // 从UI控件更新配置
                config.ApiUrl = textBoxApiUrl.Text;
                config.Security.ApiKey = textBoxApiKey.Text;
                config.Security.UseHttps = checkBoxUseHttps.Checked;
                config.Security.TimeoutSeconds = (int)numericUpDownTimeout.Value;

                config.RetryPolicy.EnableRetry = checkBoxEnableRetry.Checked;
                config.RetryPolicy.MaxRetries = (int)numericUpDownMaxRetries.Value;
                config.RetryPolicy.RetryDelaySeconds = (int)numericUpDownRetryDelay.Value;

                config.LogRetention.RetentionDays = (int)numericUpDownRetentionDays.Value;
                config.LogRetention.MaxLogFileSizeKB = (int)numericUpDownMaxLogFileSize.Value;

                config.SqlServerMonitoring.Enabled = checkBoxEnableSQLServerMonitoring.Checked;
                config.SqlServerMonitoring.MonitorIntervalSeconds = (int)numericUpDownMonitorInterval.Value;
                config.SqlServerMonitoring.BatchSize = (int)numericUpDownBatchSize.Value;
                config.SqlServerMonitoring.IncludeMSSQLSERVER = checkBoxIncludeMSSQLSERVER.Checked;
                config.SqlServerMonitoring.IncludeWindowsAuth = checkBoxIncludeWindowsAuth.Checked;

                // 保存自启动配置（必须在 SaveConfig 之前设置）
                config.AutoStart.Enabled = checkBoxEnableAutoStart.Checked;
                config.AutoStart.Mode = AutoStartMode.Gui;
                config.AutoStart.Method = (Services.AutoStartMethod)comboBoxAutoStartMethod.SelectedIndex;
                config.AutoStart.MinimizeToTray = checkBoxMinimizeToTray.Checked;

                Config.SaveConfig(config);

                // 应用自启动设置
                var autoStartService = new Services.AutoStartService();
                bool success;
                var selectedMethod = (Services.AutoStartMethod)comboBoxAutoStartMethod.SelectedIndex;

                if (config.AutoStart.Enabled)
                {
                    // 先禁用所有方式，然后启用选中的方式
                    autoStartService.DisableAllAutoStart();
                    success = autoStartService.EnableAutoStart(selectedMethod, config.AutoStart.MinimizeToTray);
                }
                else
                {
                    // 禁用所有方式
                    success = autoStartService.DisableAllAutoStart();
                }

                if (!success)
                {
                    var methodName = selectedMethod switch
                    {
                        Services.AutoStartMethod.Registry => "注册表",
                        Services.AutoStartMethod.TaskScheduler => "任务计划",
                        Services.AutoStartMethod.StartupFolder => "启动文件夹",
                        _ => "未知"
                    };
                    MessageBox.Show($"自启动设置保存失败（{methodName}），请检查权限或以管理员身份运行。\n提示: 任务计划方式可能需要管理员权限。",
                        "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                MessageBox.Show("配置已保存", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LogMessage("配置已保存");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AddError("保存配置失败", ex.Message);
            }
        }

        private async void BtnTestConnection_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxApiUrl.Text))
            {
                MessageBox.Show("请输入API地址", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnTestConnection.Enabled = false;
            btnTestConnection.Text = "测试中...";

            try
            {
                var result = await httpService.TestConnectionAsync(textBoxApiUrl.Text);

                if (result)
                {
                    MessageBox.Show("连接测试成功", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LogMessage("API连接测试成功");
                }
                else
                {
                    MessageBox.Show("连接测试失败", "失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    AddError("API连接测试失败", "无法连接到指定的API地址");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"连接测试失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AddError("API连接测试失败", ex.Message);
            }
            finally
            {
                btnTestConnection.Enabled = true;
                btnTestConnection.Text = "测试连接";
            }
        }

        #endregion

        #region 状态统计事件处理

        private void BtnClearErrors_Click(object sender, EventArgs e)
        {
            recentErrors.Clear();
            dataGridViewRecentErrors.Rows.Clear();
            uploadErrors = 0;
            UpdateStatusDisplay();
            LogMessage("错误记录已清除");
        }

        private void UpdateStatusDisplay()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(UpdateStatusDisplay));
                return;
            }

            try
            {
                lblTotalLogsProcessed.Text = $"已处理日志: {totalLogsProcessed}";
                lblLogsUploaded.Text = $"已上传日志: {logsUploaded}";
                lblUploadErrors.Text = $"上传错误: {uploadErrors}";
                lblLastUploadTime.Text = lastUploadTime == DateTime.MinValue ? "最后上传: 从未" : $"最后上传: {lastUploadTime:yyyy-MM-dd HH:mm:ss}";

                // 更新进度条
                if (totalLogsProcessed > 0)
                {
                    var successRate = (logsUploaded * 100) / totalLogsProcessed;
                    progressBarUpload.Value = Math.Min(successRate, 100);
                }
            }
            catch (Exception ex)
            {
                AddError("更新状态显示失败", ex.Message);
            }
        }

        #endregion

        #region 辅助方法

        private void AddError(string error, string details)
        {
            var errorInfo = new ErrorInfo
            {
                Time = DateTime.Now,
                Error = error,
                Details = details
            };

            recentErrors.Insert(0, errorInfo);

            // 只保留最近50个错误
            if (recentErrors.Count > 50)
            {
                recentErrors.RemoveRange(50, recentErrors.Count - 50);
            }

            // 更新错误显示
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateErrorDisplay()));
            }
            else
            {
                UpdateErrorDisplay();
            }
        }

        private void UpdateErrorDisplay()
        {
            dataGridViewRecentErrors.Rows.Clear();
            foreach (var error in recentErrors.Take(20)) // 只显示最近20个错误
            {
                dataGridViewRecentErrors.Rows.Add(
                    error.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                    error.Error,
                    error.Details
                );
            }
        }

        private void LogMessage(string message)
        {
            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            Debug.WriteLine(logMessage);
        }

        private void StartSQLServerMonitoringFromTray()
        {
            // 从托盘菜单调用的方法
            if (!isSQLServerMonitoring && checkBoxEnableSQLServerMonitoring.Checked)
            {
                BtnStartSQLServerMonitoring_Click(null, null);
            }
        }

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
                        StartSQLServerMonitoringAsync();
                        System.Diagnostics.Debug.WriteLine("[MainForm] 自启动监控已自动开始");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MainForm] 自启动监控启动失败: {ex.Message}");
                    }
                }));
            }
        }

        private void StopSQLServerMonitoring()
        {
            try
            {
                isSQLServerMonitoring = false;
                sqlServerLogMonitor.StopMonitoring();

                // 取消监控任务
                sqlServerMonitoringCancellationTokenSource?.Cancel();

                btnStartSQLServerMonitoring.Enabled = true;
                btnStopSQLServerMonitoring.Enabled = false;
                lblMonitorStatus.Text = "状态: 正在停止...";
                lblMonitorStatus.ForeColor = Color.Orange;

                LogMessage("正在停止SQL Server监控...");

                // 异步等待任务完成并更新UI
                Task.Run(async () =>
                {
                    try
                    {
                        if (sqlServerMonitoringTask != null)
                        {
                            await sqlServerMonitoringTask;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常取消
                    }
                    catch (Exception ex)
                    {
                        AddError("停止监控时出现错误", ex.Message);
                    }
                    finally
                    {
                        this.Invoke(new Action(() =>
                        {
                            lblMonitorStatus.Text = "状态: 已停止";
                            lblMonitorStatus.ForeColor = Color.Red;
                            LogMessage("SQL Server监控已停止");

                            // 更新自动刷新状态显示
                            UpdateAutoRefreshStatusDisplay();
                        }));

                        // 清理资源
                        sqlServerMonitoringCancellationTokenSource?.Dispose();
                        sqlServerMonitoringCancellationTokenSource = null;
                        sqlServerMonitoringTask = null;
                    }
                });
            }
            catch (Exception ex)
            {
                AddError("停止SQL Server监控失败", ex.Message);
                LogMessage($"停止SQL Server监控失败: {ex.Message}");
            }
        }

        #endregion

        #region 原有方法保持兼容

        private async Task StartLogOutput()
        {
            CleanLogFile();

            var selectedSource = "";
            if (comboBoxEventSource.InvokeRequired)
            {
                comboBoxEventSource.Invoke(new Action(() =>
                {
                    selectedSource = comboBoxEventSource.SelectedItem?.ToString();
                }));
            }
            else
            {
                selectedSource = comboBoxEventSource.SelectedItem?.ToString();
            }

            if (string.IsNullOrEmpty(selectedSource))
            {
                MessageBox.Show("请先选择事件源", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            isMonitoring = true;
            btnStartLogs.Invoke(new MethodInvoker(() =>
            {
                btnStartLogs.Text = "停止监控";
                btnStartLogs.BackColor = Color.Red;
            }));

            while (isMonitoring)
            {
                try
                {
                    var logEntries = eventLogReader.FilterEventLogEntries(selectedSource, string.Empty);
                    var pushedLogIds = LoadPushedLogIds();

                    var generatedTime = GetMaxGeneratedTime();
                    var newLogs = logEntries
                        .Where(log => !pushedLogIds.Contains(GenerateUniqueKey(log)) && log.TimeGenerated > generatedTime)
                        .ToList();

                    if (newLogs.Count > 0)
                    {
                        LogMessage($"发现 {newLogs.Count} 条新日志，准备处理");
                        await HandleNewLogs(newLogs);
                    }
                    else
                    {
                        await Task.Delay(1000);
                    }
                }
                catch (Exception ex)
                {
                    AddError("日志监控错误", ex.Message);
                    await Task.Delay(5000);
                }
            }
        }

        private async Task HandleNewLogs(List<EventLogEntry> newLogs)
        {
            foreach (var logEntry in newLogs)
            {
                if (!isMonitoring) break;

                AddLogToDataGridView(logEntry);
                await PushSingleLogAsync(logEntry);

                await Task.Delay(1000);
            }
        }

        private void AddLogToDataGridView(EventLogEntry logEntry)
        {
            dataGridViewLogs.Invoke(new MethodInvoker(() =>
            {
                if (dataGridViewLogs.Rows.Count >= 100)
                {
                    dataGridViewLogs.Rows.RemoveAt(0);
                }

                dataGridViewLogs.Rows.Add(logEntry.TimeGenerated, logEntry.Message, logEntry.InstanceId, logEntry.EntryType, logEntry.Site, logEntry.Source);
                if (dataGridViewLogs.RowCount > 0)
                {
                    dataGridViewLogs.FirstDisplayedScrollingRowIndex = dataGridViewLogs.RowCount - 1;
                }
            }));
        }

        private void StopLogOutput()
        {
            isMonitoring = false;
            btnStartLogs.Invoke(new MethodInvoker(() =>
            {
                btnStartLogs.Text = "开始监控";
                btnStartLogs.BackColor = SystemColors.Control;
            }));
        }

        private async Task PushSingleLogAsync(EventLogEntry logEntry)
        {
            var log = new
            {
                TimeGenerated = logEntry.TimeGenerated,
                Message = logEntry.Message,
                InstanceId = logEntry.InstanceId,
                EntryType = logEntry.EntryType,
                Site = logEntry.Site,
                Source = logEntry.Source
            };

            var logs = new List<object> { log };
            var json = JsonConvert.SerializeObject(logs);
            await httpService.PushLogsToAPIAsync(json, config.ApiUrl);

            var logTime = DateTime.Now;
            var logEntryStrings = $"Log ID: {GenerateUniqueKey(logEntry)},Generated at: {logEntry.TimeGenerated},Pushed at: {logTime}";
            await LogFileManager.WriteLogEntryAsync(LogType, logEntryStrings);

            totalLogsProcessed++;
            logsUploaded++;
            lastUploadTime = DateTime.Now;
        }

        private DateTime GetMaxGeneratedTime()
        {
            return LogFileManager.GetLastProcessedTime(LogType);
        }

        private HashSet<string> LoadPushedLogIds()
        {
            return LogFileManager.LoadPushedLogIds(LogType);
        }

        private string GenerateUniqueKey(EventLogEntry log)
        {
            var timeGeneratedTimestamp = new DateTimeOffset(log.TimeGenerated).ToUnixTimeSeconds();
            return $"{log.InstanceId}_{timeGeneratedTimestamp}_{log.TimeGenerated.Ticks}_{log.Source}";
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                notifyIcon.Visible = true;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                notifyIcon.Visible = true;
            }
        }

        private void ExitMenuItem_Click(object sender, EventArgs e)
        {
            isMonitoring = false;
            isSQLServerMonitoring = false;

            // 停止SQL Server监控
            sqlServerMonitoringCancellationTokenSource?.Cancel();

            // 清理定时器
            logCleanTimer?.Dispose();
            statusUpdateTimer?.Dispose();
            sqlServerLogRefreshTimer?.Dispose();

            // 清理监控资源
            sqlServerMonitoringCancellationTokenSource?.Dispose();

            Application.Exit();
        }

        private void CleanLogFile()
        {
            var retentionDays = config?.LogRetention?.RetentionDays ?? 3;
            LogFileManager.CleanupOldLogFiles(retentionDays);
        }

        #endregion

        #region 自启动设置事件处理

        private void CheckBoxEnableAutoStart_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = checkBoxEnableAutoStart.Checked;
            
            // 启用/禁用所有子控件
            comboBoxAutoStartMethod.Enabled = enabled;
            labelAutoStartMethod.Enabled = enabled;
            radioButtonGuiMode.Enabled = enabled;
            checkBoxMinimizeToTray.Enabled = enabled;
            labelAutoStartHint.Enabled = enabled;

            if (enabled)
            {
                // 默认选中 GUI 模式（如果未选中）
                if (!radioButtonGuiMode.Checked)
                {
                    radioButtonGuiMode.Checked = true;
                }
            }
            else
            {
                radioButtonGuiMode.Checked = false;
            }
        }

        private void RadioButtonGuiMode_CheckedChanged(object sender, EventArgs e)
        {
            // 只有当自启动启用时，才根据 GUI 模式选中状态控制最小化复选框
            if (checkBoxEnableAutoStart.Checked)
            {
                checkBoxMinimizeToTray.Enabled = radioButtonGuiMode.Checked;
            }
        }

        #endregion
    }

    public class ErrorInfo
    {
        public DateTime Time { get; set; }
        public string Error { get; set; } = "";
        public string Details { get; set; } = "";
    }
}