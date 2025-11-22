using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Threading;
using Assist_Service.Helpers;
using Assist_Service.Models;
using Assist_Service.Services;
using Newtonsoft.Json;
using System.Linq;

namespace Assist_Service
{
    public partial class Service1 : ServiceBase
    {
        public static NodeConfig _nodeConfig;
        private List<RuleConfig> _rules;
        public static List<Peer> _peers = new List<Peer>();
        public static readonly object _peersLock = new object();
        private bool _isRunning;
        private string _ruleFileAddress;

        public static UdpClient _udpClient;
        private DiscoveryService _discoveryService;
        private ApplicationMonitorService _appMonitorService;
        private PipeCommunicationService _pipeService;
        private RuleProcessingService _ruleService;
        private ApplicationClosureService _appClosureService;
        private Logging Logger = new Logging();
        private Test_Log Log = new Test_Log();
        private Remote_Power_Log PWR_Log = new Remote_Power_Log();
        private System.Timers.Timer _monitoredAppsRefreshTimer;
        private List<string> _monitoredApplications = new List<string>();
        private Thread _AppClosureListenerThread;
        private readonly object _runningAppsLock = new object();
        private Remote_Power_Management_Service _remotePowerService;


        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            if (_isRunning) return;
            _isRunning = true;
            Logger.Log("Service is starting...");

            try
            {
                // Clear old data for this node before starting
                PeerFileStorage.ClearAllNodes();

                Logger.Log("Loading node configuration...");
                _nodeConfig = FileHelper.ReadJsonWithRetry<NodeConfig>(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NodeConfig.json"));
                Logger.Log("Node configuration loaded successfully.");

                Logger.Log("Initializing UDP client...");
                _udpClient = new UdpClient(DiscoveryService.BroadcastPort);
                _udpClient.JoinMulticastGroup(DiscoveryService.MulticastAddress);
                Logger.Log("UDP client initialized and joined multicast group.");


                _ruleService = new RuleProcessingService();
                Logger.Log("Initializing services...");
                _discoveryService = new DiscoveryService(_udpClient, _nodeConfig, _peers, _peersLock);
                _pipeService = new PipeCommunicationService(_nodeConfig, _peers, _peersLock, _rules);
                _appMonitorService = new ApplicationMonitorService(
                    GetMonitoredApps(),
                    _nodeConfig,
                    _peers,
                    _peersLock,
                    _udpClient,
                    _ruleService,
                    _pipeService
                    );



                Logger.Log("Retrieving active rule configuration path from address file...");
                string configPath = _pipeService.ReadRulesPathFromAddressFile();
                Logger.Log($"Active config path obtained: {configPath}");

                Logger.Log("Loading rule configurations...");
                _rules = FileHelper.ReadJsonWithRetry<List<RuleConfig>>(configPath);
                Logger.Log("Rule configurations loaded successfully.");

                //Closure Service


                _appClosureService = new ApplicationClosureService(
                    GetMonitoredApps(),
                    _appMonitorService._rules,       // shared list
                    _appMonitorService._rulesLock,   // shared lock
                    _peers,
                    _peersLock,
                    _nodeConfig,
                    _appMonitorService._runningApplications,
                    _runningAppsLock
                    );

                _monitoredAppsRefreshTimer = new System.Timers.Timer(10000); // 10 seconds
                _monitoredAppsRefreshTimer.Elapsed += (sender, e) =>
                {
                    try
                    {
                        var latestApps = GetMonitoredApps();
                        _appMonitorService.UpdateMonitoredApps(latestApps);
                        _appClosureService.UpdateMonitoredApps(latestApps);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Failed to refresh monitored apps: {ex.Message}");
                    }
                };

                Thread monitor_Closure_Thread = new Thread(_appClosureService.MonitorApplicationClosures);
                monitor_Closure_Thread.IsBackground = true;
                _AppClosureListenerThread = new Thread(_appClosureService.ListenForUdpClosureCommands);
                _AppClosureListenerThread.IsBackground = true;


                Logger.Log("All services initialized.");

                Logger.Log("Starting services...");
                _monitoredAppsRefreshTimer.AutoReset = true;
                _monitoredAppsRefreshTimer.Start();
                _discoveryService.Start();
                _appMonitorService.Start();
                _pipeService.Start();
                monitor_Closure_Thread.Start();
                _AppClosureListenerThread.Start();
                
                Logger.Log("All services started successfully.");

                // Remote Power Management

                _remotePowerService = new Remote_Power_Management_Service();
                _remotePowerService.Start();
                Logger.Log("Remote Power Management Service started.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Service failed to start. Exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
                throw;
            }
        }

        protected override void OnStop()
        {
            _isRunning = false;

            //_discoveryService?.Stop();
            _discoveryService.Stop();
            _appMonitorService?.Stop();
            _pipeService?.Stop();

            _udpClient?.DropMulticastGroup(DiscoveryService.MulticastAddress);
            _udpClient?.Close();
            _monitoredAppsRefreshTimer?.Stop();
            _monitoredAppsRefreshTimer?.Dispose();

            try
            {
                if (_remotePowerService != null)
                {
                    _remotePowerService.Stop();    // tell it to stop
                    _remotePowerService = null;    // release reference
                    Logger.Log("Remote Power Management Service stopped.");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to stop Remote Power Management Service: {ex.Message}");
            }
        }

        private List<string> GetMonitoredApps()
        {
            // Read the rules file address from RulesAddress.txt
            string rulesFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RulesAddress.txt");
            if (!File.Exists(rulesFilePath))
            {
                Log.Log("RulesAddress.txt not found in the base directory");
            }

            string rulesFileAddress = File.ReadAllText(rulesFilePath).Trim();
            string rulesFileAddress_in_case_of_error = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RulesConfig.json");

            if (string.IsNullOrEmpty(rulesFileAddress))
            {
                Log.Log("Rules file address is empty in RulesAddress.txt");
            }

            // Read the rules file
            string rulesJson;
            if (!File.Exists(rulesFileAddress))
            {
                Log.Log($"Rules file not found at: {rulesFileAddress}");
                rulesJson = File.ReadAllText(rulesFileAddress_in_case_of_error);
            }

            else
            {
                 rulesJson = File.ReadAllText(rulesFileAddress);    
            }
            
            var rules = JsonConvert.DeserializeObject<List<RuleConfig>>(rulesJson);

            // Extract all unique TriggerApp values
            var monitoredApps = rules.Select(r => r.TriggerApp).Distinct().ToList();
            Log.Log($"Triggerer App(s): {string.Join(", ", monitoredApps)}");
            return monitoredApps;
        }

        public void UpdateMonitoredApps(List<string> latestApps)
        {
            lock (_monitoredApplications)
            {
                _monitoredApplications = latestApps;
            }
        }
    }
}