using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Threading;
using Assist_Service.Helpers;
using Assist_Service.Models;
using Assist_Service.Services;

namespace Assist_Service
{
    public partial class Service1 : ServiceBase
    {
        private NodeConfig _nodeConfig;
        private List<RuleConfig> _rules;
        private List<Peer> _peers = new List<Peer>();
        private readonly object _peersLock = new object();
        private bool _isRunning;

        private UdpClient _udpClient;
        private DiscoveryService _discoveryService;
        private ApplicationMonitorService _appMonitorService;
        private PipeCommunicationService _pipeService;
        private RuleProcessingService _ruleService;

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            _isRunning = true;

            try
            {
                // Load configurations
                _nodeConfig = FileHelper.ReadJsonWithRetry<NodeConfig>(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NodeConfig.json"));

                _rules = FileHelper.ReadJsonWithRetry<List<RuleConfig>>(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RulesConfig.json"));

                // Initialize UDP client
                _udpClient = new UdpClient(DiscoveryService.BroadcastPort);
                _udpClient.JoinMulticastGroup(DiscoveryService.MulticastAddress);

                // Initialize services
                _discoveryService = new DiscoveryService(_udpClient, _nodeConfig, _peers, _peersLock);
                _appMonitorService = new ApplicationMonitorService(
                    GetMonitoredApps(), _nodeConfig, _rules, _peers, _peersLock, _udpClient);
                _pipeService = new PipeCommunicationService(_nodeConfig, _peers, _peersLock, _rules);
                _ruleService = new RuleProcessingService();

                // Start services
                _discoveryService.Start();
                _appMonitorService.Start();
                _pipeService.Start();
            }
            catch (Exception ex)
            {
                // Log error
                throw;
            }
        }

        protected override void OnStop()
        {
            _isRunning = false;

            _discoveryService?.Stop();
            _appMonitorService?.Stop();
            _pipeService?.Stop();

            _udpClient?.DropMulticastGroup(DiscoveryService.MulticastAddress);
            _udpClient?.Close();
        }

        private List<string> GetMonitoredApps()
        {
            return new List<string> { "Code", "notepad", "devenv", "WINWORD", "vlc" };
        }
    }
}