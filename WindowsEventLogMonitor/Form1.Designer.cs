namespace WindowsEventLogMonitor
{
    partial class Form1
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
        private Button btnLoadLogs;
        private DataGridView dataGridViewLogs;
        private Button btnPushLogs;
        private TextBox textBoxApiUrl;
        private Button btnSaveConfig;

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            // ComboBox for Event Source
            comboBoxEventSource = new ComboBox();
            comboBoxEventSource.Items.AddRange(new string[] { "MSSQLSERVER", "PerfProc", "ESENT" });
            comboBoxEventSource.Location = new Point(20, 20);
            comboBoxEventSource.Width = 200;
            this.Controls.Add(comboBoxEventSource);

            // Button to Load Logs
            btnLoadLogs = new Button();
            btnLoadLogs.Text = "Load Logs";
            btnLoadLogs.Location = new Point(240, 20);
            btnLoadLogs.Click += BtnLoadLogs_Click;
            this.Controls.Add(btnLoadLogs);

            // DataGridView to Display Logs
            dataGridViewLogs = new DataGridView();
            dataGridViewLogs.Location = new Point(20, 60);
            dataGridViewLogs.Width = 600;
            dataGridViewLogs.Height = 300;
            this.Controls.Add(dataGridViewLogs);

            // Button to Push Logs
            btnPushLogs = new Button();
            btnPushLogs.Text = "Push Logs";
            btnPushLogs.Location = new Point(20, 380);
            btnPushLogs.Click += BtnPushLogs_Click;
            this.Controls.Add(btnPushLogs);

            // TextBox for API URL
            textBoxApiUrl = new TextBox();
            textBoxApiUrl.Location = new Point(20, 420);
            textBoxApiUrl.Width = 400;
            this.Controls.Add(textBoxApiUrl);

            // Button to Save Config
            btnSaveConfig = new Button();
            btnSaveConfig.Text = "Save Config";
            btnSaveConfig.Location = new Point(440, 420);
            btnSaveConfig.Click += BtnSaveConfig_Click;
            this.Controls.Add(btnSaveConfig);

            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Text = "Form1";
        }

        #endregion
    }
}
