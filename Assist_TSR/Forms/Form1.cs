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
using System.Data;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Runtime.InteropServices;
using System.Net.NetworkInformation;
using System.Reflection;
using static System.Windows.Forms.LinkLabel;



namespace Assist_TSR.Forms
{



    public partial class Form1 : Form
    {
        private Thread launchServerThread; // Remove nullable annotation
        private bool isRunning = false;
        private NotifyIcon notifyIcon; // Manually declare NotifyIcon
        private System.Windows.Forms.Timer refreshTimer, statusTimer;
        private string _selectedSourceNode = null;
        private static bool hasWarnedUser = false;
        private NodeConfig _nodeConfig;
        private const string PipeName = "CustomRulesConfigPipe";
        Dictionary<string, (bool isSourceNode, List<string> applications)> nodeApplications = new Dictionary<string, (bool, List<string>)>();
        // Log Files 
        private readonly string logFilePath;
        private long lastFilePosition = 0;
        private readonly object fileLock = new object();

        // Config_App_Status_IPC
        private List<NodeConfig> _config;
        private List<AppStatus> _statuses = new List<AppStatus>();



        public Form1()
        {
            InitializeComponent();

            // Initialize other components
            InitializeSystemTrayIcon();
            StartLaunchServer();
            InitializeTimer(); // Initialize the timer
            InitializeDataGridView();
            // Initialize log file path FIRST
            logFilePath = ResolveLogPath();

            // Setting Up Tree Status
            LoadConfig();
            StartListeningToAppStatusPipe();
            SetupTreeView();

            // Set log file path to match your service
            //logFilePath = Path.Combine("E:\\Assist\\Assist_Service\\Assist_Service\\bin\\Debug", "service.log");
            InitializeLogViewer(); // Initialize log viewer

            // Attach event handlers
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            dataGridView1.CellClick += dataGridView1_CellClick;

            // Attach the DoubleClick event handler for listView1
            listView1.DoubleClick += listView1_DoubleClick;

            

            // Attach the Save button click event handler
            btnSave.Click += btnSave_Click;

            // Attach Event handler for TreeView1
            treeView1.AfterSelect += treeView1_AfterSelect;

            // Set the ListView to display items in a vertical list
            listView1.View = View.List;
            listView1.Scrollable = true; // Enable scrolling if needed

            // Attach the Reset button click event handler
            btnReset.Click += btnReset_Click;



        }

        // LOGGING FOR TSR

        private void Log(string message)
        {
            // Simple implementation - you can enhance this as needed
            System.Diagnostics.Debug.WriteLine(message);

            // Optional: Write to a log file
            string logPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Assist_TSR.log");
            File.AppendAllText(logPath, $"{DateTime.Now}: {message}{Environment.NewLine}");
        }

        // GETTING DATA FOR GENERAL TAB FROM SERVICE
        private async Task<string> RequestDataFromServiceAsync(string requestType)
        {
            NamedPipeClientStream pipeClient = null;
            try
            {
                string pipeName = requestType == "GET_NODE_NAME"
                    ? "AssistNodeNamePipe"
                    : "AssistActivePresetPipe";

                pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);

                // Connect with timeout (same as synchronous version)
                var connectTask = pipeClient.ConnectAsync();
                var timeoutTask = Task.Delay(3000);
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    throw new System.TimeoutException("Service connection timeout");
                }
                await connectTask; // This will throw if there was a connection error

                // Write request
                var writer = new StreamWriter(pipeClient) { AutoFlush = true };
                await writer.WriteLineAsync(requestType);

                // Read response
                var reader = new StreamReader(pipeClient);
                string response = await reader.ReadLineAsync();

