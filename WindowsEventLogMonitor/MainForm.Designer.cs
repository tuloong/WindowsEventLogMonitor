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

        private TabControl tabControlMain;
        private TabPage tabPageSQLServer;
        private TabPage tabPageGeneralLogs;
        private TabPage tabPageConfiguration;
        private TabPage tabPageService;
        private TabPage tabPageStatus;

        // SQL Server 监控页面控件
        private GroupBox groupBoxSQLServerMonitoring;
        private CheckBox checkBoxEnableSQLServerMonitoring;
        private NumericUpDown numericUpDownMonitorInterval;
        private NumericUpDown numericUpDownBatchSize;
        private CheckBox checkBoxIncludeMSSQLSERVER;
        private CheckBox checkBoxIncludeWindowsAuth;
        private Button btnStartSQLServerMonitoring;
        private Button btnStopSQLServerMonitoring;
        private Button btnRefreshSQLServerLogs;
        private DataGridView dataGridViewSQLServerLogs;
        private Label lblMonitorStatus;
        private Label lblLogCount;
        private Label lblAutoRefreshStatus;

        // 通用日志监控页面控件
        private ComboBox comboBoxEventSource;
        private Button btnStartLogs;
        private DataGridView dataGridViewLogs;
        private Button btnLoadLogs;
        private Button btnPushLogs;

        // 配置页面控件
        private GroupBox groupBoxAPIConfig;
        private TextBox textBoxApiUrl;
        private TextBox textBoxApiKey;
        private CheckBox checkBoxUseHttps;
        private NumericUpDown numericUpDownTimeout;
        private Button btnSaveConfig;
        private Button btnTestConnection;

        private GroupBox groupBoxRetryPolicy;
        private CheckBox checkBoxEnableRetry;
        private NumericUpDown numericUpDownMaxRetries;
        private NumericUpDown numericUpDownRetryDelay;

        private GroupBox groupBoxLogRetention;
        private NumericUpDown numericUpDownRetentionDays;
        private NumericUpDown numericUpDownMaxLogFileSize;

        // 服务管理页面控件
        private GroupBox groupBoxServiceManagement;
        private Button btnInstallService;
        private Button btnUninstallService;
        private Button btnStartService;
        private Button btnStopService;
        private Label lblServiceStatus;
        private TextBox textBoxServiceLog;

        // 状态页面控件
        private GroupBox groupBoxStatistics;
        private Label lblTotalLogsProcessed;
        private Label lblLogsUploaded;
        private Label lblUploadErrors;
        private Label lblLastUploadTime;
        private DataGridView dataGridViewRecentErrors;
        private Button btnClearErrors;
        private ProgressBar progressBarUpload;

        // 系统托盘
        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenuStrip;

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Initialize main form
            this.Text = "SQL Server 日志监控器 v2.0";
            this.Size = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(800, 600);

            // Initialize NotifyIcon
            InitializeNotifyIcon();

            // Initialize main tab control
            InitializeMainTabControl();

            // Initialize individual tabs
            InitializeSQLServerTab();
            InitializeGeneralLogsTab();
            InitializeConfigurationTab();
            InitializeServiceTab();
            InitializeStatusTab();

            this.Controls.Add(tabControlMain);
            this.ResumeLayout(false);
        }

        private void InitializeNotifyIcon()
        {
            notifyIcon = new NotifyIcon();
            try
            {
                notifyIcon.Icon = new Icon("Resources\\app.ico");
            }
            catch
            {
                notifyIcon.Icon = SystemIcons.Application;
            }
            notifyIcon.Text = "SQL Server 日志监控器";
            notifyIcon.Visible = false;

            // Context menu for tray icon
            contextMenuStrip = new ContextMenuStrip();
            var showMenuItem = new ToolStripMenuItem("显示主界面", null, (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; });
            var startSQLMenuItem = new ToolStripMenuItem("启动SQL Server监控", null, (s, e) => StartSQLServerMonitoringFromTray());
            var stopSQLMenuItem = new ToolStripMenuItem("停止SQL Server监控", null, (s, e) => StopSQLServerMonitoring());
            var exitMenuItem = new ToolStripMenuItem("退出", null, ExitMenuItem_Click);

            contextMenuStrip.Items.AddRange(new ToolStripItem[] {
                showMenuItem,
                new ToolStripSeparator(),
                startSQLMenuItem,
                stopSQLMenuItem,
                new ToolStripSeparator(),
                exitMenuItem
            });

            notifyIcon.ContextMenuStrip = contextMenuStrip;
            notifyIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; };
        }

        private void InitializeMainTabControl()
        {
            tabControlMain = new TabControl();
            tabControlMain.Dock = DockStyle.Fill;
            tabControlMain.Font = new Font("微软雅黑", 9F);

            // Create tab pages
            tabPageSQLServer = new TabPage("SQL Server 监控");
            tabPageGeneralLogs = new TabPage("通用日志监控");
            tabPageConfiguration = new TabPage("配置管理");
            tabPageService = new TabPage("服务管理");
            tabPageStatus = new TabPage("状态统计");

            tabControlMain.TabPages.AddRange(new TabPage[] {
                tabPageSQLServer,
                tabPageGeneralLogs,
                tabPageConfiguration,
                tabPageService,
                tabPageStatus
            });
        }

        private void InitializeSQLServerTab()
        {
            var mainPanel = new TableLayoutPanel();
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.ColumnCount = 1;
            mainPanel.RowCount = 3;
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 220F)); // 增加配置区域高度
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainPanel.Padding = new Padding(10); // 添加主面板内边距

            // SQL Server 监控配置组
            groupBoxSQLServerMonitoring = new GroupBox();
            groupBoxSQLServerMonitoring.Text = "SQL Server 监控配置";
            groupBoxSQLServerMonitoring.Dock = DockStyle.Fill;
            groupBoxSQLServerMonitoring.Padding = new Padding(15, 20, 15, 15); // 增加内边距
            groupBoxSQLServerMonitoring.Font = new Font("微软雅黑", 9F, FontStyle.Bold);

            var configPanel = new TableLayoutPanel();
            configPanel.Dock = DockStyle.Fill;
            configPanel.ColumnCount = 5;
            configPanel.RowCount = 5;
            configPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            configPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            configPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            configPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            configPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            configPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F)); // 增加行高
            configPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            configPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 按钮行更高
            configPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F)); // 状态行
            configPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            configPanel.Padding = new Padding(5); // 配置面板内边距

            // 第一行
            checkBoxEnableSQLServerMonitoring = new CheckBox();
            checkBoxEnableSQLServerMonitoring.Text = "启用SQL Server监控";
            checkBoxEnableSQLServerMonitoring.Dock = DockStyle.Fill;
            checkBoxEnableSQLServerMonitoring.Font = new Font("微软雅黑", 9F);
            checkBoxEnableSQLServerMonitoring.Margin = new Padding(3, 5, 3, 5); // 添加边距
            checkBoxEnableSQLServerMonitoring.CheckedChanged += CheckBoxEnableSQLServerMonitoring_CheckedChanged;
            configPanel.Controls.Add(checkBoxEnableSQLServerMonitoring, 0, 0);

            checkBoxIncludeMSSQLSERVER = new CheckBox();
            checkBoxIncludeMSSQLSERVER.Text = "包含MSSQLSERVER事件";
            checkBoxIncludeMSSQLSERVER.Dock = DockStyle.Fill;
            checkBoxIncludeMSSQLSERVER.Font = new Font("微软雅黑", 9F);
            checkBoxIncludeMSSQLSERVER.Margin = new Padding(3, 5, 3, 5);
            checkBoxIncludeMSSQLSERVER.Checked = true;
            configPanel.Controls.Add(checkBoxIncludeMSSQLSERVER, 1, 0);

            checkBoxIncludeWindowsAuth = new CheckBox();
            checkBoxIncludeWindowsAuth.Text = "包含Windows身份验证";
            checkBoxIncludeWindowsAuth.Dock = DockStyle.Fill;
            checkBoxIncludeWindowsAuth.Font = new Font("微软雅黑", 9F);
            checkBoxIncludeWindowsAuth.Margin = new Padding(3, 5, 3, 5);
            checkBoxIncludeWindowsAuth.Checked = true;
            configPanel.Controls.Add(checkBoxIncludeWindowsAuth, 2, 0);
            configPanel.SetColumnSpan(checkBoxIncludeWindowsAuth, 2); // 跨越两列

            // 第二行
            var lblMonitorInterval = new Label();
            lblMonitorInterval.Text = "监控间隔(秒):";
            lblMonitorInterval.Dock = DockStyle.Fill;
            lblMonitorInterval.Font = new Font("微软雅黑", 9F);
            lblMonitorInterval.TextAlign = ContentAlignment.MiddleLeft;
            lblMonitorInterval.Margin = new Padding(3, 5, 3, 5);
            configPanel.Controls.Add(lblMonitorInterval, 0, 1);

            numericUpDownMonitorInterval = new NumericUpDown();
            numericUpDownMonitorInterval.Minimum = 10;
            numericUpDownMonitorInterval.Maximum = 3600;
            numericUpDownMonitorInterval.Value = 30;
            numericUpDownMonitorInterval.Dock = DockStyle.Fill;
            numericUpDownMonitorInterval.Font = new Font("微软雅黑", 9F);
            numericUpDownMonitorInterval.Margin = new Padding(3, 5, 3, 5);
            configPanel.Controls.Add(numericUpDownMonitorInterval, 1, 1);

            var lblBatchSize = new Label();
            lblBatchSize.Text = "批处理大小:";
            lblBatchSize.Dock = DockStyle.Fill;
            lblBatchSize.Font = new Font("微软雅黑", 9F);
            lblBatchSize.TextAlign = ContentAlignment.MiddleLeft;
            lblBatchSize.Margin = new Padding(3, 5, 3, 5);
            configPanel.Controls.Add(lblBatchSize, 2, 1);

            numericUpDownBatchSize = new NumericUpDown();
            numericUpDownBatchSize.Minimum = 1;
            numericUpDownBatchSize.Maximum = 100;
            numericUpDownBatchSize.Value = 10;
            numericUpDownBatchSize.Dock = DockStyle.Fill;
            numericUpDownBatchSize.Font = new Font("微软雅黑", 9F);
            numericUpDownBatchSize.Margin = new Padding(3, 5, 3, 5);
            configPanel.Controls.Add(numericUpDownBatchSize, 3, 1);

            // 第三行 - 控制按钮
            btnStartSQLServerMonitoring = new Button();
            btnStartSQLServerMonitoring.Text = "启动监控";
            btnStartSQLServerMonitoring.Dock = DockStyle.Fill;
            btnStartSQLServerMonitoring.BackColor = Color.LightGreen;
            btnStartSQLServerMonitoring.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            btnStartSQLServerMonitoring.Margin = new Padding(5, 8, 5, 8); // 增加按钮边距
            btnStartSQLServerMonitoring.FlatStyle = FlatStyle.Flat;
            btnStartSQLServerMonitoring.FlatAppearance.BorderSize = 0;
            btnStartSQLServerMonitoring.Click += BtnStartSQLServerMonitoring_Click;
            configPanel.Controls.Add(btnStartSQLServerMonitoring, 0, 2);

            btnStopSQLServerMonitoring = new Button();
            btnStopSQLServerMonitoring.Text = "停止监控";
            btnStopSQLServerMonitoring.Dock = DockStyle.Fill;
            btnStopSQLServerMonitoring.BackColor = Color.LightCoral;
            btnStopSQLServerMonitoring.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            btnStopSQLServerMonitoring.Margin = new Padding(5, 8, 5, 8);
            btnStopSQLServerMonitoring.FlatStyle = FlatStyle.Flat;
            btnStopSQLServerMonitoring.FlatAppearance.BorderSize = 0;
            btnStopSQLServerMonitoring.Enabled = false;
            btnStopSQLServerMonitoring.Click += BtnStopSQLServerMonitoring_Click;
            configPanel.Controls.Add(btnStopSQLServerMonitoring, 1, 2);

            btnRefreshSQLServerLogs = new Button();
            btnRefreshSQLServerLogs.Text = "刷新日志";
            btnRefreshSQLServerLogs.Dock = DockStyle.Fill;
            btnRefreshSQLServerLogs.BackColor = Color.LightYellow;
            btnRefreshSQLServerLogs.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            btnRefreshSQLServerLogs.Margin = new Padding(5, 8, 5, 8);
            btnRefreshSQLServerLogs.FlatStyle = FlatStyle.Flat;
            btnRefreshSQLServerLogs.FlatAppearance.BorderSize = 0;
            btnRefreshSQLServerLogs.Click += BtnRefreshSQLServerLogs_Click;
            configPanel.Controls.Add(btnRefreshSQLServerLogs, 2, 2);

            lblMonitorStatus = new Label();
            lblMonitorStatus.Text = "状态: 未启动";
            lblMonitorStatus.Dock = DockStyle.Fill;
            lblMonitorStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblMonitorStatus.ForeColor = Color.Red;
            lblMonitorStatus.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            lblMonitorStatus.Margin = new Padding(3, 8, 3, 8);
            configPanel.Controls.Add(lblMonitorStatus, 3, 2);

            lblLogCount = new Label();
            lblLogCount.Text = "日志数量: 0";
            lblLogCount.Dock = DockStyle.Fill;
            lblLogCount.TextAlign = ContentAlignment.MiddleLeft;
            lblLogCount.Font = new Font("微软雅黑", 9F);
            lblLogCount.Margin = new Padding(3, 8, 3, 8);
            configPanel.Controls.Add(lblLogCount, 4, 2);

            // 第四行 - 自动刷新状态
            lblAutoRefreshStatus = new Label();
            lblAutoRefreshStatus.Text = "自动刷新: 每10秒";
            lblAutoRefreshStatus.Dock = DockStyle.Fill;
            lblAutoRefreshStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblAutoRefreshStatus.Font = new Font("微软雅黑", 8F);
            lblAutoRefreshStatus.ForeColor = Color.Blue;
            lblAutoRefreshStatus.Margin = new Padding(3, 3, 3, 3);
            configPanel.Controls.Add(lblAutoRefreshStatus, 0, 3);
            configPanel.SetColumnSpan(lblAutoRefreshStatus, 5); // 跨越所有列

            groupBoxSQLServerMonitoring.Controls.Add(configPanel);

            // SQL Server 日志显示
            dataGridViewSQLServerLogs = new DataGridView();
            dataGridViewSQLServerLogs.Dock = DockStyle.Fill;
            dataGridViewSQLServerLogs.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewSQLServerLogs.AllowUserToAddRows = false;
            dataGridViewSQLServerLogs.AllowUserToDeleteRows = false;
            dataGridViewSQLServerLogs.ReadOnly = true;
            dataGridViewSQLServerLogs.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewSQLServerLogs.AlternatingRowsDefaultCellStyle.BackColor = Color.AliceBlue;
            dataGridViewSQLServerLogs.Font = new Font("微软雅黑", 9F);
            dataGridViewSQLServerLogs.Margin = new Padding(10, 5, 10, 5); // 添加表格边距
            dataGridViewSQLServerLogs.RowHeadersVisible = false; // 隐藏行头
            dataGridViewSQLServerLogs.GridColor = Color.LightGray;
            dataGridViewSQLServerLogs.BorderStyle = BorderStyle.Fixed3D;

            // 设置列头样式
            dataGridViewSQLServerLogs.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 240, 240);
            dataGridViewSQLServerLogs.ColumnHeadersDefaultCellStyle.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            dataGridViewSQLServerLogs.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
            dataGridViewSQLServerLogs.ColumnHeadersHeight = 35; // 增加列头高度
            dataGridViewSQLServerLogs.RowTemplate.Height = 28; // 增加行高

            // 添加列
            dataGridViewSQLServerLogs.Columns.Add("TimeGenerated", "时间");
            dataGridViewSQLServerLogs.Columns.Add("LogType", "类型");
            dataGridViewSQLServerLogs.Columns.Add("UserName", "用户名");
            dataGridViewSQLServerLogs.Columns.Add("ClientIP", "客户端IP");
            dataGridViewSQLServerLogs.Columns.Add("DatabaseName", "数据库");
            dataGridViewSQLServerLogs.Columns.Add("EventId", "事件ID");
            dataGridViewSQLServerLogs.Columns.Add("Message", "消息");

            // 设置列宽比例
            dataGridViewSQLServerLogs.Columns["TimeGenerated"].FillWeight = 15;
            dataGridViewSQLServerLogs.Columns["LogType"].FillWeight = 12;
            dataGridViewSQLServerLogs.Columns["UserName"].FillWeight = 12;
            dataGridViewSQLServerLogs.Columns["ClientIP"].FillWeight = 12;
            dataGridViewSQLServerLogs.Columns["DatabaseName"].FillWeight = 12;
            dataGridViewSQLServerLogs.Columns["EventId"].FillWeight = 8;
            dataGridViewSQLServerLogs.Columns["Message"].FillWeight = 29;

            // 状态栏
            var statusPanel = new Panel();
            statusPanel.Dock = DockStyle.Fill;
            statusPanel.BackColor = Color.FromArgb(245, 245, 245);
            statusPanel.Padding = new Padding(15, 10, 15, 10); // 增加状态栏内边距

            var statusLabel = new Label();
            statusLabel.Text = "SQL Server 登录日志监控 - 实时显示登录成功和失败事件";
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Font = new Font("微软雅黑", 9F);
            statusLabel.ForeColor = Color.FromArgb(100, 100, 100);
            statusPanel.Controls.Add(statusLabel);

            mainPanel.Controls.Add(groupBoxSQLServerMonitoring, 0, 0);
            mainPanel.Controls.Add(dataGridViewSQLServerLogs, 0, 1);
            mainPanel.Controls.Add(statusPanel, 0, 2);

            tabPageSQLServer.Controls.Add(mainPanel);
        }

        private void InitializeGeneralLogsTab()
        {
            var mainPanel = new TableLayoutPanel();
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.ColumnCount = 1;
            mainPanel.RowCount = 3;
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F)); // 增加控制面板高度
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainPanel.Padding = new Padding(10); // 添加主面板内边距

            // 控制面板组
            var groupBoxGeneralControl = new GroupBox();
            groupBoxGeneralControl.Text = "通用日志监控配置";
            groupBoxGeneralControl.Dock = DockStyle.Fill;
            groupBoxGeneralControl.Padding = new Padding(15, 20, 15, 15);
            groupBoxGeneralControl.Font = new Font("微软雅黑", 9F, FontStyle.Bold);

            var controlPanel = new TableLayoutPanel();
            controlPanel.Dock = DockStyle.Fill;
            controlPanel.ColumnCount = 4;
            controlPanel.RowCount = 2;
            controlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            controlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            controlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            controlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            controlPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            controlPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            controlPanel.Padding = new Padding(5);

            var lblEventSource = new Label();
            lblEventSource.Text = "事件源:";
            lblEventSource.Dock = DockStyle.Fill;
            lblEventSource.TextAlign = ContentAlignment.MiddleLeft;
            lblEventSource.Font = new Font("微软雅黑", 9F);
            lblEventSource.Margin = new Padding(3, 5, 3, 5);
            controlPanel.Controls.Add(lblEventSource, 0, 0);

            comboBoxEventSource = new ComboBox();
            comboBoxEventSource.Items.AddRange(new string[] { "MSSQLSERVER", "PerfProc", "ESENT", "Application Error" });
            comboBoxEventSource.Dock = DockStyle.Fill;
            comboBoxEventSource.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxEventSource.Font = new Font("微软雅黑", 9F);
            comboBoxEventSource.Margin = new Padding(3, 5, 3, 5);
            controlPanel.Controls.Add(comboBoxEventSource, 1, 0);

            btnLoadLogs = new Button();
            btnLoadLogs.Text = "加载日志";
            btnLoadLogs.Dock = DockStyle.Fill;
            btnLoadLogs.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            btnLoadLogs.Margin = new Padding(5, 8, 5, 8);
            btnLoadLogs.FlatStyle = FlatStyle.Flat;
            btnLoadLogs.FlatAppearance.BorderSize = 0;
            btnLoadLogs.BackColor = Color.LightBlue;
            btnLoadLogs.Click += BtnLoadLogs_Click;
            controlPanel.Controls.Add(btnLoadLogs, 2, 0);

            btnPushLogs = new Button();
            btnPushLogs.Text = "推送日志";
            btnPushLogs.Dock = DockStyle.Fill;
            btnPushLogs.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            btnPushLogs.Margin = new Padding(5, 8, 5, 8);
            btnPushLogs.FlatStyle = FlatStyle.Flat;
            btnPushLogs.FlatAppearance.BorderSize = 0;
            btnPushLogs.BackColor = Color.LightYellow;
            btnPushLogs.Click += BtnPushLogs_Click;
            controlPanel.Controls.Add(btnPushLogs, 3, 0);

            btnStartLogs = new Button();
            btnStartLogs.Text = "开始监控";
            btnStartLogs.Dock = DockStyle.Fill;
            btnStartLogs.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            btnStartLogs.Margin = new Padding(5, 8, 5, 8);
            btnStartLogs.FlatStyle = FlatStyle.Flat;
            btnStartLogs.FlatAppearance.BorderSize = 0;
            btnStartLogs.BackColor = Color.LightGreen;
            btnStartLogs.Click += BtnStartLogs_Click;
            controlPanel.Controls.Add(btnStartLogs, 0, 1);
            controlPanel.SetColumnSpan(btnStartLogs, 2);

            groupBoxGeneralControl.Controls.Add(controlPanel);

            // 通用日志显示
            dataGridViewLogs = new DataGridView();
            dataGridViewLogs.Dock = DockStyle.Fill;
            dataGridViewLogs.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewLogs.AllowUserToAddRows = false;
            dataGridViewLogs.AllowUserToDeleteRows = false;
            dataGridViewLogs.ReadOnly = true;
            dataGridViewLogs.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewLogs.AlternatingRowsDefaultCellStyle.BackColor = Color.AliceBlue;
            dataGridViewLogs.Font = new Font("微软雅黑", 9F);
            dataGridViewLogs.Margin = new Padding(10, 5, 10, 5);
            dataGridViewLogs.RowHeadersVisible = false;
            dataGridViewLogs.GridColor = Color.LightGray;
            dataGridViewLogs.BorderStyle = BorderStyle.Fixed3D;
            dataGridViewLogs.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 240, 240);
            dataGridViewLogs.ColumnHeadersDefaultCellStyle.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            dataGridViewLogs.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
            dataGridViewLogs.ColumnHeadersHeight = 35;
            dataGridViewLogs.RowTemplate.Height = 28;

            // 状态栏
            var statusPanel = new Panel();
            statusPanel.Dock = DockStyle.Fill;
            statusPanel.BackColor = Color.FromArgb(245, 245, 245);
            statusPanel.Padding = new Padding(15, 10, 15, 10);

            var statusLabel = new Label();
            statusLabel.Text = "通用Windows事件日志监控 - 支持多种事件源的日志监控";
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Font = new Font("微软雅黑", 9F);
            statusLabel.ForeColor = Color.FromArgb(100, 100, 100);
            statusPanel.Controls.Add(statusLabel);

            mainPanel.Controls.Add(groupBoxGeneralControl, 0, 0);
            mainPanel.Controls.Add(dataGridViewLogs, 0, 1);
            mainPanel.Controls.Add(statusPanel, 0, 2);

            tabPageGeneralLogs.Controls.Add(mainPanel);
        }

        private void InitializeConfigurationTab()
        {
            var scrollPanel = new Panel();
            scrollPanel.Dock = DockStyle.Fill;
            scrollPanel.AutoScroll = true;
            scrollPanel.Padding = new Padding(10); // 添加滚动面板内边距

            var mainPanel = new TableLayoutPanel();
            mainPanel.ColumnCount = 2;
            mainPanel.RowCount = 4;
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 220F)); // 增加API配置组高度
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 170F)); // 增加重试策略组高度
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 140F)); // 增加日志保留组高度
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            mainPanel.Dock = DockStyle.Top;
            mainPanel.Height = 610; // 增加总高度
            mainPanel.Padding = new Padding(5); // 添加主面板内边距

            // API配置组
            InitializeAPIConfigGroup();
            mainPanel.Controls.Add(groupBoxAPIConfig, 0, 0);

            // 重试策略组
            InitializeRetryPolicyGroup();
            mainPanel.Controls.Add(groupBoxRetryPolicy, 1, 0);

            // 日志保留组
            InitializeLogRetentionGroup();
            mainPanel.Controls.Add(groupBoxLogRetention, 0, 1);

            // 按钮面板
            var buttonPanel = new TableLayoutPanel();
            buttonPanel.Dock = DockStyle.Fill;
            buttonPanel.ColumnCount = 3;
            buttonPanel.RowCount = 1;
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
            buttonPanel.Padding = new Padding(10, 15, 10, 15); // 增加按钮面板内边距

            btnSaveConfig = new Button();
            btnSaveConfig.Text = "保存配置";
            btnSaveConfig.Dock = DockStyle.Fill;
            btnSaveConfig.BackColor = Color.LightBlue;
            btnSaveConfig.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            btnSaveConfig.Margin = new Padding(5, 8, 5, 8);
            btnSaveConfig.FlatStyle = FlatStyle.Flat;
            btnSaveConfig.FlatAppearance.BorderSize = 0;
            btnSaveConfig.Click += BtnSaveConfig_Click;
            buttonPanel.Controls.Add(btnSaveConfig, 0, 0);

            btnTestConnection = new Button();
            btnTestConnection.Text = "测试连接";
            btnTestConnection.Dock = DockStyle.Fill;
            btnTestConnection.BackColor = Color.LightGreen;
            btnTestConnection.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            btnTestConnection.Margin = new Padding(5, 8, 5, 8);
            btnTestConnection.FlatStyle = FlatStyle.Flat;
            btnTestConnection.FlatAppearance.BorderSize = 0;
            btnTestConnection.Click += BtnTestConnection_Click;
            buttonPanel.Controls.Add(btnTestConnection, 1, 0);

            mainPanel.Controls.Add(buttonPanel, 0, 3);
            mainPanel.SetColumnSpan(buttonPanel, 2);

            scrollPanel.Controls.Add(mainPanel);
            tabPageConfiguration.Controls.Add(scrollPanel);
        }

        private void InitializeAPIConfigGroup()
        {
            groupBoxAPIConfig = new GroupBox();
            groupBoxAPIConfig.Text = "API配置";
            groupBoxAPIConfig.Dock = DockStyle.Fill;
            groupBoxAPIConfig.Padding = new Padding(15, 20, 15, 15);
            groupBoxAPIConfig.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            groupBoxAPIConfig.Margin = new Padding(5);

            var configTable = new TableLayoutPanel();
            configTable.Dock = DockStyle.Fill;
            configTable.ColumnCount = 2;
            configTable.RowCount = 4;
            configTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            configTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            configTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            configTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            configTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            configTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            configTable.Padding = new Padding(5);

            // API URL
            var lblApiUrl = new Label();
            lblApiUrl.Text = "API地址:";
            lblApiUrl.Dock = DockStyle.Fill;
            lblApiUrl.TextAlign = ContentAlignment.MiddleLeft;
            lblApiUrl.Font = new Font("微软雅黑", 9F);
            lblApiUrl.Margin = new Padding(3, 5, 3, 5);
            configTable.Controls.Add(lblApiUrl, 0, 0);

            textBoxApiUrl = new TextBox();
            textBoxApiUrl.Dock = DockStyle.Fill;
            textBoxApiUrl.PlaceholderText = "https://your-server.com/api/logs";
            textBoxApiUrl.Font = new Font("微软雅黑", 9F);
            textBoxApiUrl.Margin = new Padding(3, 5, 3, 5);
            configTable.Controls.Add(textBoxApiUrl, 1, 0);

            // API Key
            var lblApiKey = new Label();
            lblApiKey.Text = "API密钥:";
            lblApiKey.Dock = DockStyle.Fill;
            lblApiKey.TextAlign = ContentAlignment.MiddleLeft;
            lblApiKey.Font = new Font("微软雅黑", 9F);
            lblApiKey.Margin = new Padding(3, 5, 3, 5);
            configTable.Controls.Add(lblApiKey, 0, 1);

            textBoxApiKey = new TextBox();
            textBoxApiKey.Dock = DockStyle.Fill;
            textBoxApiKey.UseSystemPasswordChar = true;
            textBoxApiKey.PlaceholderText = "输入API密钥";
            textBoxApiKey.Font = new Font("微软雅黑", 9F);
            textBoxApiKey.Margin = new Padding(3, 5, 3, 5);
            configTable.Controls.Add(textBoxApiKey, 1, 1);

            // HTTPS
            checkBoxUseHttps = new CheckBox();
            checkBoxUseHttps.Text = "强制使用HTTPS";
            checkBoxUseHttps.Dock = DockStyle.Fill;
            checkBoxUseHttps.Font = new Font("微软雅黑", 9F);
            checkBoxUseHttps.Margin = new Padding(3, 5, 3, 5);
            configTable.Controls.Add(checkBoxUseHttps, 0, 2);
            configTable.SetColumnSpan(checkBoxUseHttps, 2);

            // Timeout
            var lblTimeout = new Label();
            lblTimeout.Text = "超时(秒):";
            lblTimeout.Dock = DockStyle.Fill;
            lblTimeout.TextAlign = ContentAlignment.MiddleLeft;
            lblTimeout.Font = new Font("微软雅黑", 9F);
            lblTimeout.Margin = new Padding(3, 5, 3, 5);
            configTable.Controls.Add(lblTimeout, 0, 3);

            numericUpDownTimeout = new NumericUpDown();
            numericUpDownTimeout.Minimum = 10;
            numericUpDownTimeout.Maximum = 300;
            numericUpDownTimeout.Value = 30;
            numericUpDownTimeout.Dock = DockStyle.Fill;
            numericUpDownTimeout.Font = new Font("微软雅黑", 9F);
            numericUpDownTimeout.Margin = new Padding(3, 5, 3, 5);
            configTable.Controls.Add(numericUpDownTimeout, 1, 3);

            groupBoxAPIConfig.Controls.Add(configTable);
        }

        private void InitializeRetryPolicyGroup()
        {
            groupBoxRetryPolicy = new GroupBox();
            groupBoxRetryPolicy.Text = "重试策略";
            groupBoxRetryPolicy.Dock = DockStyle.Fill;
            groupBoxRetryPolicy.Padding = new Padding(15, 20, 15, 15);
            groupBoxRetryPolicy.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            groupBoxRetryPolicy.Margin = new Padding(5);

            var retryTable = new TableLayoutPanel();
            retryTable.Dock = DockStyle.Fill;
            retryTable.ColumnCount = 2;
            retryTable.RowCount = 3;
            retryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            retryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            retryTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            retryTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            retryTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            retryTable.Padding = new Padding(5);

            checkBoxEnableRetry = new CheckBox();
            checkBoxEnableRetry.Text = "启用重试机制";
            checkBoxEnableRetry.Dock = DockStyle.Fill;
            checkBoxEnableRetry.Checked = true;
            checkBoxEnableRetry.Font = new Font("微软雅黑", 9F);
            checkBoxEnableRetry.Margin = new Padding(3, 5, 3, 5);
            retryTable.Controls.Add(checkBoxEnableRetry, 0, 0);
            retryTable.SetColumnSpan(checkBoxEnableRetry, 2);

            var lblMaxRetries = new Label();
            lblMaxRetries.Text = "最大重试次数:";
            lblMaxRetries.Dock = DockStyle.Fill;
            lblMaxRetries.TextAlign = ContentAlignment.MiddleLeft;
            lblMaxRetries.Font = new Font("微软雅黑", 9F);
            lblMaxRetries.Margin = new Padding(3, 5, 3, 5);
            retryTable.Controls.Add(lblMaxRetries, 0, 1);

            numericUpDownMaxRetries = new NumericUpDown();
            numericUpDownMaxRetries.Minimum = 1;
            numericUpDownMaxRetries.Maximum = 10;
            numericUpDownMaxRetries.Value = 3;
            numericUpDownMaxRetries.Dock = DockStyle.Fill;
            numericUpDownMaxRetries.Font = new Font("微软雅黑", 9F);
            numericUpDownMaxRetries.Margin = new Padding(3, 5, 3, 5);
            retryTable.Controls.Add(numericUpDownMaxRetries, 1, 1);

            var lblRetryDelay = new Label();
            lblRetryDelay.Text = "重试延迟(秒):";
            lblRetryDelay.Dock = DockStyle.Fill;
            lblRetryDelay.TextAlign = ContentAlignment.MiddleLeft;
            lblRetryDelay.Font = new Font("微软雅黑", 9F);
            lblRetryDelay.Margin = new Padding(3, 5, 3, 5);
            retryTable.Controls.Add(lblRetryDelay, 0, 2);

            numericUpDownRetryDelay = new NumericUpDown();
            numericUpDownRetryDelay.Minimum = 1;
            numericUpDownRetryDelay.Maximum = 60;
            numericUpDownRetryDelay.Value = 5;
            numericUpDownRetryDelay.Dock = DockStyle.Fill;
            numericUpDownRetryDelay.Font = new Font("微软雅黑", 9F);
            numericUpDownRetryDelay.Margin = new Padding(3, 5, 3, 5);
            retryTable.Controls.Add(numericUpDownRetryDelay, 1, 2);

            groupBoxRetryPolicy.Controls.Add(retryTable);
        }

        private void InitializeLogRetentionGroup()
        {
            groupBoxLogRetention = new GroupBox();
            groupBoxLogRetention.Text = "日志保留设置";
            groupBoxLogRetention.Dock = DockStyle.Fill;
            groupBoxLogRetention.Padding = new Padding(15, 20, 15, 15);
            groupBoxLogRetention.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            groupBoxLogRetention.Margin = new Padding(5);

            var retentionTable = new TableLayoutPanel();
            retentionTable.Dock = DockStyle.Fill;
            retentionTable.ColumnCount = 2;
            retentionTable.RowCount = 2;
            retentionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            retentionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            retentionTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            retentionTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            retentionTable.Padding = new Padding(5);

            var lblRetentionDays = new Label();
            lblRetentionDays.Text = "保留天数:";
            lblRetentionDays.Dock = DockStyle.Fill;
            lblRetentionDays.TextAlign = ContentAlignment.MiddleLeft;
            lblRetentionDays.Font = new Font("微软雅黑", 9F);
            lblRetentionDays.Margin = new Padding(3, 5, 3, 5);
            retentionTable.Controls.Add(lblRetentionDays, 0, 0);

            numericUpDownRetentionDays = new NumericUpDown();
            numericUpDownRetentionDays.Minimum = 1;
            numericUpDownRetentionDays.Maximum = 365;
            numericUpDownRetentionDays.Value = 7;
            numericUpDownRetentionDays.Dock = DockStyle.Fill;
            numericUpDownRetentionDays.Font = new Font("微软雅黑", 9F);
            numericUpDownRetentionDays.Margin = new Padding(3, 5, 3, 5);
            retentionTable.Controls.Add(numericUpDownRetentionDays, 1, 0);

            var lblMaxLogFileSize = new Label();
            lblMaxLogFileSize.Text = "最大文件大小(KB):";
            lblMaxLogFileSize.Dock = DockStyle.Fill;
            lblMaxLogFileSize.TextAlign = ContentAlignment.MiddleLeft;
            lblMaxLogFileSize.Font = new Font("微软雅黑", 9F);
            lblMaxLogFileSize.Margin = new Padding(3, 5, 3, 5);
            retentionTable.Controls.Add(lblMaxLogFileSize, 0, 1);

            numericUpDownMaxLogFileSize = new NumericUpDown();
            numericUpDownMaxLogFileSize.Minimum = 100;
            numericUpDownMaxLogFileSize.Maximum = 10240;
            numericUpDownMaxLogFileSize.Value = 500;
            numericUpDownMaxLogFileSize.Dock = DockStyle.Fill;
            numericUpDownMaxLogFileSize.Font = new Font("微软雅黑", 9F);
            numericUpDownMaxLogFileSize.Margin = new Padding(3, 5, 3, 5);
            retentionTable.Controls.Add(numericUpDownMaxLogFileSize, 1, 1);

            groupBoxLogRetention.Controls.Add(retentionTable);
        }

        private void InitializeServiceTab()
        {
            var mainPanel = new TableLayoutPanel();
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.ColumnCount = 1;
            mainPanel.RowCount = 3;
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 180F)); // 调整服务管理组高度
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainPanel.Padding = new Padding(10); // 添加主面板内边距

            // 服务管理组
            groupBoxServiceManagement = new GroupBox();
            groupBoxServiceManagement.Text = "Windows服务管理";
            groupBoxServiceManagement.Dock = DockStyle.Fill;
            groupBoxServiceManagement.Padding = new Padding(15, 20, 15, 15);
            groupBoxServiceManagement.Font = new Font("微软雅黑", 9F, FontStyle.Bold);

            var serviceTable = new TableLayoutPanel();
            serviceTable.Dock = DockStyle.Fill;
            serviceTable.ColumnCount = 4;
            serviceTable.RowCount = 3;
            serviceTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            serviceTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            serviceTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            serviceTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            serviceTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 按钮行高度
            serviceTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F)); // 状态行高度
            serviceTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            serviceTable.Padding = new Padding(5);

            btnInstallService = new Button();
            btnInstallService.Text = "安装服务";
            btnInstallService.Dock = DockStyle.Fill;
            btnInstallService.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            btnInstallService.Margin = new Padding(5, 8, 5, 8);
            btnInstallService.FlatStyle = FlatStyle.Flat;
            btnInstallService.FlatAppearance.BorderSize = 0;
            btnInstallService.BackColor = Color.LightBlue;
            btnInstallService.Click += BtnInstallService_Click;
            serviceTable.Controls.Add(btnInstallService, 0, 0);

            btnUninstallService = new Button();
            btnUninstallService.Text = "卸载服务";
            btnUninstallService.Dock = DockStyle.Fill;
            btnUninstallService.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            btnUninstallService.Margin = new Padding(5, 8, 5, 8);
            btnUninstallService.FlatStyle = FlatStyle.Flat;
            btnUninstallService.FlatAppearance.BorderSize = 0;
            btnUninstallService.BackColor = Color.LightGray;
            btnUninstallService.Click += BtnUninstallService_Click;
            serviceTable.Controls.Add(btnUninstallService, 1, 0);

            btnStartService = new Button();
            btnStartService.Text = "启动服务";
            btnStartService.Dock = DockStyle.Fill;
            btnStartService.BackColor = Color.LightGreen;
            btnStartService.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            btnStartService.Margin = new Padding(5, 8, 5, 8);
            btnStartService.FlatStyle = FlatStyle.Flat;
            btnStartService.FlatAppearance.BorderSize = 0;
            btnStartService.Click += BtnStartService_Click;
            serviceTable.Controls.Add(btnStartService, 2, 0);

            btnStopService = new Button();
            btnStopService.Text = "停止服务";
            btnStopService.Dock = DockStyle.Fill;
            btnStopService.BackColor = Color.LightCoral;
            btnStopService.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            btnStopService.Margin = new Padding(5, 8, 5, 8);
            btnStopService.FlatStyle = FlatStyle.Flat;
            btnStopService.FlatAppearance.BorderSize = 0;
            btnStopService.Click += BtnStopService_Click;
            serviceTable.Controls.Add(btnStopService, 3, 0);

            lblServiceStatus = new Label();
            lblServiceStatus.Text = "服务状态: 检查中...";
            lblServiceStatus.Dock = DockStyle.Fill;
            lblServiceStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblServiceStatus.ForeColor = Color.Blue;
            lblServiceStatus.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            lblServiceStatus.Margin = new Padding(3, 5, 3, 5);
            serviceTable.Controls.Add(lblServiceStatus, 0, 1);
            serviceTable.SetColumnSpan(lblServiceStatus, 4);

            groupBoxServiceManagement.Controls.Add(serviceTable);

            // 服务日志显示
            textBoxServiceLog = new TextBox();
            textBoxServiceLog.Dock = DockStyle.Fill;
            textBoxServiceLog.Multiline = true;
            textBoxServiceLog.ScrollBars = ScrollBars.Both;
            textBoxServiceLog.ReadOnly = true;
            textBoxServiceLog.Font = new Font("Consolas", 9F);
            textBoxServiceLog.BackColor = Color.Black;
            textBoxServiceLog.ForeColor = Color.LightGreen;
            textBoxServiceLog.Margin = new Padding(10, 5, 10, 5);

            // 状态栏
            var statusPanel = new Panel();
            statusPanel.Dock = DockStyle.Fill;
            statusPanel.BackColor = Color.FromArgb(245, 245, 245);
            statusPanel.Padding = new Padding(15, 10, 15, 10);

            var statusLabel = new Label();
            statusLabel.Text = "Windows服务管理 - 安装、启动、停止监控服务";
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Font = new Font("微软雅黑", 9F);
            statusLabel.ForeColor = Color.FromArgb(100, 100, 100);
            statusPanel.Controls.Add(statusLabel);

            mainPanel.Controls.Add(groupBoxServiceManagement, 0, 0);
            mainPanel.Controls.Add(textBoxServiceLog, 0, 1);
            mainPanel.Controls.Add(statusPanel, 0, 2);

            tabPageService.Controls.Add(mainPanel);
        }

        private void InitializeStatusTab()
        {
            var mainPanel = new TableLayoutPanel();
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.ColumnCount = 1;
            mainPanel.RowCount = 3;
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 220F)); // 增加统计信息组高度
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainPanel.Padding = new Padding(10); // 添加主面板内边距

            // 统计信息组
            groupBoxStatistics = new GroupBox();
            groupBoxStatistics.Text = "运行统计";
            groupBoxStatistics.Dock = DockStyle.Fill;
            groupBoxStatistics.Padding = new Padding(15, 20, 15, 15);
            groupBoxStatistics.Font = new Font("微软雅黑", 9F, FontStyle.Bold);

            var statsTable = new TableLayoutPanel();
            statsTable.Dock = DockStyle.Fill;
            statsTable.ColumnCount = 2;
            statsTable.RowCount = 4;
            statsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            statsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            statsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            statsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            statsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            statsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 按钮行更高
            statsTable.Padding = new Padding(5);

            lblTotalLogsProcessed = new Label();
            lblTotalLogsProcessed.Text = "已处理日志: 0";
            lblTotalLogsProcessed.Dock = DockStyle.Fill;
            lblTotalLogsProcessed.TextAlign = ContentAlignment.MiddleLeft;
            lblTotalLogsProcessed.Font = new Font("微软雅黑", 10F, FontStyle.Bold);
            lblTotalLogsProcessed.Margin = new Padding(3, 5, 3, 5);
            statsTable.Controls.Add(lblTotalLogsProcessed, 0, 0);

            lblLogsUploaded = new Label();
            lblLogsUploaded.Text = "已上传日志: 0";
            lblLogsUploaded.Dock = DockStyle.Fill;
            lblLogsUploaded.TextAlign = ContentAlignment.MiddleLeft;
            lblLogsUploaded.Font = new Font("微软雅黑", 10F, FontStyle.Bold);
            lblLogsUploaded.ForeColor = Color.Green;
            lblLogsUploaded.Margin = new Padding(3, 5, 3, 5);
            statsTable.Controls.Add(lblLogsUploaded, 1, 0);

            lblUploadErrors = new Label();
            lblUploadErrors.Text = "上传错误: 0";
            lblUploadErrors.Dock = DockStyle.Fill;
            lblUploadErrors.TextAlign = ContentAlignment.MiddleLeft;
            lblUploadErrors.Font = new Font("微软雅黑", 10F, FontStyle.Bold);
            lblUploadErrors.ForeColor = Color.Red;
            lblUploadErrors.Margin = new Padding(3, 5, 3, 5);
            statsTable.Controls.Add(lblUploadErrors, 0, 1);

            lblLastUploadTime = new Label();
            lblLastUploadTime.Text = "最后上传: 从未";
            lblLastUploadTime.Dock = DockStyle.Fill;
            lblLastUploadTime.TextAlign = ContentAlignment.MiddleLeft;
            lblLastUploadTime.Font = new Font("微软雅黑", 9F);
            lblLastUploadTime.Margin = new Padding(3, 5, 3, 5);
            statsTable.Controls.Add(lblLastUploadTime, 1, 1);

            progressBarUpload = new ProgressBar();
            progressBarUpload.Dock = DockStyle.Fill;
            progressBarUpload.Style = ProgressBarStyle.Continuous;
            progressBarUpload.Margin = new Padding(3, 8, 3, 8);
            progressBarUpload.Height = 20;
            statsTable.Controls.Add(progressBarUpload, 0, 2);
            statsTable.SetColumnSpan(progressBarUpload, 2);

            btnClearErrors = new Button();
            btnClearErrors.Text = "清除错误记录";
            btnClearErrors.Dock = DockStyle.Fill;
            btnClearErrors.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            btnClearErrors.Margin = new Padding(5, 8, 5, 8);
            btnClearErrors.FlatStyle = FlatStyle.Flat;
            btnClearErrors.FlatAppearance.BorderSize = 0;
            btnClearErrors.BackColor = Color.LightYellow;
            btnClearErrors.Click += BtnClearErrors_Click;
            statsTable.Controls.Add(btnClearErrors, 0, 3);

            groupBoxStatistics.Controls.Add(statsTable);

            // 错误日志显示
            dataGridViewRecentErrors = new DataGridView();
            dataGridViewRecentErrors.Dock = DockStyle.Fill;
            dataGridViewRecentErrors.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewRecentErrors.AllowUserToAddRows = false;
            dataGridViewRecentErrors.AllowUserToDeleteRows = false;
            dataGridViewRecentErrors.ReadOnly = true;
            dataGridViewRecentErrors.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewRecentErrors.AlternatingRowsDefaultCellStyle.BackColor = Color.AliceBlue;
            dataGridViewRecentErrors.Font = new Font("微软雅黑", 9F);
            dataGridViewRecentErrors.Margin = new Padding(10, 5, 10, 5);
            dataGridViewRecentErrors.RowHeadersVisible = false;
            dataGridViewRecentErrors.GridColor = Color.LightGray;
            dataGridViewRecentErrors.BorderStyle = BorderStyle.Fixed3D;
            dataGridViewRecentErrors.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 240, 240);
            dataGridViewRecentErrors.ColumnHeadersDefaultCellStyle.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            dataGridViewRecentErrors.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
            dataGridViewRecentErrors.ColumnHeadersHeight = 35;
            dataGridViewRecentErrors.RowTemplate.Height = 28;

            dataGridViewRecentErrors.Columns.Add("Time", "时间");
            dataGridViewRecentErrors.Columns.Add("Error", "错误信息");
            dataGridViewRecentErrors.Columns.Add("Details", "详细信息");

            // 设置列宽比例
            dataGridViewRecentErrors.Columns["Time"].FillWeight = 20;
            dataGridViewRecentErrors.Columns["Error"].FillWeight = 40;
            dataGridViewRecentErrors.Columns["Details"].FillWeight = 40;

            // 状态栏
            var statusPanel = new Panel();
            statusPanel.Dock = DockStyle.Fill;
            statusPanel.BackColor = Color.FromArgb(245, 245, 245);
            statusPanel.Padding = new Padding(15, 10, 15, 10);

            var statusLabel = new Label();
            statusLabel.Text = "系统运行状态和统计信息 - 监控上传状态和错误日志";
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Font = new Font("微软雅黑", 9F);
            statusLabel.ForeColor = Color.FromArgb(100, 100, 100);
            statusPanel.Controls.Add(statusLabel);

            mainPanel.Controls.Add(groupBoxStatistics, 0, 0);
            mainPanel.Controls.Add(dataGridViewRecentErrors, 0, 1);
            mainPanel.Controls.Add(statusPanel, 0, 2);

            tabPageStatus.Controls.Add(mainPanel);
        }

        #endregion
    }
}
