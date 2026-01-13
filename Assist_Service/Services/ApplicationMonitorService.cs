using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Assist_Service.Helpers;
using Assist_Service.Models;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;
using Assist_Service.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Assist_Service.Services
{
    public class ApplicationMonitorService
    {
        private List<string> _monitoredApplications;
        public List<string> MonitoredApplications => _monitoredApplications;
        //private readonly List<string> _runningApplications = new List<string>();
        private readonly NodeConfig _nodeConfig;
        private RuleProcessingService _ruleService;
        private PipeCommunicationService _GetPath ;
        public List<RuleConfig> _rules = new List<RuleConfig>(); // Shared reference

        public readonly object _rulesLock = new object(); // Lock to protect shared access
        private readonly List<Peer> _peers;
        private readonly object _peersLock;
        private readonly UdpClient _udpClient;
        private readonly Logging Logger = new Logging();
        private readonly Test_Log Log = new Test_Log();
        private bool _isRunning;
        private const string PipeName = "AssistServicePipe";
        
        private const int UdpPort = 12346;
        private Thread _listenerThread;
        
        private Dictionary<string, int> _processCounts = new Dictionary<string, int>();
        public HashSet<string> _runningApplications = new HashSet<string>();
        private Dictionary<string, int> _previousProcessCounts = new Dictionary<string, int>();
        private List<RuleConfig> _cachedRules = null;
        private readonly object _rulesCacheLock = new object();
        private DateTime _lastRulesUpdateTime = DateTime.MinValue;
        private FileSystemWatcher _fileWatcher;
        private string _currentWatchedPath;
        private List<RuleConfig> _currentRules = new List<RuleConfig>();
        private Timer _refreshTimer;
        private List<string> _monitoredApp;




        public ApplicationMonitorService(
            List<string> monitoredApplications,
            NodeConfig nodeConfig,
            List<Peer> peers,
            object peersLock,
            UdpClient udpClient,
            RuleProcessingService ruleService,
            PipeCommunicationService pipeService
            )
        {
            _monitoredApplications = monitoredApplications;
            _nodeConfig = nodeConfig;
            _peers = peers;
            _peersLock = peersLock;
            _udpClient = udpClient;
            _ruleService = ruleService;
            _GetPath = pipeService;
            // Initial load
            var latestRules = GetLatestRules();
            lock (_rulesLock)
            {
                _rules.Clear();
                _rules.AddRange(latestRules);
            }

            // Start timer to refresh rules every 60 seconds
            _refreshTimer = new Timer(_ =>
            {
                var updatedRules = GetLatestRules();
                lock (_rulesLock)
                {
                    _rules.Clear();
                    _rules.AddRange(updatedRules);
                }
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(60));
        
        }

        public List<RuleConfig> GetSharedRules()
        {
            lock (_rulesLock)
            {
                return new List<RuleConfig>(_rules); // Return a copy to avoid outside mutation
            }
        }


        public void Start()
        {
            _isRunning = true;
            Thread monitorThread = new Thread(MonitorApplicationLaunches);
            
            monitorThread.IsBackground = true;
            
            Thread ListenUDP = new Thread(new ThreadStart(ListenForUdpCommands));
            //_listenerThread = new Thread(new ThreadStart(ListenForUdpCommands));
            //_listenerThread.IsBackground = true;
            ListenUDP.IsBackground = true;




            ListenUDP.Start();
            monitorThread.Start();
            
        }

        public void Stop()
        {
            _isRunning = false;
        }




        private void MonitorApplicationLaunches()
        {
            Logger.Log("MonitorApplicationLaunches thread started.");

            while (_isRunning)
            {
                try
                {
                    var processes = Process.GetProcesses();

                    // Count how many instances of each monitored app are running
                    var currentCounts = processes
                        .Where(p => _monitoredApplications.Contains(p.ProcessName))
                        .GroupBy(p => p.ProcessName)
                        .ToDictionary(g => g.Key, g => g.Count());

                    // Ensure all monitored apps are initialized in _processCounts
                    foreach (var app in _monitoredApplications)
                    {
                        if (!_processCounts.ContainsKey(app))
                            _processCounts[app] = 0;

                        currentCounts.TryGetValue(app, out int currentCount);
                        int previousCount = _processCounts[app];

                        if (currentCount > previousCount)
                        {
                            Logger.Log($"New instance(s) of {app} detected. New: {currentCount}, Previous: {previousCount}");

                            // Trigger HandleApplicationTrigger for each new instance
                            for (int i = 0; i < currentCount - previousCount; i++)
                            {
                                Logger.Log($"Triggering instance {i + 1} of {app}");
                                HandleApplicationTrigger(app);
                            }
                        }
                    }

                    // Update counts for the next iteration
                    _processCounts = currentCounts;

                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error in MonitorApplicationLaunches: {ex.Message}");
                }
            }

            Logger.Log("MonitorApplicationLaunches thread stopped.");
        }


        public void UpdateMonitoredApps(List<string> latestApps)
        {
            lock (_monitoredApplications)
            {
                _monitoredApplications = latestApps;
            }
        }

        private void HandleApplicationTrigger(string appName)
        {
            Logger.Log($"Handling trigger for application: {appName}");

            var applicableRules = _rules.Where(r =>
                r.SourceNode.Equals(_nodeConfig.NodeName, StringComparison.OrdinalIgnoreCase) &&
                r.TriggerApp.Equals(appName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            Logger.Log($"Found {applicableRules.Count} applicable rule(s) for {appName}.");

            foreach (var rule in applicableRules)
            {
                foreach (var target in rule.TargetNodes)
                {
                    lock (_peersLock)
                    {
                        var peer = _peers.FirstOrDefault(p =>
                            p.NodeName.Equals(target.NodeName, StringComparison.OrdinalIgnoreCase));
                        if (peer != null)
                        {
                            Logger.Log($"Sending launch command for {target.LaunchApp} to {peer.NodeName} at 12346.");
                            var fixedEndPoint = new IPEndPoint(peer.EndPoint.Address, 12346);
                            SendLaunchCommand(target.LaunchApp, fixedEndPoint);
                        }
                        else
                        {
                            Logger.Log($"Peer not found for target node: {target.NodeName}");
                        }
                    }
                }
            }
        }

        private void SendLaunchCommand(string appName, IPEndPoint targetEndPoint)
        {
            try
            {
                Logger.Log($"Preparing to send launch command for {appName} to {targetEndPoint}");

                //string token = SecurityHelper.GenerateToken("APP_LAUNCH");
                //string message = $"APP_LAUNCH:{appName}:{token}";
                string message = $"APP_LAUNCH:{appName}";
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                _udpClient.Send(bytes, bytes.Length, targetEndPoint);

                Logger.Log($"Launch command sent for {appName} to {targetEndPoint}");
            }
            catch (Exception ex)
            {
                Logger.Log($"Error sending launch command to {targetEndPoint}: {ex.Message}");
            }
        }

        private void ListenForUdpCommands()
        {
            UdpClient udpClient = null;

            try
            {
                udpClient = new UdpClient(UdpPort);
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                Logger.Log($"UDP listener started on port {UdpPort}.");

                while (_isRunning)
                {
                    try
                    {
                        byte[] bytes = udpClient.Receive(ref remoteEP);
                        string message = Encoding.UTF8.GetString(bytes);
                        Logger.Log($"Received UDP message from {remoteEP}: {message}");

                        if (message.StartsWith("APP_LAUNCH:"))
                        {
                            string[] parts = message.Split(':');
                            if (parts.Length == 2)
                            {
                                string appName = parts[1];
                                //string token = parts[2];

                                //Logger.Log($"App launch command received: {appName} with token {token}");
                                Logger.Log($"App launch command received: {appName}");

                                /*if (SecurityHelper.ValidateToken(token, "APP_LAUNCH"))
                                {
                                   Logger.Log($"Token validated. Forwarding launch request to pipe for app: {appName}");
                                    SendToAppLaunchNamedPipe(appName);
                                }*/
                                
                                
                                    Logger.Log($"Forwarding launch request to pipe for app: {appName}");
                                    SendToAppLaunchNamedPipe(appName);
                                
                                /*else
                                {
                                    Logger.Log($"Invalid token received for app: {appName}");
                                }*/
                            }
                            else
                            {
                                Logger.Log("Malformed APP_LAUNCH message format.");
                            }
                        }
                        else
                        {
                            Logger.Log("Unknown UDP message format received.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"UDP listener error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to start UDP listener on port {UdpPort}: {ex.Message}");
            }
            finally
            {
                udpClient?.Close();
                Logger.Log("UDP listener shut down.");
            }
        }

        private void SendToAppLaunchNamedPipe(string appName)
        {
            try
            {
                using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
                {
                    Logger.Log($"Connecting to named pipe: {PipeName}...");
                    pipeClient.Connect(2000); // 2-second timeout
                    Logger.Log("Connected to named pipe.");

                    using (StreamWriter writer = new StreamWriter(pipeClient))
                    using (StreamReader reader = new StreamReader(pipeClient))
                    {
                        writer.AutoFlush = true;
                        writer.WriteLine($"LAUNCH:{appName}");
                        Logger.Log($"Sent LAUNCH command to pipe: {appName}");

                        string response = reader.ReadLine();
                        Logger.Log($"Received response from pipe: {response}");

                        if (response != null && response.StartsWith("SUCCESS"))
                        {
                            Logger.Log($"Launch command for '{appName}' processed successfully.");
                        }
                        else
                        {
                            Logger.Log($"Error processing launch command for '{appName}': {response}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error sending to named pipe: {ex.Message}");
            }
        }
    

        



        public List<RuleConfig> GetLatestRules()
        {
            string configPath = _GetPath.GetActiveConfigPath();
            Log.Log($"Path {configPath}");

            // Initialize/update file watcher if needed
            if (_fileWatcher == null || _currentWatchedPath != configPath)
            {
                if (_fileWatcher != null)
                {
                    _fileWatcher.Dispose();
                }

                string directory = Path.GetDirectoryName(configPath);
                string fileName = Path.GetFileName(configPath);

                _fileWatcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = true
                };

                _fileWatcher.Changed += (sender, e) => RefreshRules(configPath);
                _fileWatcher.Renamed += (sender, e) => RefreshRules(configPath);
                _fileWatcher.Deleted += (sender, e) => _currentRules = new List<RuleConfig>();

                _currentWatchedPath = configPath;
            }

            // Read and return current rules
            try
            {
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    return JsonConvert.DeserializeObject<List<RuleConfig>>(json) ?? new List<RuleConfig>();
                }
                return new List<RuleConfig>();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error reading rules: {ex.Message}");
                return new List<RuleConfig>();
            }
        }

        private void RefreshRules(string configPath)
        {
            try
            {
                Thread.Sleep(100); // Allow file write to complete
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    _currentRules = JsonConvert.DeserializeObject<List<RuleConfig>>(json) ?? new List<RuleConfig>();
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error refreshing rules: {ex.Message}");
            }
        }

    }
}