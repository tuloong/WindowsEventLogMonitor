namespace WindowsEventLogMonitor
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private ComboBox comboBoxEventSource;
        private Button btnStartLogs;
        private DataGridView dataGridViewLogs;
        private TextBox textBoxApiUrl;
        private Button btnSaveConfig;
        private TableLayoutPanel tableLayoutPanel;
        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenuStrip;

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Initialize NotifyIcon
            notifyIcon = new NotifyIcon();
            // notifyIcon.Icon = SystemIcons.Application;
            notifyIcon.Icon = new Icon("Resources\\app.ico");
            notifyIcon.Text = "Windows Event Log Monitor";
            notifyIcon.Visible = true;

            // Initialize ContextMenuStrip
            contextMenuStrip = new ContextMenuStrip();
            var startMenuItem = new ToolStripMenuItem("Start Logs", null, async (s, e) => await Task.Run(() => StartLogOutput()));
            var stopMenuItem = new ToolStripMenuItem("Stop Logs", null, (s, e) => StopLogOutput());
            var exitMenuItem = new ToolStripMenuItem("Exit", null, (s, e) => ExitMenuItem_Click(s, e));

            contextMenuStrip.Items.Add(startMenuItem);
            contextMenuStrip.Items.Add(stopMenuItem);
            contextMenuStrip.Items.Add(new ToolStripSeparator());
            contextMenuStrip.Items.Add(exitMenuItem);

            notifyIcon.ContextMenuStrip = contextMenuStrip;
            notifyIcon.DoubleClick += (s, e) => this.Show();

            // TableLayoutPanel for layout management
            tableLayoutPanel = new TableLayoutPanel();
            tableLayoutPanel.ColumnCount = 3;
            tableLayoutPanel.RowCount = 3;
            tableLayoutPanel.Dock = DockStyle.Fill;
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            this.Controls.Add(tableLayoutPanel);

            // ComboBox for Event Source
            comboBoxEventSource = new ComboBox();
            comboBoxEventSource.Items.AddRange(new string[] { "MSSQLSERVER", "PerfProc", "ESENT" });
            comboBoxEventSource.Dock = DockStyle.Fill;
            tableLayoutPanel.Controls.Add(comboBoxEventSource, 0, 0);

            // Button to Load Logs
            btnStartLogs = new Button();
            btnStartLogs.Text = "Start Logs";
            btnStartLogs.Dock = DockStyle.Fill;
            btnStartLogs.Click += BtnStartLogs_Click;
            tableLayoutPanel.Controls.Add(btnStartLogs, 1, 0);

            // DataGridView to Display Logs
            dataGridViewLogs = new DataGridView();
            dataGridViewLogs.Dock = DockStyle.Fill;
            dataGridViewLogs.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewLogs.AllowUserToAddRows = false;
            dataGridViewLogs.AllowUserToDeleteRows = false;
            dataGridViewLogs.ReadOnly = true;
            dataGridViewLogs.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            tableLayoutPanel.Controls.Add(dataGridViewLogs, 0, 1);
            tableLayoutPanel.SetColumnSpan(dataGridViewLogs, 3);

            // TextBox for API URL
            textBoxApiUrl = new TextBox();
            textBoxApiUrl.Dock = DockStyle.Fill;
            tableLayoutPanel.Controls.Add(textBoxApiUrl, 0, 2);
            tableLayoutPanel.SetColumnSpan(textBoxApiUrl, 2);
            textBoxApiUrl.Text = Config.LoadConfig()?.ApiUrl;

            // Button to Save Config
            btnSaveConfig = new Button();
            btnSaveConfig.Text = "Save Config";
            btnSaveConfig.Dock = DockStyle.Fill;
            btnSaveConfig.Click += BtnSaveConfig_Click;
            tableLayoutPanel.Controls.Add(btnSaveConfig, 2, 2);

            // Form settings
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Text = "WindowsEventLogMonitor";
            this.Icon = new Icon("Resources\\app.ico");
            this.ResumeLayout(false);
        }


        #endregion
    }
}
