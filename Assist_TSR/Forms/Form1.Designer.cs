using System.Diagnostics;
using System;

namespace Assist_TSR.Forms
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>

        protected override void Dispose(bool disposing)
        {
            if (disposing && !this.IsDisposed)  // Check if already disposed
            {
                try
                {
                    // 1. Clean up system tray icon
                    notifyIcon?.Dispose();
                    notifyIcon = null;  // Prevent double-dispose

                    // 2. Signal thread to stop
                    isRunning = false;

                    // 3. Graceful thread shutdown with timeout
                    if (launchServerThread != null && launchServerThread.IsAlive)
                    {
                        if (!launchServerThread.Join(500))  // Wait max 500ms
                        {
                            try { launchServerThread.Interrupt(); }  // Force if needed
                            catch { /* Ignore thread state exceptions */ }
                        }
                        launchServerThread = null;  // Clear reference
                    }

                    // 4. Dispose other managed resources here if needed
                    // (e.g., logUpdateTimer?.Dispose())
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Dispose error: {ex.Message}");
                }
            }

            // Always call base disposal
            base.Dispose(disposing);
        }

        /*protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }*/

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.notifyIcon1 = new System.Windows.Forms.NotifyIcon(this.components);
            this.panel1 = new System.Windows.Forms.Panel();
            this.notificationPanel = new System.Windows.Forms.Panel();
            this.closeNotificationButton = new System.Windows.Forms.Button();
            this.notificationLabel = new System.Windows.Forms.Label();
            this.app_name = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.general_tab = new System.Windows.Forms.TabPage();
            this.flowLayoutPanel3 = new System.Windows.Forms.FlowLayoutPanel();
            this.settings = new System.Windows.Forms.Label();
            this.panel4 = new System.Windows.Forms.Panel();
            this.panel5 = new System.Windows.Forms.Panel();
            this.panel3 = new System.Windows.Forms.Panel();
            this.preset_btn = new System.Windows.Forms.Button();
            this.red = new System.Windows.Forms.PictureBox();
            this.green = new System.Windows.Forms.PictureBox();
            this.stop = new System.Windows.Forms.Button();
            this.start = new System.Windows.Forms.Button();
            this.save = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.node_name_set = new System.Windows.Forms.Label();
            this.flowLayoutPanel2 = new System.Windows.Forms.FlowLayoutPanel();
            this.node_info = new System.Windows.Forms.Label();
            this.node_value = new System.Windows.Forms.Label();
            this.console_value = new System.Windows.Forms.Label();
            this.con_stat_val = new System.Windows.Forms.Label();
            this.config_preset = new System.Windows.Forms.Label();
            this.preset_value = new System.Windows.Forms.Label();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.label1 = new System.Windows.Forms.Label();
            this.line = new System.Windows.Forms.Panel();
            this.config_tab = new System.Windows.Forms.TabPage();
            this.trigger_app = new System.Windows.Forms.Label();
            this.listView2 = new System.Windows.Forms.ListView();
            this.btnSave = new System.Windows.Forms.Button();
            this.update = new System.Windows.Forms.Button();
            this.btnReset = new System.Windows.Forms.Button();
            this.applications = new System.Windows.Forms.Label();
            this.nodes = new System.Windows.Forms.Label();
            this.listView1 = new System.Windows.Forms.ListView();
            this.treeView1 = new System.Windows.Forms.TreeView();
            this.panel7 = new System.Windows.Forms.Panel();
            this.configured_preset = new System.Windows.Forms.Label();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.triggering_node = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.Available_Nodes = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panel6 = new System.Windows.Forms.Panel();
            this.cust_presets = new System.Windows.Forms.Label();
            this.status_tab = new System.Windows.Forms.TabPage();
            this.treeView2 = new System.Windows.Forms.TreeView();
            this.panel8 = new System.Windows.Forms.Panel();
            this.app_status = new System.Windows.Forms.Label();
            this.service_status = new System.Windows.Forms.Label();
            this.s_status = new System.Windows.Forms.Label();
            this.net_status = new System.Windows.Forms.Label();
            this.n_status = new System.Windows.Forms.Label();
            this.con_status = new System.Windows.Forms.Label();
            this.c_status = new System.Windows.Forms.Label();
            this.log_tab = new System.Windows.Forms.TabPage();
            this.logTextBox = new System.Windows.Forms.RichTextBox();
            this.flowLayoutPanel4 = new System.Windows.Forms.FlowLayoutPanel();
            this.logs = new System.Windows.Forms.Label();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.logUpdateTimer = new System.Windows.Forms.Timer(this.components);
            this.panel1.SuspendLayout();
            this.notificationPanel.SuspendLayout();
            this.panel2.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.general_tab.SuspendLayout();
            this.flowLayoutPanel3.SuspendLayout();
            this.panel4.SuspendLayout();
            this.panel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.red)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.green)).BeginInit();
            this.flowLayoutPanel2.SuspendLayout();
            this.flowLayoutPanel1.SuspendLayout();
            this.config_tab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.status_tab.SuspendLayout();
            this.log_tab.SuspendLayout();
            this.flowLayoutPanel4.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.notificationPanel);
            this.panel1.Controls.Add(this.app_name);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(5, 5);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(415, 29);
            this.panel1.TabIndex = 0;
            this.panel1.Paint += new System.Windows.Forms.PaintEventHandler(this.panel1_Paint);
            // 
            // notificationPanel
            // 
            this.notificationPanel.BackColor = System.Drawing.SystemColors.Info;
            this.notificationPanel.Controls.Add(this.closeNotificationButton);
            this.notificationPanel.Controls.Add(this.notificationLabel);
            this.notificationPanel.Location = new System.Drawing.Point(150, 9);
            this.notificationPanel.Name = "notificationPanel";
            this.notificationPanel.Size = new System.Drawing.Size(252, 15);
            this.notificationPanel.TabIndex = 10;
            this.notificationPanel.Visible = false;
            // 
            // closeNotificationButton
            // 
            this.closeNotificationButton.BackColor = System.Drawing.SystemColors.Info;
            this.closeNotificationButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.closeNotificationButton.Location = new System.Drawing.Point(237, -3);
            this.closeNotificationButton.Name = "closeNotificationButton";
            this.closeNotificationButton.Size = new System.Drawing.Size(15, 23);
            this.closeNotificationButton.TabIndex = 1;
            this.closeNotificationButton.Text = "x";
            this.closeNotificationButton.UseVisualStyleBackColor = false;
            this.closeNotificationButton.Click += new System.EventHandler(this.closeNotificationButton_Click_1);
            // 
            // notificationLabel
            // 
            this.notificationLabel.AutoSize = true;
            this.notificationLabel.Location = new System.Drawing.Point(3, 0);
            this.notificationLabel.Name = "notificationLabel";
            this.notificationLabel.Size = new System.Drawing.Size(30, 13);
            this.notificationLabel.TabIndex = 0;
            this.notificationLabel.Text = "asda";
            // 
            // app_name
            // 
            this.app_name.AutoSize = true;
            this.app_name.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.app_name.Location = new System.Drawing.Point(12, 9);
            this.app_name.Name = "app_name";
            this.app_name.Size = new System.Drawing.Size(123, 15);
            this.app_name.TabIndex = 1;
            this.app_name.Text = "ASSIST CONSOLE";
            this.app_name.Click += new System.EventHandler(this.label1_Click);
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.tabControl1);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(5, 34);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(415, 429);
            this.panel2.TabIndex = 1;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.general_tab);
            this.tabControl1.Controls.Add(this.config_tab);
            this.tabControl1.Controls.Add(this.status_tab);
            this.tabControl1.Controls.Add(this.log_tab);
            this.tabControl1.Location = new System.Drawing.Point(6, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(404, 414);
            this.tabControl1.TabIndex = 0;
            // 
            // general_tab
            // 
            this.general_tab.Controls.Add(this.flowLayoutPanel3);
            this.general_tab.Controls.Add(this.panel3);
            this.general_tab.Controls.Add(this.flowLayoutPanel1);
            this.general_tab.Location = new System.Drawing.Point(4, 22);
            this.general_tab.Name = "general_tab";
            this.general_tab.Padding = new System.Windows.Forms.Padding(3);
            this.general_tab.Size = new System.Drawing.Size(396, 388);
            this.general_tab.TabIndex = 0;
            this.general_tab.Text = "General";
            this.general_tab.UseVisualStyleBackColor = true;
            // 
            // flowLayoutPanel3
            // 
            this.flowLayoutPanel3.Controls.Add(this.settings);
            this.flowLayoutPanel3.Controls.Add(this.panel4);
            this.flowLayoutPanel3.Location = new System.Drawing.Point(0, 223);
            this.flowLayoutPanel3.Name = "flowLayoutPanel3";
            this.flowLayoutPanel3.Size = new System.Drawing.Size(396, 33);
            this.flowLayoutPanel3.TabIndex = 1;
            this.flowLayoutPanel3.Paint += new System.Windows.Forms.PaintEventHandler(this.flowLayoutPanel3_Paint);
            // 
            // settings
            // 
            this.settings.AutoSize = true;
            this.settings.Dock = System.Windows.Forms.DockStyle.Top;
            this.settings.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.settings.Location = new System.Drawing.Point(13, 8);
            this.settings.Margin = new System.Windows.Forms.Padding(13, 8, 3, 0);
            this.settings.Name = "settings";
            this.settings.Size = new System.Drawing.Size(51, 15);
            this.settings.TabIndex = 0;
            this.settings.Text = "Settings";
            // 
            // panel4
            // 
            this.panel4.BackColor = System.Drawing.Color.LightGray;
            this.panel4.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel4.Controls.Add(this.panel5);
            this.panel4.Location = new System.Drawing.Point(77, 16);
            this.panel4.Margin = new System.Windows.Forms.Padding(10, 16, 3, 3);
            this.panel4.Name = "panel4";
            this.panel4.Size = new System.Drawing.Size(298, 1);
            this.panel4.TabIndex = 2;
            this.panel4.Paint += new System.Windows.Forms.PaintEventHandler(this.panel4_Paint);
            // 
            // panel5
            // 
            this.panel5.BackColor = System.Drawing.Color.LightGray;
            this.panel5.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel5.Location = new System.Drawing.Point(10, 83);
            this.panel5.Margin = new System.Windows.Forms.Padding(10, 16, 3, 3);
            this.panel5.Name = "panel5";
            this.panel5.Size = new System.Drawing.Size(298, 1);
            this.panel5.TabIndex = 3;
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.preset_btn);
            this.panel3.Controls.Add(this.red);
            this.panel3.Controls.Add(this.green);
            this.panel3.Controls.Add(this.stop);
            this.panel3.Controls.Add(this.start);
            this.panel3.Controls.Add(this.save);
            this.panel3.Controls.Add(this.textBox1);
            this.panel3.Controls.Add(this.node_name_set);
            this.panel3.Controls.Add(this.flowLayoutPanel2);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel3.Location = new System.Drawing.Point(3, 47);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(390, 338);
            this.panel3.TabIndex = 1;
            this.panel3.Paint += new System.Windows.Forms.PaintEventHandler(this.panel3_Paint);
            // 
            // preset_btn
            // 
            this.preset_btn.Location = new System.Drawing.Point(211, 297);
            this.preset_btn.Name = "preset_btn";
            this.preset_btn.Size = new System.Drawing.Size(91, 23);
            this.preset_btn.TabIndex = 10;
            this.preset_btn.Text = "Choose Preset";
            this.preset_btn.UseVisualStyleBackColor = true;
            this.preset_btn.Click += new System.EventHandler(this.preset_btn_Click);
            // 
            // red
            // 
            this.red.Image = global::Assist_TSR.Properties.Resources.red;
            this.red.Location = new System.Drawing.Point(314, 24);
            this.red.Name = "red";
            this.red.Size = new System.Drawing.Size(46, 50);
            this.red.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.red.TabIndex = 9;
            this.red.TabStop = false;
            this.red.Visible = false;
            // 
            // green
            // 
            this.green.BackColor = System.Drawing.Color.Transparent;
            this.green.Image = global::Assist_TSR.Properties.Resources.green;
            this.green.Location = new System.Drawing.Point(211, 24);
            this.green.Name = "green";
            this.green.Size = new System.Drawing.Size(54, 46);
            this.green.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.green.TabIndex = 8;
            this.green.TabStop = false;
            this.green.Visible = false;
            // 
            // stop
            // 
            this.stop.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.stop.Location = new System.Drawing.Point(300, 86);
            this.stop.Name = "stop";
            this.stop.Size = new System.Drawing.Size(75, 23);
            this.stop.TabIndex = 7;
            this.stop.Text = "Stop";
            this.stop.UseVisualStyleBackColor = true;
            this.stop.Click += new System.EventHandler(this.stop_Click);
            // 
            // start
            // 
            this.start.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.start.Location = new System.Drawing.Point(202, 86);
            this.start.Name = "start";
            this.start.Size = new System.Drawing.Size(75, 23);
            this.start.TabIndex = 6;
            this.start.Text = "Start";
            this.start.UseVisualStyleBackColor = true;
            this.start.Click += new System.EventHandler(this.start_Click);
            // 
            // save
            // 
            this.save.Location = new System.Drawing.Point(314, 297);
            this.save.Name = "save";
            this.save.Size = new System.Drawing.Size(75, 23);
            this.save.TabIndex = 5;
            this.save.Text = "Save";
            this.save.UseVisualStyleBackColor = true;
            this.save.Click += new System.EventHandler(this.save_Click);
            // 
            // textBox1
            // 
            this.textBox1.BackColor = System.Drawing.SystemColors.Info;
            this.textBox1.Location = new System.Drawing.Point(103, 229);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(129, 20);
            this.textBox1.TabIndex = 2;
            this.textBox1.TextChanged += new System.EventHandler(this.textBox1_TextChanged);
            // 
            // node_name_set
            // 
            this.node_name_set.AutoSize = true;
            this.node_name_set.Location = new System.Drawing.Point(25, 232);
            this.node_name_set.Name = "node_name_set";
            this.node_name_set.Size = new System.Drawing.Size(60, 13);
            this.node_name_set.TabIndex = 1;
            this.node_name_set.Text = "Set Name: ";
            this.node_name_set.Click += new System.EventHandler(this.label2_Click);
            // 
            // flowLayoutPanel2
            // 
            this.flowLayoutPanel2.Controls.Add(this.node_info);
            this.flowLayoutPanel2.Controls.Add(this.node_value);
            this.flowLayoutPanel2.Controls.Add(this.console_value);
            this.flowLayoutPanel2.Controls.Add(this.con_stat_val);
            this.flowLayoutPanel2.Controls.Add(this.config_preset);
            this.flowLayoutPanel2.Controls.Add(this.preset_value);
            this.flowLayoutPanel2.Location = new System.Drawing.Point(-3, 0);
            this.flowLayoutPanel2.Name = "flowLayoutPanel2";
            this.flowLayoutPanel2.Size = new System.Drawing.Size(167, 179);
            this.flowLayoutPanel2.TabIndex = 0;
            // 
            // node_info
            // 
            this.node_info.AutoSize = true;
            this.node_info.Location = new System.Drawing.Point(15, 20);
            this.node_info.Margin = new System.Windows.Forms.Padding(15, 20, 3, 0);
            this.node_info.Name = "node_info";
            this.node_info.Size = new System.Drawing.Size(70, 13);
            this.node_info.TabIndex = 1;
            this.node_info.Text = "Node Name: ";
            // 
            // node_value
            // 
            this.node_value.AutoSize = true;
            this.node_value.Location = new System.Drawing.Point(103, 20);
            this.node_value.Margin = new System.Windows.Forms.Padding(15, 20, 3, 0);
            this.node_value.Name = "node_value";
            this.node_value.Size = new System.Drawing.Size(40, 13);
            this.node_value.TabIndex = 2;
            this.node_value.Text = "Value: ";
            // 
            // console_value
            // 
            this.console_value.AutoSize = true;
            this.console_value.Location = new System.Drawing.Point(15, 53);
            this.console_value.Margin = new System.Windows.Forms.Padding(15, 20, 3, 0);
            this.console_value.Name = "console_value";
            this.console_value.Size = new System.Drawing.Size(84, 13);
            this.console_value.TabIndex = 5;
            this.console_value.Text = "Console Status: ";
            // 
            // con_stat_val
            // 
            this.con_stat_val.AutoSize = true;
            this.con_stat_val.Location = new System.Drawing.Point(103, 53);
            this.con_stat_val.Margin = new System.Windows.Forms.Padding(1, 20, 3, 0);
            this.con_stat_val.Name = "con_stat_val";
            this.con_stat_val.Size = new System.Drawing.Size(40, 13);
            this.con_stat_val.TabIndex = 6;
            this.con_stat_val.Text = "Value: ";
            // 
            // config_preset
            // 
            this.config_preset.AutoSize = true;
            this.config_preset.Location = new System.Drawing.Point(15, 86);
            this.config_preset.Margin = new System.Windows.Forms.Padding(15, 20, 3, 0);
            this.config_preset.Name = "config_preset";
            this.config_preset.Size = new System.Drawing.Size(76, 13);
            this.config_preset.TabIndex = 7;
            this.config_preset.Text = "Active Preset: ";
            // 
            // preset_value
            // 
            this.preset_value.AutoSize = true;
            this.preset_value.Location = new System.Drawing.Point(102, 86);
            this.preset_value.Margin = new System.Windows.Forms.Padding(8, 20, 3, 0);
            this.preset_value.Name = "preset_value";
            this.preset_value.Size = new System.Drawing.Size(40, 13);
            this.preset_value.TabIndex = 8;
            this.preset_value.Text = "Value: ";
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.label1);
            this.flowLayoutPanel1.Controls.Add(this.line);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(3, 3);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(390, 44);
            this.flowLayoutPanel1.TabIndex = 0;
            this.flowLayoutPanel1.Paint += new System.Windows.Forms.PaintEventHandler(this.flowLayoutPanel1_Paint);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(10, 20);
            this.label1.Margin = new System.Windows.Forms.Padding(10, 20, 3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(75, 15);
            this.label1.TabIndex = 0;
            this.label1.Text = "Console Info";
            // 
            // line
            // 
            this.line.BackColor = System.Drawing.Color.LightGray;
            this.line.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.line.Location = new System.Drawing.Point(3, 63);
            this.line.Margin = new System.Windows.Forms.Padding(3, 28, 3, 3);
            this.line.Name = "line";
            this.line.Size = new System.Drawing.Size(298, 1);
            this.line.TabIndex = 1;
            // 
            // config_tab
            // 
            this.config_tab.Controls.Add(this.trigger_app);
            this.config_tab.Controls.Add(this.listView2);
            this.config_tab.Controls.Add(this.btnSave);
            this.config_tab.Controls.Add(this.update);
            this.config_tab.Controls.Add(this.btnReset);
            this.config_tab.Controls.Add(this.applications);
            this.config_tab.Controls.Add(this.nodes);
            this.config_tab.Controls.Add(this.listView1);
            this.config_tab.Controls.Add(this.treeView1);
            this.config_tab.Controls.Add(this.panel7);
            this.config_tab.Controls.Add(this.configured_preset);
            this.config_tab.Controls.Add(this.dataGridView1);
            this.config_tab.Controls.Add(this.panel6);
            this.config_tab.Controls.Add(this.cust_presets);
            this.config_tab.Location = new System.Drawing.Point(4, 22);
            this.config_tab.Name = "config_tab";
            this.config_tab.Padding = new System.Windows.Forms.Padding(3);
            this.config_tab.Size = new System.Drawing.Size(396, 388);
            this.config_tab.TabIndex = 1;
            this.config_tab.Text = "Configurations";
            this.config_tab.UseVisualStyleBackColor = true;
            // 
            // trigger_app
            // 
            this.trigger_app.AutoSize = true;
            this.trigger_app.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.trigger_app.Location = new System.Drawing.Point(300, 206);
            this.trigger_app.Name = "trigger_app";
            this.trigger_app.Size = new System.Drawing.Size(70, 15);
            this.trigger_app.TabIndex = 13;
            this.trigger_app.Text = "Trigger App";
            // 
            // listView2
            // 
            this.listView2.HideSelection = false;
            this.listView2.Location = new System.Drawing.Point(298, 231);
            this.listView2.Name = "listView2";
            this.listView2.Size = new System.Drawing.Size(75, 97);
            this.listView2.TabIndex = 12;
            this.listView2.UseCompatibleStateImageBehavior = false;
            // 
            // btnSave
            // 
            this.btnSave.BackColor = System.Drawing.Color.Green;
            this.btnSave.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSave.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.btnSave.Location = new System.Drawing.Point(307, 350);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(75, 23);
            this.btnSave.TabIndex = 11;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = false;
            // 
            // update
            // 
            this.update.BackColor = System.Drawing.Color.Yellow;
            this.update.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.update.Location = new System.Drawing.Point(217, 350);
            this.update.Name = "update";
            this.update.Size = new System.Drawing.Size(75, 23);
            this.update.TabIndex = 10;
            this.update.Text = "Update";
            this.update.UseVisualStyleBackColor = false;
            this.update.Click += new System.EventHandler(this.update_Click);
            // 
            // btnReset
            // 
            this.btnReset.BackColor = System.Drawing.Color.Red;
            this.btnReset.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnReset.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.btnReset.Location = new System.Drawing.Point(19, 350);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(75, 23);
            this.btnReset.TabIndex = 9;
            this.btnReset.Text = "Reset";
            this.btnReset.UseVisualStyleBackColor = false;
            // 
            // applications
            // 
            this.applications.AutoSize = true;
            this.applications.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.applications.Location = new System.Drawing.Point(186, 206);
            this.applications.Name = "applications";
            this.applications.Size = new System.Drawing.Size(73, 15);
            this.applications.TabIndex = 8;
            this.applications.Text = "Applications";
            // 
            // nodes
            // 
            this.nodes.AutoSize = true;
            this.nodes.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.nodes.Location = new System.Drawing.Point(51, 206);
            this.nodes.Name = "nodes";
            this.nodes.Size = new System.Drawing.Size(43, 15);
            this.nodes.TabIndex = 7;
            this.nodes.Text = "Nodes";
            // 
            // listView1
            // 
            this.listView1.HideSelection = false;
            this.listView1.Location = new System.Drawing.Point(162, 231);
            this.listView1.Name = "listView1";
            this.listView1.Size = new System.Drawing.Size(121, 97);
            this.listView1.TabIndex = 5;
            this.listView1.UseCompatibleStateImageBehavior = false;
            // 
            // treeView1
            // 
            this.treeView1.Location = new System.Drawing.Point(19, 231);
            this.treeView1.Name = "treeView1";
            this.treeView1.Size = new System.Drawing.Size(121, 97);
            this.treeView1.TabIndex = 4;
            // 
            // panel7
            // 
            this.panel7.BackColor = System.Drawing.Color.Black;
            this.panel7.Location = new System.Drawing.Point(129, 182);
            this.panel7.Name = "panel7";
            this.panel7.Size = new System.Drawing.Size(253, 1);
            this.panel7.TabIndex = 2;
            // 
            // configured_preset
            // 
            this.configured_preset.AutoSize = true;
            this.configured_preset.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.configured_preset.Location = new System.Drawing.Point(6, 174);
            this.configured_preset.Name = "configured_preset";
            this.configured_preset.Size = new System.Drawing.Size(105, 15);
            this.configured_preset.TabIndex = 3;
            this.configured_preset.Text = "Configured Preset";
            // 
            // dataGridView1
            // 
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.triggering_node,
            this.Available_Nodes});
            this.dataGridView1.Location = new System.Drawing.Point(39, 58);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.Size = new System.Drawing.Size(324, 86);
            this.dataGridView1.TabIndex = 2;
            this.dataGridView1.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellContentClick);
            // 
            // triggering_node
            // 
            this.triggering_node.HeaderText = "Triggering Node";
            this.triggering_node.Name = "triggering_node";
            // 
            // Available_Nodes
            // 
            this.Available_Nodes.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Available_Nodes.HeaderText = "Available Nodes";
            this.Available_Nodes.Name = "Available_Nodes";
            // 
            // panel6
            // 
            this.panel6.BackColor = System.Drawing.Color.Black;
            this.panel6.Location = new System.Drawing.Point(129, 30);
            this.panel6.Name = "panel6";
            this.panel6.Size = new System.Drawing.Size(253, 1);
            this.panel6.TabIndex = 1;
            // 
            // cust_presets
            // 
            this.cust_presets.AutoSize = true;
            this.cust_presets.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cust_presets.Location = new System.Drawing.Point(6, 20);
            this.cust_presets.Name = "cust_presets";
            this.cust_presets.Size = new System.Drawing.Size(113, 15);
            this.cust_presets.TabIndex = 0;
            this.cust_presets.Text = "Customizing Preset";
            // 
            // status_tab
            // 
            this.status_tab.Controls.Add(this.treeView2);
            this.status_tab.Controls.Add(this.panel8);
            this.status_tab.Controls.Add(this.app_status);
            this.status_tab.Controls.Add(this.service_status);
            this.status_tab.Controls.Add(this.s_status);
            this.status_tab.Controls.Add(this.net_status);
            this.status_tab.Controls.Add(this.n_status);
            this.status_tab.Controls.Add(this.con_status);
            this.status_tab.Controls.Add(this.c_status);
            this.status_tab.Location = new System.Drawing.Point(4, 22);
            this.status_tab.Name = "status_tab";
            this.status_tab.Size = new System.Drawing.Size(396, 388);
            this.status_tab.TabIndex = 2;
            this.status_tab.Text = "Status";
            this.status_tab.UseVisualStyleBackColor = true;
            // 
            // treeView2
            // 
            this.treeView2.Location = new System.Drawing.Point(15, 160);
            this.treeView2.Name = "treeView2";
            this.treeView2.Size = new System.Drawing.Size(367, 194);
            this.treeView2.TabIndex = 8;
            // 
            // panel8
            // 
            this.panel8.BackColor = System.Drawing.Color.Black;
            this.panel8.Location = new System.Drawing.Point(110, 138);
            this.panel8.Name = "panel8";
            this.panel8.Size = new System.Drawing.Size(272, 1);
            this.panel8.TabIndex = 7;
            // 
            // app_status
            // 
            this.app_status.AutoSize = true;
            this.app_status.Location = new System.Drawing.Point(12, 131);
            this.app_status.Name = "app_status";
            this.app_status.Size = new System.Drawing.Size(92, 13);
            this.app_status.TabIndex = 6;
            this.app_status.Text = "Application Status";
            // 
            // service_status
            // 
            this.service_status.AutoSize = true;
            this.service_status.Location = new System.Drawing.Point(112, 92);
            this.service_status.Name = "service_status";
            this.service_status.Size = new System.Drawing.Size(10, 13);
            this.service_status.TabIndex = 5;
            this.service_status.Text = "-";
            // 
            // s_status
            // 
            this.s_status.AutoSize = true;
            this.s_status.Location = new System.Drawing.Point(12, 92);
            this.s_status.Name = "s_status";
            this.s_status.Size = new System.Drawing.Size(82, 13);
            this.s_status.TabIndex = 4;
            this.s_status.Text = "Service Status: ";
            // 
            // net_status
            // 
            this.net_status.AutoSize = true;
            this.net_status.Location = new System.Drawing.Point(112, 61);
            this.net_status.Name = "net_status";
            this.net_status.Size = new System.Drawing.Size(35, 13);
            this.net_status.TabIndex = 3;
            this.net_status.Text = "label3";
            // 
            // n_status
            // 
            this.n_status.AutoSize = true;
            this.n_status.Location = new System.Drawing.Point(12, 61);
            this.n_status.Name = "n_status";
            this.n_status.Size = new System.Drawing.Size(86, 13);
            this.n_status.TabIndex = 2;
            this.n_status.Text = "Network Status: ";
            // 
            // con_status
            // 
            this.con_status.AutoSize = true;
            this.con_status.Location = new System.Drawing.Point(112, 34);
            this.con_status.Name = "con_status";
            this.con_status.Size = new System.Drawing.Size(57, 13);
            this.con_status.TabIndex = 1;
            this.con_status.Text = "Not Active";
            // 
            // c_status
            // 
            this.c_status.AutoSize = true;
            this.c_status.Location = new System.Drawing.Point(12, 34);
            this.c_status.Name = "c_status";
            this.c_status.Size = new System.Drawing.Size(84, 13);
            this.c_status.TabIndex = 0;
            this.c_status.Text = "Console Status: ";
            // 
            // log_tab
            // 
            this.log_tab.Controls.Add(this.logTextBox);
            this.log_tab.Controls.Add(this.flowLayoutPanel4);
            this.log_tab.Location = new System.Drawing.Point(4, 22);
            this.log_tab.Name = "log_tab";
            this.log_tab.Size = new System.Drawing.Size(396, 388);
            this.log_tab.TabIndex = 3;
            this.log_tab.Text = "Logs";
            this.log_tab.UseVisualStyleBackColor = true;
            // 
            // logTextBox
            // 
            this.logTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logTextBox.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.logTextBox.Location = new System.Drawing.Point(0, 31);
            this.logTextBox.Name = "logTextBox";
            this.logTextBox.ReadOnly = true;
            this.logTextBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.logTextBox.Size = new System.Drawing.Size(396, 357);
            this.logTextBox.TabIndex = 2;
            this.logTextBox.Text = "";
            // 
            // flowLayoutPanel4
            // 
            this.flowLayoutPanel4.Controls.Add(this.logs);
            this.flowLayoutPanel4.Controls.Add(this.btnRefresh);
            this.flowLayoutPanel4.Dock = System.Windows.Forms.DockStyle.Top;
            this.flowLayoutPanel4.Location = new System.Drawing.Point(0, 0);
            this.flowLayoutPanel4.Name = "flowLayoutPanel4";
            this.flowLayoutPanel4.Size = new System.Drawing.Size(396, 31);
            this.flowLayoutPanel4.TabIndex = 1;
            // 
            // logs
            // 
            this.logs.AutoSize = true;
            this.logs.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.logs.Location = new System.Drawing.Point(15, 10);
            this.logs.Margin = new System.Windows.Forms.Padding(15, 10, 3, 0);
            this.logs.Name = "logs";
            this.logs.Size = new System.Drawing.Size(41, 16);
            this.logs.TabIndex = 0;
            this.logs.Text = "Logs";
            // 
            // btnRefresh
            // 
            this.btnRefresh.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.btnRefresh.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnRefresh.Location = new System.Drawing.Point(309, 3);
            this.btnRefresh.Margin = new System.Windows.Forms.Padding(250, 3, 3, 3);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.btnRefresh.Size = new System.Drawing.Size(75, 23);
            this.btnRefresh.TabIndex = 3;
            this.btnRefresh.Text = "Refresh";
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new System.EventHandler(this.button1_Click);
            // 
            // logUpdateTimer
            // 
            this.logUpdateTimer.Tick += new System.EventHandler(this.logUpdateTimer_Tick_1);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ControlLight;
            this.ClientSize = new System.Drawing.Size(425, 468);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "Form1";
            this.Padding = new System.Windows.Forms.Padding(5);
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.notificationPanel.ResumeLayout(false);
            this.notificationPanel.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.general_tab.ResumeLayout(false);
            this.flowLayoutPanel3.ResumeLayout(false);
            this.flowLayoutPanel3.PerformLayout();
            this.panel4.ResumeLayout(false);
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.red)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.green)).EndInit();
            this.flowLayoutPanel2.ResumeLayout(false);
            this.flowLayoutPanel2.PerformLayout();
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.config_tab.ResumeLayout(false);
            this.config_tab.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.status_tab.ResumeLayout(false);
            this.status_tab.PerformLayout();
            this.log_tab.ResumeLayout(false);
            this.flowLayoutPanel4.ResumeLayout(false);
            this.flowLayoutPanel4.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.NotifyIcon notifyIcon1;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label app_name;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage general_tab;
        private System.Windows.Forms.TabPage config_tab;
        private System.Windows.Forms.TabPage status_tab;
        private System.Windows.Forms.TabPage log_tab;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Panel line;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel2;
        private System.Windows.Forms.Label node_info;
        private System.Windows.Forms.Label node_value;
        private System.Windows.Forms.Label console_value;
        private System.Windows.Forms.Label con_stat_val;
        private System.Windows.Forms.Label config_preset;
        private System.Windows.Forms.Label preset_value;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel3;
        private System.Windows.Forms.Label settings;
        private System.Windows.Forms.Panel panel4;
        private System.Windows.Forms.Label node_name_set;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Button save;
        private System.Windows.Forms.Button stop;
        private System.Windows.Forms.Button start;
        private System.Windows.Forms.PictureBox red;
        private System.Windows.Forms.PictureBox green;
        private System.Windows.Forms.Label cust_presets;
        private System.Windows.Forms.Panel panel5;
        private System.Windows.Forms.Panel panel6;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Label configured_preset;
        private System.Windows.Forms.Panel panel7;
        private System.Windows.Forms.Label nodes;
        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.TreeView treeView1;
        private System.Windows.Forms.Label applications;
        private System.Windows.Forms.Button update;
        private System.Windows.Forms.Button btnReset;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.DataGridViewCheckBoxColumn triggering_node;
        private System.Windows.Forms.DataGridViewTextBoxColumn Available_Nodes;
        private System.Windows.Forms.Label trigger_app;
        private System.Windows.Forms.ListView listView2;
        private System.Windows.Forms.RichTextBox logTextBox;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel4;
        private System.Windows.Forms.Label logs;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Timer logUpdateTimer;
        private System.Windows.Forms.Label con_status;
        private System.Windows.Forms.Label c_status;
        private System.Windows.Forms.Label n_status;
        private System.Windows.Forms.Label net_status;
        private System.Windows.Forms.Label s_status;
        private System.Windows.Forms.Label service_status;
        private System.Windows.Forms.Panel panel8;
        private System.Windows.Forms.Label app_status;
        private System.Windows.Forms.TreeView treeView2;
        private System.Windows.Forms.Panel notificationPanel;
        private System.Windows.Forms.Button closeNotificationButton;
        private System.Windows.Forms.Label notificationLabel;
        private System.Windows.Forms.Button preset_btn;
    }
}

