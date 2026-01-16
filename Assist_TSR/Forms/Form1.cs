using System;
using System.ServiceProcess;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Timers;
using Newtonsoft.Json;
using System.Text.Json;
using System.Windows.Forms.VisualStyles;
using System.Linq;
//using System.Data;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Runtime.InteropServices;
using System.Net.NetworkInformation;
using System.Reflection;
using static System.Windows.Forms.LinkLabel;
using Assist_TSR.IPC_Handler;
using Assist_TSR.Utilities;
using Assist_TSR.Event_Handler;
using Assist_TSR.Classes;
using Assist_Service.IPC_Handler;
using Assist_Service.Helpers;
using Logging = Assist_TSR.Utilities.Logging;
using FileHelper = Assist_TSR.Classes.FileHelper;



namespace Assist_TSR.Forms
{

    public partial class Form1 : Form
    {

        private Thread launchServerThread; // Remove nullable annotation
        private Thread closureServerThread;
        public bool isRunning { get; private set; } = false;
        private NotifyIcon notifyIcon; // Manually declare NotifyIcon
        private System.Windows.Forms.Timer refreshTimer, statusTimer;
        private string _selectedSourceNode = null;
        private NodeConfig _nodeConfig;
        private const string PipeName = "CustomRulesConfigPipe";
        Dictionary<string, (bool isSourceNode, List<string> applications)> nodeApplications = new Dictionary<string, (bool, List<string>)>();
        // Log Files 
        private readonly string logFilePath;
        private long lastFilePosition = 0;
        private readonly object fileLock = new object();
        string serviceMissingLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TSR_Fatel_Crash.log");

        // Config_App_Status_IPC
        private List<NodeConfig> _config = new List<NodeConfig>();
        private List<AppStatus> _statuses = new List<AppStatus>();
        private readonly object _statusLock = new object();
        private Listen_App_Status listen_app_status;

        // PANELS 
        private bool panel11SelectAll = false;



        // OBJECT DECLARATION
        Server my_server;
        Logging loger;
        TSR_Logging tsr_log;
        Request_Data node_name;
        Request_Data fetch_data;
        Get_File_Path config_path;
        Notifying_Service notifying_service;
        Send_Path send_custom_rule_path;
        Get_List_Rules get_list_rules;
        Display_Config_User display_config_user;
        Show show_message;
        Loading_Config loader;
        Get_Peers peers_helper;
        Timer_ timer_;
        Get_Rules_Path get_rules_path;
        Read_Rules read_rules;
        Rule_Class myRule;
        //Listen_App_Status _listenAppStatus;

        // Power Management
        private PeerSelectionManager selectionManager = new PeerSelectionManager();
        private System.Threading.Timer statusRefreshTimer;
        private readonly Power_Management powerManager;
        private readonly System.Windows.Forms.ToolTip toolTip;
        // To track toggle state manually
        private bool selectAllActive = false;

        public Form1()
        {
            try
            {
                Log("Constructor Start");



                // OBJECT INSTANCES
                my_server = new Server(this);
                loger = new Logging();
                tsr_log = new TSR_Logging();
                node_name = new Request_Data(this);
                fetch_data = new Request_Data(this);
                config_path = new Get_File_Path();
                notifying_service = new Notifying_Service();
                send_custom_rule_path = new Send_Path();
                get_list_rules = new Get_List_Rules();
                display_config_user = new Display_Config_User();
                show_message = new Show();
                loader = new Loading_Config();
                peers_helper = new Get_Peers();
                timer_ = new Timer_(UpdateDataGridViewWithPeers);
                get_rules_path = new Get_Rules_Path();
                read_rules = new Read_Rules();
                myRule = new Rule_Class();
                Log("Initialized all helper classes");

                InitializeComponent();
                Log("Called InitializeComponent");

                InitializeSystemTrayIcon();
                Log("Initialized system tray icon");

                try
                {
                    my_server.StartLaunchServer();
                    Log("Server launch started");
                }
                catch (Exception ex)
                {
                    Log("StartLaunchServer failed: " + ex);
                }

                try
                {
                    my_server.StartClosureServer();
                    Log("Closure Server launch started");
                }
                catch (Exception ex)
                {
                    Log("StartClosureServer failed: " + ex);
                }

                try
                {
                    timer_.InitializeTimer();
                    Log("Timer initialized");
                }
                catch (Exception ex)
                {
                    Log("Timer init failed: " + ex);
                }

                InitializeDataGridView();
                Log("DataGridView initialized");

                logFilePath = ResolveLogPath();
                Log("Resolved log file path");

                loader.LoadConfig();
                Log("Config loaded");

                listen_app_status = new Listen_App_Status(
                    _statuses,
                    new Action(() =>
                    {
                        try
                        {
                            if (IsDisposed || !IsHandleCreated) return;

                            if (InvokeRequired)
                                BeginInvoke(new Action(UpdateTreeView));
                            else
                                UpdateTreeView();
                        }
                        catch (ObjectDisposedException) { }
                        catch (InvalidOperationException) { }
                        }),
                        _statusLock
                );
                Log("Initialized listen_app_status");

                try
                {
                    listen_app_status.StartListeningToAppStatusPipe();
                    Log("Started listening to pipe");
                }
                catch (Exception ex)
                {
                    Log("Pipe listen failed: " + ex);
                }

                SetupTreeView();
                Log("Tree view set up");

                InitializeLogViewer();
                Log("Log viewer initialized");
                

                // Attach event handlers
                this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
                dataGridView1.CellClick += dataGridView1_CellClick;
                listView1.DoubleClick += listView1_DoubleClick;
                btnSave.Click += btnSave_Click;
                treeView1.AfterSelect += treeView1_AfterSelect;
                listView1.View = View.List;
                listView1.Scrollable = true;
                btnReset.Click += btnReset_Click;
                Log("Event handlers attached");


                // Power Management 
                powerManager = new Power_Management();
                powerManager.OnPeersUpdated += PowerManager_OnPeersUpdated;

                

                // Handle click event manually (instead of CheckedChanged)
                select_all.Click += select_all_Click;


                Log("Constructor End");
            }
            catch (Exception ex)
            {
                Log("Exception in Form1 constructor: " + ex.ToString());
            }
        }

