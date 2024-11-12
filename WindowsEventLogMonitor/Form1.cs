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

namespace WindowsEventLogMonitor
{
    public partial class Form1 : Form
    {

        private EventLogReader eventLogReader;
        private JsonService jsonService;
        private HttpService httpService;
        private const string LogFilePath = "push_log.ini";
        private bool isMonitoring = false;

        public Form1()
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
        }

        private async Task StartLogOutput()
        {
            var selectedSource = comboBoxEventSource.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedSource))
            {
                MessageBox.Show("Please select a source first.");
                return;
            }

            isMonitoring = true;

            while (isMonitoring)
            {
                var logEntries = eventLogReader.GetFilteredEntries(selectedSource, string.Empty); // get logs，filter by source，no filter by event type
                var pushedLogIds = LoadPushedLogIds();

                var newLogs = logEntries.Where(log => !pushedLogIds.Contains(GenerateUniqueKey(log))).ToList();
                if (newLogs.Count > 0)
                {
                    foreach (var logEntry in newLogs)
                    {
                        if (!isMonitoring) break; // 如果监控停止，跳出循环

                        // 限制展示的条目不超过100行
                        if (dataGridViewLogs.Rows.Count >= 100)
                        {
                            dataGridViewLogs.Rows.RemoveAt(0); // 移除最早的一行
                        }

                        dataGridViewLogs.Rows.Add(logEntry.TimeGenerated, logEntry.Message, logEntry.InstanceId, logEntry.EntryType, logEntry.Site, logEntry.Source);
                        if (dataGridViewLogs.RowCount > 0)
                        {
                            dataGridViewLogs.FirstDisplayedScrollingRowIndex = dataGridViewLogs.RowCount - 1; // 滚动到最后一行
                        }

                        await PushSingleLogAsync(logEntry);
                        await Task.Delay(1000); // 每1秒输出一行日志
                    }

                    //await PushLogsAsync(newLogs);
                }
                else
                {
                    await Task.Delay(1000); // 如果没有新日志，每1秒检查一次
                }
            }
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
            await httpService.PushLogsToAPI(json, apiUrl);
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
            await httpService.PushLogsToAPI(json, apiUrl);

            // 记录推送的日志ID
            var logTime = DateTime.Now;
            var logEntryStrings = $"Log ID: {GenerateUniqueKey(logEntry)}, Pushed at: {logTime}";
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

        private void BtnStartLogs_Click(object sender, EventArgs e)
        {
            if (btStartLogs.Text == "Start Logs")
            {
                StartLogOutput();
                btStartLogs.Text = "Cancel Logs"; // 修改按钮文本
                btStartLogs.BackColor = System.Drawing.Color.Red; // 修改按钮背景颜色
            }
            else
            {
                isMonitoring = false;
                btStartLogs.Text = "Start Logs"; // 恢复按钮文本
                btStartLogs.BackColor = System.Drawing.Color.Empty; // 恢复按钮背景颜色
            }
        }

        private void BtnLoadLogs_Click(object sender, EventArgs e)
        {
            var selectedSource = comboBoxEventSource.SelectedItem?.ToString();
            eventLogReader = new EventLogReader("Application");
            var logs = eventLogReader.GetFilteredEntries(selectedSource!, string.Empty);
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

            var logs = eventLogReader.GetFilteredEntries(selectedSource, string.Empty); // get logs，filter by source，no filter by event type
            var pushedLogIds = LoadPushedLogIds();

            var newLogs = logs.Where(log => !pushedLogIds.Contains(GenerateUniqueKey(log))).ToList();
            if (newLogs.Count == 0)
            {
                MessageBox.Show("No new logs to push.");
                return;
            }

            var jsonData = jsonService.ConvertToJSON(newLogs);
            await httpService.PushLogsToAPI(jsonData, Config.GetCachedConfig()?.ApiUrl ?? "");

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
    }
}