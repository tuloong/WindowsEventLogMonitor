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

namespace WindowsEventLogMonitor
{
    public partial class MainForm : Form
    {

        private EventLogReader eventLogReader;
        private JsonService jsonService;
        private HttpService httpService;
        private const string LogFilePath = "push_log.ini";
        private bool isMonitoring = false;
        private System.Threading.Timer logCleanTimer;


        public MainForm()
        {
            InitializeComponent();
            eventLogReader = new EventLogReader("Application");
            jsonService = new JsonService();
            httpService = new HttpService();

            // 添加 DataGridView 列
            dataGridViewLogs.Columns.Add("TimeGenerated", "TimeGenerated");
            dataGridViewLogs.Columns.Add("Message", "Message");
            dataGridViewLogs.Columns.Add("InstanceId", "InstanceId");
            dataGridViewLogs.Columns.Add("EntryType", "EntryType");
            dataGridViewLogs.Columns.Add("Site", "Site");
            dataGridViewLogs.Columns.Add("TimeGenerated", "TimeGenerated");
            dataGridViewLogs.Columns.Add("Source", "Source");

            // 设置定时器，每天清理一次日志文件
            logCleanTimer = new System.Threading.Timer(_ => CleanLogFile(), null, TimeSpan.Zero, TimeSpan.FromDays(1));
        }

        private async Task StartLogOutput()
        {

            // 清理日志文件
            CleanLogFile();

            var selectedSource = "";
            if (comboBoxEventSource.InvokeRequired)
            {
                comboBoxEventSource.Invoke(new Action(() =>
                {
                    selectedSource = comboBoxEventSource.SelectedItem?.ToString();
                    // 在这里处理 selectedSource
                }));
            }
            else
            {
                selectedSource = comboBoxEventSource.SelectedItem?.ToString();
                // 在这里处理 selectedSource
            }
            if (string.IsNullOrEmpty(selectedSource))
            {
                MessageBox.Show("Please select a source first.");
                return;
            }

            isMonitoring = true;
            btnStartLogs.Invoke(new MethodInvoker(() =>
            {
                btnStartLogs.Text = "Cancel Logs";
                btnStartLogs.BackColor = System.Drawing.Color.Red;
            }
            ));

            while (isMonitoring)
            {
                try
                {
                    var logEntries = eventLogReader.FilterEventLogEntries(selectedSource, string.Empty);
                    var pushedLogIds = LoadPushedLogIds();

                    // 获取最新日志，
                    var generatedTime = GetMaxGeneratedTime();
                    var newLogs = logEntries
                        .Where(log => !pushedLogIds.Contains(GenerateUniqueKey(log)) && log.TimeGenerated > generatedTime)
                        .ToList();

                    if (newLogs.Count > 0)
                    {
                        await HandleNewLogs(newLogs);
                    }
                    else
                    {
                        await Task.Delay(1000); // 如果没有新日志，每1秒检查一次
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred: {ex.Message}");
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

                await Task.Delay(1000); // 每1秒输出一行日志
            }
        }

        private void AddLogToDataGridView(EventLogEntry logEntry)
        {
            dataGridViewLogs.Invoke(new MethodInvoker(() =>
            {
                if (dataGridViewLogs.Rows.Count >= 100)
                {
                    dataGridViewLogs.Rows.RemoveAt(0); // 移除最早的一行
                }

                dataGridViewLogs.Rows.Add(logEntry.TimeGenerated, logEntry.Message, logEntry.InstanceId, logEntry.EntryType, logEntry.Site, logEntry.Source);
                if (dataGridViewLogs.RowCount > 0)
                {
                    dataGridViewLogs.FirstDisplayedScrollingRowIndex = dataGridViewLogs.RowCount - 1;
                }
            }
            ));
        }


        private void StopLogOutput()
        {
            isMonitoring = false;
            btnStartLogs.Invoke(new MethodInvoker(() =>
            {
                btnStartLogs.Text = "Start Logs";
                btnStartLogs.BackColor = System.Drawing.Color.Empty;
            }
            ));
        }

        private async Task PushLogsAsync(List<EventLogEntry> newLogs)
        {
            var logs = new List<object>();
            foreach (var logEntry in newLogs)
            {
                logs.Add(new
                {
                    TimeGenerated = logEntry.TimeGenerated,
                    Message = logEntry.Message,
                    InstanceId = logEntry.InstanceId,
                    EntryType = logEntry.EntryType,
                    Site = logEntry.Site,
                    Source = logEntry.Source
                });
            }

            var json = JsonConvert.SerializeObject(logs);
            var apiUrl = textBoxApiUrl.Text;
            await httpService.PushLogsToAPIAsync(json, apiUrl);
            // 记录推送的日志ID
            var logTime = DateTime.Now;
            using (var writer = new StreamWriter(LogFilePath, true))
            {
                foreach (var logEntry in newLogs)
                {
                    var logEntryStrings = $"Log ID: {GenerateUniqueKey(logEntry)}, Pushed at: {logTime}";
                    await writer.WriteLineAsync(logEntryStrings);
                }
            }
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
            var apiUrl = textBoxApiUrl.Text;
            await httpService.PushLogsToAPIAsync(json, apiUrl);

            // 记录推送的日志ID
            var logTime = DateTime.Now;
            var logEntryStrings = $"Log ID: {GenerateUniqueKey(logEntry)},Generated at: {logEntry.TimeGenerated},Pushed at: {logTime}";
            using (var writer = new StreamWriter(LogFilePath, true))
            {
                await writer.WriteLineAsync(logEntryStrings);
            }
        }

        private void BtnSaveConfig_Click(object sender, EventArgs e)
        {
            var config = new Config { ApiUrl = textBoxApiUrl.Text };
            Config.SaveConfig(config);
        }

        private async void BtnStartLogs_Click(object sender, EventArgs e)
        {
            if (btnStartLogs.Text == "Start Logs")
            {
                await Task.Run(() => StartLogOutput());  // 启动 StartLogOutput() 在单独的线程上运行
            }
            else
            {
                isMonitoring = false;
                btnStartLogs.Text = "Start Logs";
                btnStartLogs.BackColor = System.Drawing.Color.Empty;
            }
        }


        private void BtnLoadLogs_Click(object sender, EventArgs e)
        {
            var selectedSource = comboBoxEventSource.SelectedItem?.ToString();
            eventLogReader = new EventLogReader("Application");
            var logs = eventLogReader.FilterEventLogEntries(selectedSource!, string.Empty);
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
        }

        private async void BtnPushLogs_Click(object sender, EventArgs e)
        {
            var selectedSource = comboBoxEventSource.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(selectedSource))
            {
                MessageBox.Show("Please select a source first.");
                return;
            }

            var logs = eventLogReader.FilterEventLogEntries(selectedSource, string.Empty); // get logs，filter by source，no filter by event type
            var pushedLogIds = LoadPushedLogIds();

            var newLogs = logs.Where(log => !pushedLogIds.Contains(GenerateUniqueKey(log))).ToList();
            if (newLogs.Count == 0)
            {
                MessageBox.Show("No new logs to push.");
                return;
            }

            var jsonData = jsonService.ConvertToJSON(newLogs);
            await httpService.PushLogsToAPIAsync(jsonData, Config.GetCachedConfig()?.ApiUrl ?? "");

            // 记录推送时间点和事件主键，并缓存到本地日志
            var logTime = DateTime.Now;
            using (var writer = new StreamWriter(LogFilePath, true))
            {
                foreach (var log in newLogs)
                {
                    var logEntry = $"Log ID: {GenerateUniqueKey(log)}, Pushed at: {logTime}";
                    writer.WriteLine(logEntry);
                }
            }
        }

        private DateTime GetMaxGeneratedTime()
        {
            DateTime maxGeneratedTime = DateTime.MinValue;
            string pattern = @"Generated at: (?<generatedTime>[\d\- :]+)";

            if (File.Exists(LogFilePath))
            {
                var lines = File.ReadAllLines(LogFilePath);
                foreach (var line in lines)
                {
                    var match = Regex.Match(line, pattern);
                    if (match.Success)
                    {
                        if (DateTime.TryParse(match.Groups["generatedTime"].Value, out var generatedDate))
                        {
                            if (generatedDate > maxGeneratedTime)
                            {
                                maxGeneratedTime = generatedDate;
                            }
                        }
                    }
                }
            }

            return maxGeneratedTime;
        }

        private HashSet<string> LoadPushedLogIds()
        {
            var pushedLogIds = new HashSet<string>();
            if (File.Exists(LogFilePath))
            {
                var lines = File.ReadAllLines(LogFilePath);
                foreach (var line in lines)
                {
                    var parts = line.Split(',');
                    if (parts.Length > 0)
                    {
                        var logIdPart = parts[0].Split(':');
                        if (logIdPart.Length > 1)
                        {
                            pushedLogIds.Add(logIdPart[1].Trim());
                        }
                    }
                }
            }
            return pushedLogIds;
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
                e.Cancel = true;  // 取消退出动作
                Hide();  // 隐藏主窗口
                notifyIcon.Visible = true;  // 显示系统托盘图标
            }
        }