        private void Log(string message)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assist_TSR.txt");
                File.AppendAllText(path, $"{DateTime.Now}: {message}{Environment.NewLine}");
            }
            catch
            {
                // Silent catch to avoid recursive logging exceptions
            }
        }

        // LINKED WITH REQUEST_DATA.CS FILE IN IPC_HANDLER FOLDER
        public void ShowNotificationSafe(string message)
        {
            try
            {
                if (IsDisposed || !IsHandleCreated) return;

                if (InvokeRequired)
                {
                    BeginInvoke((MethodInvoker)(() => ShowNotificationSafe(message)));
                    return;
                }

                ShowNotification(message);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        public void HideNotificationSafe()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(HideNotificationSafe));
                return;
            }

            notificationPanel.Visible = false; // or whatever UI you use
        }

        private void ShowNotification(string message)
        {
            notificationLabel.Text = message;
            notificationPanel.Visible = true;
        }

        // Notification Bar within UI (unchanged)


        private void closeNotificationButton_Click_1(object sender, EventArgs e)
        {
            notificationPanel.Visible = false;
        }

        // Updated to use the new async method directly


        // UPDATING_GENERAL_TAB
        private async void UpdateUIWithStatus()
        {
            try
            {
                string nodeName = await fetch_data.FetchDataAsync("GET_NODE_NAME");
                node_value.Text = nodeName;

                string activePreset = await fetch_data.FetchDataAsync("GET_ACTIVE_PRESET");
                preset_value.Text = activePreset;

                con_stat_val.Text = isRunning ? "Running" : "Stopped";
            }
            catch (Exception ex)
            {
                Log("UpdateUIWithStatus failed: " + ex);
            }
        }

        private void InitializeDataGridView()
        {
            // Check if the "Browse" column already exists to avoid duplicates
            if (dataGridView1.Columns["Browse"] == null)
            {
                // Add a new column for the Browse button
                DataGridViewButtonColumn browseButtonColumn = new DataGridViewButtonColumn();
                browseButtonColumn.HeaderText = "Configuring";
                browseButtonColumn.Text = "Browse";
                browseButtonColumn.Name = "Browse";
                browseButtonColumn.UseColumnTextForButtonValue = true;
                dataGridView1.Columns.Add(browseButtonColumn);
            }

            /*Attach the CellClick event handler (if not already attached)
            dataGridView1.CellClick -= dataGridView1_CellClick; // Remove existing handler to avoid duplication
            dataGridView1.CellClick += dataGridView1_CellClick;*/
        }

        private void InitializeSystemTrayIcon()
        {
            notifyIcon = new NotifyIcon(); // Manually initialize NotifyIcon
            notifyIcon.Icon = Properties.Resources.assist; // Ensure the icon is added to resources
            notifyIcon.Text = "Assist TSR";
            notifyIcon.Visible = true;
            notifyIcon.MouseDoubleClick += NotifyIcon_MouseDoubleClick;

            ContextMenuStrip contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Start", null, OnStart);
            contextMenu.Items.Add("Stop", null, OnStop);
            contextMenu.Items.Add("Exit", null, OnExit);

            notifyIcon.ContextMenuStrip = contextMenu;
        }
        private void OnStart(object sender, EventArgs e)
        {
            try
            {
                if (isRunning)
                    return;

                isRunning = true;

                // -------------------------------
                // 1️⃣ Start Launch Server IMMEDIATELY (DO NOT WAIT)
                // -------------------------------
                if (launchServerThread != null && launchServerThread.IsAlive)
                    launchServerThread.Join(500);

                launchServerThread = new Thread(my_server.StartLaunchServer)
                {
                    IsBackground = true
                };
                launchServerThread.Start();

                // -------------------------------
                // 2️⃣ Start Closure Server IMMEDIATELY
                // -------------------------------
                if (closureServerThread != null && closureServerThread.IsAlive)
                    closureServerThread.Join(500);

                closureServerThread = new Thread(my_server.StartClosureServer)
                {
                    IsBackground = true
                };
                closureServerThread.Start();

                // -------------------------------
                // 3️⃣ NON-BLOCKING service readiness check (background)
                // -------------------------------
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        using (ServiceController sc = new ServiceController("Assist_Service"))
                        {
                            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(60));
                        }
                    }
                    catch (Exception ex)
                    {
                        string logPath = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "TSR_service_missing.log");

                        File.AppendAllText(
                            logPath,
                            $"[{DateTime.Now}] Assist_Service not ready: {ex}\n");
                    }
                });

                // -------------------------------
                // 4️⃣ UI update (SAME AS OLD CODE)
                // -------------------------------
                if (this.InvokeRequired)
                {
                    this.BeginInvoke((MethodInvoker)UpdateUIStarted);
                }
                else
                {
                    UpdateUIStarted();
                }
            }
            catch (Exception ex)
            {
                string onStartLogPath =
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TSR_OnStart.log");

                File.AppendAllText(
                    onStartLogPath,
                    $"[{DateTime.Now}] Exception in OnStart: {ex}\n");
            }
        }


        private void OnStop(object sender, EventArgs e)
        {
            if (isRunning)
            {
                isRunning = false;
                // Don't block the UI thread - use async approach
                Task.Run(() =>
                {
                    if (launchServerThread != null && launchServerThread.IsAlive)
                    {
                        // Implement proper thread termination in StartLaunchServer
                        launchServerThread.Join(TimeSpan.FromSeconds(5)); // Timeout to prevent hanging
                    }

                    // Update UI through Invoke if needed
                    this.Invoke((MethodInvoker)delegate {
                        notifyIcon.ShowBalloonTip(1000, "Assist", "TSR stopped.", ToolTipIcon.Info);
                        con_stat_val.Text = "Stopped";
                        con_status.Text = "Stopped";
                        con_status.ForeColor = Color.Red;
                    });
                });
            }
        }

        private void OnExit(object sender, EventArgs e)
        {
            isRunning = false;

            // Request thread stop (implement this in your StartLaunchServer method)
            // Then dispose resources
            notifyIcon.Visible = false;
            notifyIcon.Dispose();

            // Proper application exit
            Environment.Exit(0); // Forceful exit if needed
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Hide();  // Hide the form on startup

            // ✅ Auto-start the server when form loads
            Task.Run(() => OnStart(null, EventArgs.Empty));
        }


        private void NotifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            LoginForm login = new LoginForm();
            if (login.ShowDialog() != DialogResult.OK)
            {
                // Exit if login cancelled or failed
                return;
            }
            this.Show();
            isRunning = true;
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            Activate();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true; // Cancel the close event
                this.Hide(); // Hide the form
                this.ShowInTaskbar = false; // Remove from the taskbar
                notifyIcon.ShowBalloonTip(1000, "Assist TSR", "The application is still running in the system tray.", ToolTipIcon.Info);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Initialize and start a timer to update the UI every 5 seconds
            statusTimer = new System.Windows.Forms.Timer();
            statusTimer.Interval = 5000; // 5 seconds
            statusTimer.Tick += (s, ev) => UpdateUIWithStatus();
            statusTimer.Start();

            UpdateServiceStatus();
            RefreshNetworkStatus();
            

        }

        private void start_Click(object sender, EventArgs e)
        {
            try
            {
                green.Visible = true;
                red.Visible = false;
                start.Enabled = false; // Disable button during operation

                //await Task.Run(() => OnStart(sender, e));
            }
            catch (Exception ex)
            {
                red.Visible = true;
                green.Visible = false;
                MessageBox.Show($"Failed to start: {ex.Message}", "Error",
                               MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                start.Enabled = true;
            }
        }

        private async void stop_Click(object sender, EventArgs e)
        {
            try
            {
                green.Visible = false;
                red.Visible = true;
                stop.Enabled = false; // Disable button during operation

                //await Task.Run(() => OnStop(sender, e));
            }
            catch (Exception ex)
            {
                // If stop failed, show error but keep UI in stopped state
                MessageBox.Show($"Failed to stop: {ex.Message}", "Error",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                stop.Enabled = true;
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        // Save Button Click Handler

        private void save_Click(object sender, EventArgs e)
        {
            try
            {
                string nodeName = textBox1.Text.Trim();

                if (string.IsNullOrEmpty(nodeName))
                {
                    MessageBox.Show("Please enter a valid node name.");
                    return;
                }

                // Get the appropriate config file path
                string configPath = config_path.GetConfigFilePath();

                // Create a new configuration object (this will replace any existing content)
                var config = new NodeConfig
                {
                    NodeName = nodeName  // This will be the only content in the file
                };

                // This will overwrite the entire file with the new configuration
                // Any previous content will be completely replaced
                FileHelper.WriteJsonWithRetry(configPath, config);

                // Notify the service about the change
                notifying_service.NotifyServiceAboutConfigChange(nodeName);

                MessageBox.Show("Configuration saved successfully!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving configuration: {ex.Message}");
            }
        }


        // Custom Preset Selection

        private async void preset_btn_Click(object sender, EventArgs e)
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                openFileDialog.Title = "Select Rules Configuration File";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        loger.Log($"Attempting to send configuration: {openFileDialog.FileName}");
                        await send_custom_rule_path.SendPathToService(openFileDialog.FileName);

                        loger.Log($"Successfully sent configuration: {openFileDialog.FileName}");
                        show_message.ShowMessage("Configuration sent to service",
                                  $"Successfully sent:\n{openFileDialog.FileName}");
                        //preset_value.Text = Path.GetFileName(openFileDialog.FileName);
                    }
                    catch (Exception ex)
                    {
                        string errorMsg = $"Failed to send configuration: {ex.Message}";
                        loger.Log(errorMsg);

                        show_message.ShowError("Configuration Error", errorMsg);
                    }
                }
            }
        }


        // Button click event for getting current config
        private async void btnGetConfig_Click(object sender, EventArgs e)
        {
            try
            {
                var config = await get_list_rules.GetConfigFromService();
                display_config_user.DisplayConfig(config);
            }
            catch (Exception ex)
            {
                show_message.ShowError("Configuration Error",
                        $"Failed to get configuration:\n{ex.Message}");
            }
        }

        private void SetupTreeView()
        {
            if (treeView2 == null)
            {
                Log("treeView2 is null in SetupTreeView");
                return;
            }
            treeView2.Nodes.Clear();

            if (_config == null)
            {
                Log("_config is null in SetupTreeView");
                return;
            }

            foreach (var config in _config)
            {
                foreach (var target in config.TargetNodes)
                {
                    TreeNode node = new TreeNode(target.NodeName);
                    TreeNode appsNode = new TreeNode("Configured Applications");

                    TreeNode appNode = new TreeNode($"{target.LaunchApp} : NOT CHECKED");
                    appsNode.Nodes.Add(appNode);
                    node.Nodes.Add(appsNode);

                    treeView2.Nodes.Add(node);
                }
            }

            treeView2.ExpandAll();
        }

        //.....................................

        private void UpdateTreeView()
        {
            foreach (TreeNode node in treeView2.Nodes)
            {
                string nodeName = node.Text;
                foreach (TreeNode child in node.Nodes)
                {
                    if (child.Text == "Configured Applications")
                    {
                        for (int i = 0; i < child.Nodes.Count; i++)
                        {
                            string appName = child.Nodes[i].Text.Split(':')[0].Trim();

                            lock (_statusLock)
                            {
                                var status = _statuses.Find(s => s.NodeName == nodeName && s.AppName == appName);
                                if (status != null)
                                {
                                    child.Nodes[i].Text = $"{appName} : {status.Status}";
                                }
                            }
                        }
                    }
                }
            }
        }

        //Sending the GET_PEERS

        private async void RefreshNetworkStatus()
        {
            var peers = await peers_helper.GetPeersAsync();

            if (peers.Count > 1)
            {
                net_status.Text = "Connected to Network";
                net_status.ForeColor = System.Drawing.Color.Green;
            }
            else
            {
                net_status.Text = "Disconnected";
                net_status.ForeColor = System.Drawing.Color.Red;
            }
        }

        // GetPeersFromService and updates the DataGridView with the received list of peers
        private async Task UpdateDataGridViewWithPeers()
        {
            try
            {
                var peers = await peers_helper.GetPeersAsync();

                // Use Invoke to update the DataGridView on the UI thread
                dataGridView1.Invoke((MethodInvoker)delegate
                {
                    dataGridView1.SuspendLayout(); // Suspend layout to prevent reentrant calls
                    dataGridView1.Rows.Clear();
                    treeView1.Nodes.Clear(); // Clear existing nodes in the TreeView

                    foreach (var peer in peers)
                    {
                        // Add a new row to the DataGridView
                        int rowIndex = dataGridView1.Rows.Add();

                        // Populate the "Available_Nodes" column with the node name
                        dataGridView1.Rows[rowIndex].Cells["Available_Nodes"].Value = peer;

                        // Ensure the "triggering_node" column is initialized with a Boolean value
                        dataGridView1.Rows[rowIndex].Cells["triggering_node"].Value = false; // Default to false

                        // Add the node name to the TreeView without numbering
                        treeView1.Nodes.Add(peer); // No numbering
                    }

                    dataGridView1.ResumeLayout(); // Resume layout after updates
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fetching peers: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        // Periodically Using a Timer to Update Peers List 

        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            // Ensure an item is selected in listView1
            if (listView1.SelectedItems.Count > 0)
            {
                // Get the selected item from listView1
                ListViewItem selectedItem = listView1.SelectedItems[0];

                // Get the application name from the selected item
                string applicationName = selectedItem.Text; // Application name is in the first column

                // Add the application to listView2
                AddAppToListView2(applicationName);
            }
            else
            {
                MessageBox.Show("No app selected in listView1.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            // **1. Handle "Browse" Button Click (Selecting Applications)**
            if (e.ColumnIndex == dataGridView1.Columns["Browse"].Index)
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Executable Files (*.exe)|*.exe";
                openFileDialog.Title = "Select an Application";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedExePath = openFileDialog.FileName;
                    string nodeName = dataGridView1.Rows[e.RowIndex].Cells["Available_Nodes"].Value.ToString();
                    string applicationName = Path.GetFileNameWithoutExtension(selectedExePath);

                    // Add or update node information
                    if (!nodeApplications.ContainsKey(nodeName))
                    {
                        nodeApplications[nodeName] = (false, new List<string>());
                    }

                    nodeApplications[nodeName].applications.Add(applicationName);

                    // Update ListView1 immediately
                    UpdateListView(nodeName);
                }
            }

            // **2. Handle "triggering_node" Column Click (Marking Source Node)**
            if (e.ColumnIndex == dataGridView1.Columns["triggering_node"].Index)
            {
                // Clear listView2 when a new source node is selected
                ClearListView2();

                foreach (DataGridViewRow row in dataGridView1.Rows)
                {
                    if (row.Index != e.RowIndex)
                    {
                        row.Cells["triggering_node"].Value = false; // Uncheck others
                    }
                }

                // Toggle the clicked checkbox
                dataGridView1.Rows[e.RowIndex].Cells["triggering_node"].Value = true;

                // Store the selected source node
                string selectedSourceNode = dataGridView1.Rows[e.RowIndex].Cells["Available_Nodes"].Value.ToString();

                // Update the dictionary to mark the selected node as the source
                foreach (var key in nodeApplications.Keys.ToList())
                {
                    nodeApplications[key] = (key == selectedSourceNode, nodeApplications[key].applications);
                }
            }
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            string selectedNodeName = e.Node.Text; // Get the clicked node name

            // Update listView1 with stored applications
            UpdateListView(selectedNodeName);
        }

        private void UpdateListView(string nodeName)
        {
            listView1.Items.Clear(); // Clear previous entries

            if (nodeApplications.ContainsKey(nodeName))
            {
                foreach (string app in nodeApplications[nodeName].applications)
                {
                    listView1.Items.Add(new ListViewItem(app));
                }
            }
        }


        private void AddAppToListView2(string applicationName)
        {
            // Add the application name to listView2
            ListViewItem item = new ListViewItem(applicationName); // Application name in the first column
            listView2.Items.Add(item);
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            // Clear all items from the ListView
            listView1.Items.Clear();
            listView2.Items.Clear();
            _selectedSourceNode = null;

            // Reset the status of the DataGridView
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                // Uncheck the "triggering_node" checkbox
                row.Cells["triggering_node"].Value = false;
            }

            // Clear the dictionary
            nodeApplications.Clear();

            MessageBox.Show("Reset successful!", "Reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // UPDATE BUTTON

        private void update_Click(object sender, EventArgs e)
        {
            try
            {
                // Find the selected source node from the dictionary
                string sourceNode = nodeApplications.FirstOrDefault(kvp => kvp.Value.isSourceNode).Key;

                if (string.IsNullOrEmpty(sourceNode))
                {
                    MessageBox.Show("No source node selected. Please select a source node by checking the checkbox.",
                                  "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Get the trigger app for the source node
                string triggerApp = GetTriggerAppForSourceNode(sourceNode);

                if (string.IsNullOrEmpty(triggerApp))
                {
                    MessageBox.Show("No trigger app found for the selected source node.",
                                  "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Create the new rule
                var newRule = new Rule
                {
                    SourceNode = sourceNode,
                    TriggerApp = triggerApp,
                    TargetNodes = GetTargetNodes(sourceNode)
                };

                // Read existing rules from the file
                List<Rule> existingRules = read_rules.ReadExistingRules();

                // Find and remove the existing rule for this source node (if it exists)
                //existingRules.RemoveAll(r => r.SourceNode == sourceNode);
                existingRules.Clear();

                // Add the new rule
                existingRules.Add(newRule);

                // Serialize the updated rules to JSON
                string json = JsonConvert.SerializeObject(existingRules, Formatting.Indented);

                // Update the existing RulesConfig.json file
                UpdateRulesConfigFile(json);

                MessageBox.Show("Rules updated successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating rules: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void UpdateRulesConfigFile(string jsonContent)
        {
            string filePath = get_rules_path.GetRulesConfigFilePath();

            // Ensure the directory exists (especially important for CommonApplicationData path)
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write to temporary file first for atomic update
            string tempFilePath = Path.Combine(directory, Guid.NewGuid().ToString() + ".tmp");

            try
            {
                // Write to temp file
                File.WriteAllText(tempFilePath, jsonContent);

                // Replace original file
                File.Delete(filePath);
                File.Move(tempFilePath, filePath);
            }
            finally
            {
                // Clean up temp file if something went wrong
                if (File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); } catch { /* Ignore cleanup errors */ }
                }
            }
        }


        //Save Button Click Event
        private void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                // Find the selected source node from the dictionary
                string sourceNode = nodeApplications.FirstOrDefault(kvp => kvp.Value.isSourceNode).Key;

                if (string.IsNullOrEmpty(sourceNode))
                {
                    MessageBox.Show("No source node selected. Please select a source node by checking the checkbox.",
                                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Get the trigger app for the source node
                string triggerApp = GetTriggerAppForSourceNode(sourceNode);

                if (string.IsNullOrEmpty(triggerApp))
                {
                    MessageBox.Show("No trigger app found for the selected source node.",
                                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Create the JSON structure
                var rules = new List<Rule>
        {
            new Rule
            {
                SourceNode = sourceNode,
                TriggerApp = triggerApp,
                TargetNodes = GetTargetNodes(sourceNode)
            }
        };

                // Serialize the rules to JSON
                string json = JsonConvert.SerializeObject(rules, Formatting.Indented);

                // Save the JSON to a file
                SaveJsonToFile(json);

                MessageBox.Show("Rules saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving rules: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



        // Helper Methods For Save Button Event

        private string GetTriggerAppForSourceNode(string sourceNode)
        {
            if (nodeApplications.TryGetValue(sourceNode, out var nodeData) && nodeData.isSourceNode)
            {
                if (nodeData.applications.Count > 0)
                {
                    return nodeData.applications[0]; // Assuming the first app is the trigger app
                }
            }
            return null; // No trigger app found
        }

        private List<TargetNode> GetTargetNodes(string sourceNode)
        {
            var targetNodes = new List<TargetNode>();

            foreach (var kvp in nodeApplications)
            {
                string nodeName = kvp.Key;
                var (isSource, applications) = kvp.Value;

                // Skip the source node itself
                if (nodeName == sourceNode || isSource)
                    continue;

                // Get the launch app (assuming the first one in the list is the main launch app)
                string launchApp = applications.Count > 0 ? applications[0] : null;

                if (!string.IsNullOrEmpty(launchApp))
                {
                    targetNodes.Add(new TargetNode
                    {
                        NodeName = nodeName,
                        LaunchApp = launchApp,
                        LaunchArguments = "/q" // Fixed launch arguments
                    });
                }
            }

            return targetNodes;
        }

        private void SaveJsonToFile(string json)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "JSON Files (*.json)|*.json";
            saveFileDialog.Title = "Save Rules";
            saveFileDialog.FileName = "rules.json";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                File.WriteAllText(saveFileDialog.FileName, json);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private void ClearListView2()
        {
            listView2.Items.Clear();
            _selectedSourceNode = null; // Clear the stored source node
        }

        // LOGS_TAB


        private string ResolveLogPath()
        {
            // List of possible log file locations (order determines priority)
            List<string> possibleLogPaths = new List<string>
    {
        // 1. First check development path (only on dev machine)
        @"E:\Assist\Assist_Service\Assist_Service\bin\Debug\service.log",
        
        // 2. Check common application data directory
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Assist",
            "service.log"
        ),
        
        // 3. Check application startup directory
        Path.Combine(Application.StartupPath, "service.log")
    };

            // Find the first existing log file
            foreach (var path in possibleLogPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // If no log file exists, create one in CommonApplicationData location
            string defaultLogPath = possibleLogPaths[1]; // CommonApplicationData path
            Directory.CreateDirectory(Path.GetDirectoryName(defaultLogPath));
            return defaultLogPath;
        }


        private void InitializeLogViewer()
        {
            logUpdateTimer.Tick += (s, e) => LoadNewLogs();
            logUpdateTimer.Start();
            btnRefresh.Click += (s, e) => LoadAllLogs();
            LoadAllLogs();
        }

        private void LoadAllLogs()
        {
            try
            {
                lock (fileLock)
                {
                    if (File.Exists(logFilePath))
                    {
                        // Use FileShare.ReadWrite
                        using (var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var reader = new StreamReader(stream))
                        {
                            string text = reader.ReadToEnd();
                            SafeSetText(text);
                            lastFilePosition = stream.Length;
                        }
                    }
                    else
                    {
                        SafeSetText($"[INFO] Log file not found at: {logFilePath}\n");
                    }
                }
            }
            catch (Exception ex)
            {
                SafeAppendText($"[LOG ERROR] {ex.Message}\n");
                Debug.WriteLine($"LoadAllLogs error: {ex}");
            }
        }

        private void LoadNewLogs()
        {
            try
            {
                lock (fileLock)
                {
                    if (File.Exists(logFilePath))
                    {
                        var fileInfo = new FileInfo(logFilePath);
                        if (fileInfo.Length > lastFilePosition)
                        {
                            using (var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                stream.Seek(lastFilePosition, SeekOrigin.Begin);
                                using (var reader = new StreamReader(stream))
                                {
                                    string newContent = reader.ReadToEnd();
                                    if (!string.IsNullOrEmpty(newContent))
                                    {
                                        SafeAppendText(newContent);
                                        lastFilePosition = stream.Position;
                                        ScrollToBottom();
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SafeAppendText($"[LOG ERROR] {ex.Message}\n");
                Debug.WriteLine($"LoadNewLogs error: {ex}");
            }
        }

        // Thread-safe UI methods (unchanged from original)
        private void SafeSetText(string text)
        {
            if (logTextBox.InvokeRequired)
            {
                logTextBox.Invoke(new Action(() => {
                    logTextBox.Text = text;
                    ScrollToBottom();
                }));
            }
            else
            {
                logTextBox.Text = text;
                ScrollToBottom();
            }
        }

        private void SafeAppendText(string text)
        {
            if (logTextBox.InvokeRequired)
            {
                logTextBox.Invoke(new Action(() => logTextBox.AppendText(text)));
            }
            else
            {
                logTextBox.AppendText(text);
            }
        }

        private void ScrollToBottom()
        {
            logTextBox.SelectionStart = logTextBox.TextLength;
            logTextBox.ScrollToCaret();
        }

        private void logUpdateTimer_Tick(object sender, EventArgs e)
        {
            logUpdateTimer.Interval = 1000;
            logUpdateTimer.Enabled = true;
            logUpdateTimer.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Only intercept user closing, allow system shutdown to close normally
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;               // Cancel the close event
                this.Hide();                   // Hide the form
                this.ShowInTaskbar = false;    // Remove from taskbar

                // Show a balloon tip (optional)
                notifyIcon.ShowBalloonTip(1000, "Assist TSR",
                    "The application is still running in the system tray.", ToolTipIcon.Info);

                // Stop any timers / background threads as needed
                logUpdateTimer.Stop();
                isRunning = false;

                if (launchServerThread != null && launchServerThread.IsAlive)
                {
                    if (!launchServerThread.Join(500)) // Wait 500ms for clean shutdown
                    {
                        try { launchServerThread.Interrupt(); } catch { }
                    }
                }

                // Do NOT dispose the notifyIcon here — we want it to remain in the tray
                return; // Exit method early
            }

            // If system is shutting down, close normally
            base.OnFormClosing(e);
        }



        // STATUS_TAB
        private void UpdateServiceStatus()
        {
            string serviceName = "Service1";

            try
            {
                using (ServiceController sc = new ServiceController(serviceName))
                {
                    service_status.Text = sc.Status.ToString();
                    service_status.ForeColor = (sc.Status == ServiceControllerStatus.Running) ? System.Drawing.Color.Green : System.Drawing.Color.Red;
                }
            }
            catch (Exception)
            {
                service_status.Text = "Service Not Found";
                service_status.ForeColor = System.Drawing.Color.Gray;
            }
        }

        private void UpdateUIStarted()
        {
            try
            {
                // -------------------------------
                // 1️⃣ Immediate UI feedback
                // -------------------------------

                this.BeginInvoke((MethodInvoker)delegate {
                    con_stat_val.Text = "Running";
                    con_status.Text = "Active";
                    con_status.ForeColor = Color.Green;
                });

                // -------------------------------
                // 2️⃣ Delayed balloon notification
                // -------------------------------
                Task.Delay(2000).ContinueWith(t =>
                {
                    // Ensure we are on UI thread
                    if (this.InvokeRequired)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            notifyIcon.ShowBalloonTip(1000, "Assist", "TSR started.", ToolTipIcon.Info);
                        });
                    }
                    else
                    {
                        notifyIcon.ShowBalloonTip(1000, "Assist", "TSR started.", ToolTipIcon.Info);
                    }
                });
            }
            catch (Exception ex)
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TSR_UI.log");
                File.AppendAllText(logPath, $"[{DateTime.Now}] Exception in ShowTSRStartedStatus: {ex}\n");
            }
        }

        private void panel3_Paint(object sender, PaintEventArgs e)
        {

        }

        // POWER MANAGEMENT

        private void PowerManager_OnPeersUpdated(List<Assist_Service.Models.Peer> peers)
        {
            tsr_log.Log("Peers Loading...");

            if (panel10.InvokeRequired)
                panel10.Invoke(new Action(() => DisplayOnlineNodes(peers)));
            else
                DisplayOnlineNodes(peers);

            if (panel11.InvokeRequired)
                panel11.Invoke(new Action(() => DisplayOfflineNodes(peers)));
            else
                DisplayOfflineNodes(peers);
        }

        private void DisplayOnlineNodes(List<Assist_Service.Models.Peer> peers)
        {
            // ✅ Clear only peer icons and labels, not the title/buttons
            foreach (Control ctrl in panel10.Controls.OfType<PictureBox>().ToList())
            {
                panel10.Controls.Remove(ctrl);
                ctrl.Dispose();
            }

            foreach (Control ctrl in panel10.Controls.OfType<Label>()
                .Where(l => l.Text != "Online Nodes" && l.Name != "OnlineNodesLabel")
                .ToList())
            {
                panel10.Controls.Remove(ctrl);
                ctrl.Dispose();
            }

            int startY = 60; // leave space below the "Online Nodes" label + buttons
            int x = 10;
            int y = startY;

            int iconSize = 40;
            int margin = 10;
            int maxPerRow = (panel10.Width - 20) / (iconSize + margin);
            int count = 0;

            foreach (var peer in peers.Where(p => p.Status == "Online"))
            {
                PictureBox pcIcon = new PictureBox
                {
                    Width = iconSize,
                    Height = iconSize,
                    Image = Properties.Resources.pc_icon1,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Location = new Point(x, y),
                    Cursor = Cursors.Hand,
                    Tag = peer.NodeName,
                    BorderStyle = selectionManager.SelectedPeers.Contains(peer.NodeName)
                      ? BorderStyle.Fixed3D
                      : BorderStyle.None
                };

                pcIcon.MouseClick += (s, e) => selectionManager.ToggleSelection((PictureBox)s);

                Label nameLabel = new Label
                {
                    Text = peer.NodeName,
                    AutoSize = true,
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter
                };

                nameLabel.Location = new Point(
                    pcIcon.Left + (pcIcon.Width - nameLabel.PreferredWidth) / 2,
                    pcIcon.Bottom + 5
                );

                // Tooltip
                System.Windows.Forms.ToolTip tooltip = new System.Windows.Forms.ToolTip();
                tooltip.SetToolTip(pcIcon, $"Node: {peer.NodeName}\n" +
                    $"MAC: {peer.MacAddress}\n" +
                    $"Status: {peer.Status}\n" +
                    $"Graceful Exit: {peer.LeftGracefully}");

                // Add to panel
                panel10.Controls.Add(pcIcon);
                panel10.Controls.Add(nameLabel);
                pcIcon.BringToFront();
                nameLabel.BringToFront();

                count++;
                if (count % maxPerRow == 0)
                {
                    x = 10;
                    y += iconSize + margin;
                }
                else
                {
                    x += iconSize + margin;
                }
            }
        }


        // Display Offline nodes
        private void DisplayOfflineNodes(List<Assist_Service.Models.Peer> peers)
        {
            // ✅ Remove only peer icons and labels, not static UI (title/buttons)
            foreach (Control ctrl in panel11.Controls.OfType<PictureBox>().ToList())
            {
                panel11.Controls.Remove(ctrl);
                ctrl.Dispose();
            }

            foreach (Control ctrl in panel11.Controls.OfType<Label>()
                .Where(l => l.Text != "Offline Nodes" && l.Name != "OfflineNodesLabel")
                .ToList())
            {
                panel11.Controls.Remove(ctrl);
                ctrl.Dispose();
            }

            int startY = 60; // leave space below the "Offline Nodes" label + buttons
            int x = 10;
            int y = startY;

            int iconSize = 40;
            int margin = 10;
            int maxPerRow = (panel11.Width - 20) / (iconSize + margin);
            int count = 0;

            foreach (var peer in peers.Where(p => p.Status == "Offline"))
            {
                PictureBox pcIcon = new PictureBox
                {
                    Width = iconSize,
                    Height = iconSize,
                    Image = Properties.Resources.pc_icon1, // you can change to a gray/offline icon
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Location = new Point(x, y),
                    Cursor = Cursors.Hand,
                    Tag = peer.NodeName,
                    BorderStyle = selectionManager.SelectedPeers.Contains(peer.NodeName)
                      ? BorderStyle.Fixed3D
                      : BorderStyle.None
                };

                pcIcon.MouseClick += (s, e) => selectionManager.ToggleSelection((PictureBox)s);

                Label nameLabel = new Label
                {
                    Text = peer.NodeName,
                    AutoSize = true,
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter
                };

                nameLabel.Location = new Point(
                    pcIcon.Left + (pcIcon.Width - nameLabel.PreferredWidth) / 2,
                    pcIcon.Bottom + 5
                );

                // Tooltip
                System.Windows.Forms.ToolTip tooltip = new System.Windows.Forms.ToolTip();
                tooltip.SetToolTip(pcIcon, $"Node: {peer.NodeName}\n" +
                    $"MAC: {peer.MacAddress}\n" +
                    $"Status: {peer.Status}\n" +
                    $"Graceful Exit: {peer.LeftGracefully}");

                // Add to panel11
                panel11.Controls.Add(pcIcon);
                panel11.Controls.Add(nameLabel);
                pcIcon.BringToFront();
                nameLabel.BringToFront();

                count++;
                if (count % maxPerRow == 0)
                {
                    x = 10;
                    y += iconSize + margin;
                }
                else
                {
                    x += iconSize + margin;
                }
            }
        }


        private void panel10_Paint(object sender, PaintEventArgs e)
        {

        }

        private void shutdown_Click(object sender, EventArgs e)
        {
            try
            {
                if (powerManager.ShutdownSelectedNodes(selectionManager))
                    MessageBox.Show("Shutdown command sent to selected peers.");
                else
                    MessageBox.Show("No peers were selected.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void sleep_Click(object sender, EventArgs e)
        {
            try
            {
                if (powerManager.SleepSelectedNodes(selectionManager))
                    MessageBox.Show("Sleep command sent to selected peers.");
                else
                    MessageBox.Show("No peers were selected.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }

        }

        // Select Online Nodes Radio Button
        private void select_all_Click(object sender, EventArgs e)
        {
            var pcIcons = panel10.Controls.OfType<PictureBox>();

            if (!selectAllActive)
            {
                // ✅ First click → Select all
                selectionManager.SelectAll(pcIcons);
                selectAllActive = true;
                select_all.Checked = true;
            }
            else
            {
                // ✅ Second click → Deselect all
                selectionManager.DeselectAll(pcIcons);
                selectAllActive = false;
                select_all.Checked = false; // uncheck manually
            }
        }

        private void select_all_2_Click(object sender, EventArgs e)
        {
            var pcIcons = panel11.Controls.OfType<PictureBox>();

            panel11SelectAll = !panel11SelectAll;
            select_all_2.Checked = panel11SelectAll;

            if (panel11SelectAll)
                selectionManager.SelectAll(pcIcons);
            else
                selectionManager.DeselectAll(pcIcons);
        }

        private void power_up_Click(object sender, EventArgs e)
        {
            try
            {
                if (powerManager.PowerUpSelectedNodes(selectionManager))
                    MessageBox.Show("Power Up command sent to selected peers.");
                else
                    MessageBox.Show("No peers were selected.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void delete_Click(object sender, EventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "Are you sure you want to delete the selected peers?\n\nThis action cannot be undone.",
                    "Confirm Delete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                );

                if (result != DialogResult.Yes)
                    return;

                if (powerManager.DeleteSelectedNodes(selectionManager))
                    MessageBox.Show("Selected peers deleted successfully.");
                else
                    MessageBox.Show("No peers were selected for deletion.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void logUpdateTimer_Tick_1(object sender, EventArgs e)
        {
            UpdateServiceStatus();
        }

    }

}