                return response ?? "Unknown";
            }
            catch (System.TimeoutException)
            {
                WarnOnce("Service connection timeout");
                return "Unknown";
            }
            catch (Exception ex)
            {
                WarnOnce($"Service communication failed: {ex.Message}");
                return "Unknown";
            }
            finally
            {
                pipeClient?.Dispose();
            }
        }

        // Notification Bar within UI (unchanged)
        private void ShowNotification(string message)
        {
            notificationLabel.Text = message;
            notificationPanel.Visible = true;
        }

        private void closeNotificationButton_Click_1(object sender, EventArgs e)
        {
            notificationPanel.Visible = false;
        }

        private void WarnOnce(string message)
        {
            if (!hasWarnedUser)
            {
                ShowNotification(message);
                hasWarnedUser = true;
            }
        }

        // Updated to use the new async method directly
        private async Task<string> FetchDataAsync(string requestType)
        {
            await Task.Delay(100); // Let server initialize
            return await RequestDataFromServiceAsync(requestType);
        }

        // UPDATING_GENERAL_TAB
        private async void UpdateUIWithStatus()
        {
            // Request Node Name from the service
            string nodeName = await FetchDataAsync("GET_NODE_NAME");

            node_value.Text = nodeName;

            // Request Active Preset from the service
            string activePreset = await FetchDataAsync("GET_ACTIVE_PRESET");
            preset_value.Text = activePreset;

            // Update Console Status directly from the Windows Forms application
            con_stat_val.Text = isRunning ? "Running" : "Stopped";
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
            if (!isRunning)
            {
                isRunning = true;
                if (launchServerThread != null && launchServerThread.IsAlive)
                {
                    launchServerThread.Join(500); // Cleanup any previous thread
                }

                launchServerThread = new Thread(StartLaunchServer);
                launchServerThread.IsBackground = true;
                launchServerThread.Start();

                // Immediate UI feedback
                con_stat_val.Text = "Running";
                con_status.Text = "Active";
                con_status.ForeColor = Color.Green;

                // Delayed notification
                Task.Delay(1000).ContinueWith(t => {
                    this.Invoke((MethodInvoker)delegate {
                        notifyIcon.ShowBalloonTip(1000, "Assist", "TSR started.", ToolTipIcon.Info);
                    });
                });
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

        // Form Clean Ups



        private void StartLaunchServer()
        {
            while (isRunning)
            {
                try
                {
                    using (NamedPipeServerStream pipeServer = new NamedPipeServerStream("LaunchHandlerPipe", PipeDirection.InOut))
                    {
                        Debug.WriteLine("Waiting for launch request...");
                        pipeServer.WaitForConnection();
                        Debug.WriteLine("Launch request received.");

                        using (StreamReader reader = new StreamReader(pipeServer))
                        using (StreamWriter writer = new StreamWriter(pipeServer))
                        {
                            string request = reader.ReadLine();
                            Debug.WriteLine($"Received request: {request}");

                            if (request != null && request.StartsWith("LAUNCH:"))
                            {
                                string appName = request.Substring("LAUNCH:".Length);

                                if (LaunchApplication(appName))
                                {
                                    writer.WriteLine("SUCCESS");
                                    Debug.WriteLine($"Successfully launched: {appName}");
                                }
                                else
                                {
                                    writer.WriteLine("ERROR: Failed to launch");
                                    Debug.WriteLine($"Failed to launch: {appName}");
                                }

                                writer.Flush();
                            }
                            else
                            {
                                writer.WriteLine("ERROR: Invalid request");
                                writer.Flush();
                                Debug.WriteLine("Invalid request received.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in Launch Handler: {ex.Message}");
                }
            }
        }

        private bool LaunchApplication(string appName)
        {
            try
            {
                Process.Start(new ProcessStartInfo(appName) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error launching application: {ex.Message}");
                return false;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Hide();  // Hide the form on startup
        }

        private void NotifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
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

        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {
            
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void flowLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void flowLayoutPanel3_Paint(object sender, PaintEventArgs e)
        {

        }

        private void panel4_Paint(object sender, PaintEventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }


        private async void start_Click(object sender, EventArgs e)
        {
            try
            {
                green.Visible = true;
                red.Visible = false;
                start.Enabled = false; // Disable button during operation

                await Task.Run(() => OnStart(sender, e));
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

                await Task.Run(() => OnStop(sender, e));
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
                string configPath = GetConfigFilePath();

                // Create configuration object
                var config = new NodeConfig
                {
                    NodeName = nodeName
                };

                // Thread-safe write operation
                FileHelper.WriteJsonWithRetry(configPath, config);

                // Notify the service about the change
                NotifyServiceAboutConfigChange(nodeName);

                MessageBox.Show("Configuration saved successfully!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving configuration: {ex.Message}");
            }
        }

        private string GetConfigFilePath()
        {
            // List of possible config file locations (order determines priority)
            List<string> possibleConfigPaths = new List<string>
    {
        // 1. First check development path (only on dev machine)
        @"E:\Assist\Assist_Service\Assist_Service\bin\Debug\NodeConfig.json",
        
        // 2. Check application startup directory
        Path.Combine(Application.StartupPath, "NodeConfig.json"),
        
        // 3. Check common application data directory
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Assist",
            "NodeConfig.json"
        ),
        
        // 4. Fallback to executable directory (for service)
        Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "NodeConfig.json"
        )
    };

            string configPath = null;

            // Find the first existing config file
            foreach (var path in possibleConfigPaths)
            {
                if (File.Exists(path))
                {
                    configPath = path;
                    break;
                }
            }

            // If no existing file found, determine where we should create it
            if (configPath == null)
            {
                // Choose where to create new config file based on environment
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    // Development environment - use debug folder
                    configPath = possibleConfigPaths[0];
                }
                else if (Environment.UserInteractive)
                {
                    // Running as application - use application data folder
                    configPath = possibleConfigPaths[2];

                    // Ensure directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                }
                else
                {
                    // Running as service - use executable directory
                    configPath = possibleConfigPaths[3];
                }

                // Create default config file
                var defaultConfig = new NodeConfig { NodeName = "DefaultNode" };
                FileHelper.WriteJsonWithRetry(configPath, defaultConfig);
            }

            return configPath;
        }

        // Communication with Windows Service
        private void NotifyServiceAboutConfigChange(string newNodeName)
        {
            try
            {
                using (var pipeClient = new NamedPipeClientStream(".", "AssistNodeNamePipe", PipeDirection.InOut))
                {
                    pipeClient.Connect(3000); // 3 second timeout

                    var writer = new StreamWriter(pipeClient);
                    var reader = new StreamReader(pipeClient);

                    writer.WriteLine("UPDATE_NODE_NAME:" + newNodeName);
                    writer.Flush();

                    string response = reader.ReadLine();

                    if (response != "OK")
                    {
                        Log($"Service responded with: {response}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error notifying service: {ex.Message}");
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
                        await SendPathToService(openFileDialog.FileName);
                        ShowMessage("Configuration sent to service",
                                  $"Successfully sent:\n{openFileDialog.FileName}");
                    }
                    catch (Exception ex)
                    {
                        ShowError("Configuration Error",
                                $"Failed to send configuration:\n{ex.Message}");
                    }
                }
            }
        }

        // Button click event for getting current config
        private async void btnGetConfig_Click(object sender, EventArgs e)
        {
            try
            {
                var config = await GetConfigFromService();
                DisplayConfig(config);
            }
            catch (Exception ex)
            {
                ShowError("Configuration Error",
                        $"Failed to get configuration:\n{ex.Message}");
            }
        }

        // Send file path to service via named pipe
        private async Task SendPathToService(string path)
        {
            await Task.Run(() =>
            {
                using (var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
                {
                    pipeClient.Connect(5000); // 5 second timeout
                    using (var writer = new StreamWriter(pipeClient))
                    {
                        writer.Write(path);
                        writer.Flush();
                    }
                }
            });
        }

        // Get config from service via named pipe
        private async Task<List<RuleConfig>> GetConfigFromService()
        {
            return await Task.Run(() =>
            {
                using (var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
                {
                    pipeClient.Connect(5000); // 5 second timeout
                    using (var writer = new StreamWriter(pipeClient))
                    using (var reader = new StreamReader(pipeClient))
                    {
                        writer.Write("GET_CONFIG");
                        writer.Flush();
                        string json = reader.ReadToEnd();
                        return JsonConvert.DeserializeObject<List<RuleConfig>>(json);
                    }
                }
            });
        }

        // Display the configuration in a readable format
        private void DisplayConfig(List<RuleConfig> config)
        {
            if (config == null || config.Count == 0)
            {
                ShowMessage("Configuration", "No rules configured");
                return;
            }

            var result = new System.Text.StringBuilder();
            result.AppendLine("Current Configuration Rules");
            result.AppendLine("==========================");

            foreach (var rule in config)
            {
                result.AppendLine($"\nSource Node: {rule.SourceNode}");
                result.AppendLine($"Trigger App: {rule.TriggerApp}");
                result.AppendLine("Target Nodes:");

                if (rule.TargetNodes != null)
                {
                    foreach (var target in rule.TargetNodes)
                    {
                        result.AppendLine($"- {target.NodeName} (Launch: {target.LaunchApp})");
                    }
                }
                else
                {
                    result.AppendLine("No target nodes configured");
                }
            }

            ShowMessage("Current Configuration", result.ToString());
        }

        // Helper method for success messages
        private void ShowMessage(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Helper method for error messages
        private void ShowError(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        // Setting up Configured App Status IPC

        private void LoadConfig()
        {
            // List of possible config file locations (order determines priority)
            List<string> possibleConfigPaths = new List<string>
    {
        // 1. First check development path (only on dev machine)
        @"E:\Assist\Assist_Service\Assist_Service\bin\Debug\RulesConfig.json",
        
        // 2. Check application startup directory
        Path.Combine(Application.StartupPath, "RulesConfig.json"),
        
        // 3. Check common application data directory
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Assist",
            "RulesConfig.json"
        )
    };

            string configPath = null;

            // Find the first existing config file
            foreach (var path in possibleConfigPaths)
            {
                if (File.Exists(path))
                {
                    configPath = path;
                    break;
                }
            }

            if (configPath == null)
            {
                throw new FileNotFoundException(
                    $"Config file not found at any of these locations:\n" +
                    $"- {possibleConfigPaths[0]}\n" +
                    $"- {possibleConfigPaths[1]}\n" +
                    $"- {possibleConfigPaths[2]}"
                );
            }

            // Load and parse the JSON
            string json = File.ReadAllText(configPath);
            _config = JsonConvert.DeserializeObject<List<NodeConfig>>(json);
        }

        private void SetupTreeView()
        {
            treeView2.Nodes.Clear();

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

        private void StartListeningToAppStatusPipe()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    using (NamedPipeServerStream pipeServer = new NamedPipeServerStream("AssistStatusPipe", PipeDirection.In))
                    {
                        pipeServer.WaitForConnection();

                        using (StreamReader reader = new StreamReader(pipeServer))
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                string[] parts = line.Split(':');
                                if (parts.Length == 3)
                                {
                                    string node = parts[0];
                                    string app = parts[1];
                                    string status = parts[2];

                                    lock (_statuses)
                                    {
                                        var existing = _statuses.Find(s => s.NodeName == node && s.AppName == app);
                                        if (existing != null)
                                            existing.Status = status;
                                        else
                                            _statuses.Add(new AppStatus { NodeName = node, AppName = app, Status = status });
                                    }

                                    Invoke(new Action(UpdateTreeView));
                                }
                            }
                        }
                    }
                }
            });
        }

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

        //Sending the GET_PEERS
        private async Task<List<string>> GetPeersFromService()
        {
            var peers = new List<string>();

            using (var pipeClient = new NamedPipeClientStream(".", "AssistPeersPipe", PipeDirection.InOut))
            {
                await pipeClient.ConnectAsync();

                using (var reader = new StreamReader(pipeClient))
                using (var writer = new StreamWriter(pipeClient))
                {
                    // Send GET_PEERS request
                    await writer.WriteLineAsync("GET_PEERS");
                    await writer.FlushAsync();

                    // Read the response
                    string response = await reader.ReadLineAsync();
                    peers = JsonConvert.DeserializeObject<List<string>>(response);
                }
            }

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

            return peers;
        }

        // GetPeersFromService and updates the DataGridView with the received list of peers
        private async Task UpdateDataGridViewWithPeers()
        {
            try
            {
                var peers = await GetPeersFromService();

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

        private void InitializeTimer()
        {
            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = 5000; // Refresh every 5 seconds
            refreshTimer.Tick += RefreshTimer_Tick;
            refreshTimer.Start();
        }

        private async void RefreshTimer_Tick(object sender, EventArgs e)
        {
            await UpdateDataGridViewWithPeers();
        }

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

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

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
                List<Rule> existingRules = ReadExistingRules();

                // Find and remove the existing rule for this source node (if it exists)
                existingRules.RemoveAll(r => r.SourceNode == sourceNode);

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

        private List<Rule> ReadExistingRules()
        {
            string filePath = GetRulesConfigFilePath();

            if (!File.Exists(filePath))
            {
                return new List<Rule>();
            }

            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<List<Rule>>(json) ?? new List<Rule>();
        }

        private string GetRulesConfigFilePath()
        {
            // List of possible config file locations (order determines priority)
            List<string> possibleConfigPaths = new List<string>
    {
        // 1. First check development path (only on dev machine)
        @"E:\Assist\Assist_Service\Assist_Service\bin\Debug\RulesConfig.json",
        
        // 2. Check application startup directory
        Path.Combine(Application.StartupPath, "RulesConfig.json"),
        
        // 3. Check common application data directory
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Assist",
            "RulesConfig.json"
        )
    };

            // Return the first existing file path
            foreach (string path in possibleConfigPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // If none exist, return the most appropriate path (application startup directory)
            return possibleConfigPaths[1];
        }

        private void UpdateRulesConfigFile(string jsonContent)
        {
            string filePath = GetRulesConfigFilePath();

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
            // Stop the timer first
            logUpdateTimer.Stop();

            // Add your TSR cleanup logic
            isRunning = false;

            // Clean up the server thread with timeout to prevent freezing
            if (launchServerThread != null && launchServerThread.IsAlive)
            {
                if (!launchServerThread.Join(500)) // Wait up to 500ms for clean shutdown
                {
                    // Thread didn't exit cleanly in time
                    try { launchServerThread.Interrupt(); } catch { }
                }
            }

            // Clean up system tray icon
            notifyIcon.Visible = false;
            notifyIcon.Dispose();

            // Call base method last
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

        private void panel3_Paint(object sender, PaintEventArgs e)
        {

        }

        private void logUpdateTimer_Tick_1(object sender, EventArgs e)
        {
            UpdateServiceStatus();
        }

    }

   

    public class Rule
    {
        public string SourceNode { get; set; }
        public string TriggerApp { get; set; }
        public List<TargetNode> TargetNodes { get; set; }
    }

    public class TargetNode
    {
        public string NodeName { get; set; }
        public string LaunchApp { get; set; }
        public string LaunchArguments { get; set; }
    }

    public class NodeConfig
    {
        public string NodeName { get; set; }
        public string SourceNode { get; set; }
        public string TriggerApp { get; set; }
        public List<TargetNode> TargetNodes { get; set; }
    }

    public class AppStatus
    {
        public string NodeName { get; set; }
        public string AppName { get; set; }
        public string Status { get; set; }
    }

    public class RuleConfig
    {
        public string SourceNode { get; set; }
        public string TriggerApp { get; set; }
        public List<TargetNode> TargetNodes { get; set; }
    }

    

    // Class For Thread-Safe File Operations
    public static class FileHelper
    {
        private static readonly object _fileLock = new object();

        public static void WriteJsonWithRetry(string path, object data, int retries = 3, int delay = 100)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    lock (_fileLock)
                    {
                        string tempPath = path + ".tmp";
                        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                        File.WriteAllText(tempPath, json);
                        File.Replace(tempPath, path, null);
                    }
                    return;
                }
                catch (IOException) when (i < retries - 1)
                {
                    Thread.Sleep(delay);
                }
            }
        }

        public static T ReadJsonWithRetry<T>(string path, int retries = 3, int delay = 100)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    lock (_fileLock)
                    {
                        string json = File.ReadAllText(path);
                        return JsonConvert.DeserializeObject<T>(json);
                    }
                }
                catch (IOException) when (i < retries - 1)
                {
                    Thread.Sleep(delay);
                }
            }
            return default;
        }
    }

}