using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Assist_Service.Helpers;
using Assist_Service.Models;

namespace Assist_Service.Services
{
    public class ApplicationMonitorService
    {
        private readonly List<string> _monitoredApplications;
        private readonly List<string> _runningApplications = new List<string>();
        private readonly NodeConfig _nodeConfig;
        private readonly List<RuleConfig> _rules;
        private readonly List<Peer> _peers;
        private readonly object _peersLock;
        private readonly UdpClient _udpClient;
        private bool _isRunning;

        public ApplicationMonitorService(
            List<string> monitoredApplications,
            NodeConfig nodeConfig,
            List<RuleConfig> rules,
            List<Peer> peers,
            object peersLock,
            UdpClient udpClient)
        {
            _monitoredApplications = monitoredApplications;
            _nodeConfig = nodeConfig;
            _rules = rules;
            _peers = peers;
            _peersLock = peersLock;
            _udpClient = udpClient;
        }

        public void Start()
        {
            _isRunning = true;
            Thread monitorThread = new Thread(MonitorApplicationLaunches);
            monitorThread.IsBackground = true;
            monitorThread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
        }

        private void MonitorApplicationLaunches()
        {
            while (_isRunning)
            {
                try
                {
                    var processes = Process.GetProcesses();
                    foreach (var process in processes)
                    {
                        string appName = process.ProcessName;
                        if (_monitoredApplications.Contains(appName) && !_runningApplications.Contains(appName))
                        {
                            _runningApplications.Add(appName);
                            HandleApplicationTrigger(appName);
                        }
                    }
                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    // Log error
                }
            }
        }

        private void HandleApplicationTrigger(string appName)
        {
            var applicableRules = _rules.Where(r =>
                r.SourceNode.Equals(_nodeConfig.NodeName, StringComparison.OrdinalIgnoreCase) &&
                r.TriggerApp.Equals(appName, StringComparison.OrdinalIgnoreCase))
                .ToList();

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
                            SendLaunchCommand(target.LaunchApp, peer.EndPoint);
                        }
                    }
                }
            }
        }

        private void SendLaunchCommand(string appName, IPEndPoint targetEndPoint)
        {
            try
            {
                string token = SecurityHelper.GenerateToken("APP_LAUNCH");
                string message = $"APP_LAUNCH:{appName}:{token}";
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                _udpClient.Send(bytes, bytes.Length, targetEndPoint);
            }
            catch (Exception ex)
            {
                // Log error
            }
        }
    }
}