        private void ExitMenuItem_Click(object sender, EventArgs e)
        {
            isMonitoring = false;  // 确保停止任务
            logCleanTimer?.Dispose(); // 释放定时器
            Application.Exit();  // 关闭应用程序
        }

        private void CleanLogFile()
        {
            var retentionPeriod = TimeSpan.FromDays(7); // 保留7天的日志
            var validLines = new List<string>();
            string pattern = @"Generated at: (?<generatedTime>[\d\- :]+)";

            if (File.Exists(LogFilePath))
            {
                var lines = File.ReadAllLines(LogFilePath);
                var logEntries = new List<(DateTime generatedTime, string line)>();

                foreach (var line in lines)
                {
                    var match = Regex.Match(line, pattern);
                    if (match.Success)
                    {
                        if (DateTime.TryParse(match.Groups["generatedTime"].Value, out var generatedDate))
                        {
                            logEntries.Add((generatedDate, line));
                        }
                    }
                }

                if (logEntries.Any())
                {
                    // 按生成时间排序
                    logEntries = logEntries.OrderByDescending(entry => entry.generatedTime).ToList();

                    // 取最近七天的日志
                    var cutoffDate = logEntries.First().generatedTime - retentionPeriod;
                    validLines = logEntries.Where(entry => entry.generatedTime >= cutoffDate).Select(entry => entry.line).ToList();

                    File.WriteAllLines(LogFilePath, validLines);
                }
            }
        }

    }
}