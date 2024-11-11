using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;

namespace WindowsEventLogMonitor
{
    public partial class Form1 : Form
    {
        private EventLogReader eventLogReader;
        private Config config;
        private JsonService jsonService;
        private HttpService httpService;
        private const string LogFilePath = "push_log.ini";

        public Form1()
        {
            InitializeComponent();
            eventLogReader = new EventLogReader("Application");
            config = new Config();
            jsonService = new JsonService();
            httpService = new HttpService();
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
            await httpService.PushLogsToAPI(jsonData, config.ApiUrl);

            // 记录推送时间点和事件主键，并缓存到本地日志
            var logTime = DateTime.Now;
            using (var writer = new StreamWriter(LogFilePath, true))
            {
                foreach (var log in newLogs)
                {
                    var logEntry = $"Log ID: {GenerateUniqueKey(log)}, Pushed at: {logTime}";
                    writer.WriteLine(logEntry);
                    Console.WriteLine(logEntry);
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
            return $"{log.InstanceId}_{log.TimeGenerated.Ticks}_{log.Source}";
        }

        private void BtnSaveConfig_Click(object sender, EventArgs e)
        {
            config.ApiUrl = textBoxApiUrl.Text;
            Config.SaveConfig(config);
        }
    }
}