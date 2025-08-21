using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Assist_Service.Helpers;
using Assist_Service.Models;
using Assist_Service.Services;
using Newtonsoft.Json.Linq;

namespace Assist_Service.Services
{
    internal class ApplicationClosureService
    {
        private readonly string _pipeName = "AppClosurePipe";
        private bool _isRunning = true;
        private List<string> _monitoredApplications;
        private Dictionary<string, int> _previousProcessCounts = new Dictionary<string, int>();
        private List<RuleConfig> _rules;
        private readonly object _rulesLock;
        private readonly object _peersLock;
        private readonly List<Peer> _peers;
        private readonly NodeConfig _nodeConfig;
        private readonly UdpClient _udpClient = new UdpClient(12348);
        private Dictionary<string, int> _processCounts = new Dictionary<string, int>();
        private readonly HashSet<string> _runningApplications;
        private readonly Closure_Log closure_Log = new Closure_Log();
        private readonly object _runningAppsLock;
        private readonly object _monitoredAppsLock = new object();
        private Dictionary<string, int> _previousAppCounts = new Dictionary<string, int>();

        // Application Closures

        public ApplicationClosureService(
            List<string> monitoredApplications,
            List<RuleConfig> rules,
            object rulesLock,
            List<Peer> peers,
            object peersLock,
            NodeConfig nodeConfig,
            HashSet<string> runningApplications,
            object runningAppsLock)
        {
            _monitoredApplications = monitoredApplications;
            _rules = rules;
            _rulesLock = rulesLock;
            _peers = peers;
            _peersLock = peersLock;
            _nodeConfig = nodeConfig;
            _runningApplications = runningApplications;
            _runningAppsLock = runningAppsLock;
        }

        public void MonitorApplicationClosures()
        {
            closure_Log.Log("MonitorApplicationClosures thread started.");

            while (_isRunning)
            {
                try
                {
                    var processes = Process.GetProcesses();

                    var currentCounts = processes
                        .Where(p => _monitoredApplications.Any(m =>
                            string.Equals(m, p.ProcessName, StringComparison.OrdinalIgnoreCase)))
                        .GroupBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

                    closure_Log.Log("Monitored Applications: " + string.Join(", ", _monitoredApplications));
                    closure_Log.Log("Currently running monitored processes: " + string.Join(", ", currentCounts.Keys));

                    lock (_runningAppsLock)
                    {
                        // 🟢 Populate _runningApplications if empty
                        foreach (var app in currentCounts.Keys)
                        {
                            if (!_runningApplications.Contains(app, StringComparer.OrdinalIgnoreCase))
                            {
                                _runningApplications.Add(app);
                                closure_Log.Log($"Initial run: Added to _runningApplications: {app}");
                            }
                        }

                        // 🔴 Check if any previously running apps are now closed
                        foreach (var appName in _runningApplications.ToList())
                        {
                            if (!currentCounts.Keys.Any(k =>
                                string.Equals(k, appName, StringComparison.OrdinalIgnoreCase)))
                            {
                                _runningApplications.Remove(appName);
                                closure_Log.Log($"Application closed: {appName}");
                                HandleApplicationClosure(appName);
                            }
                        }
                    }

                    _processCounts = currentCounts;

                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    closure_Log.Log($"Error in MonitorApplicationClosures: {ex.Message}");
                }
            }

            closure_Log.Log("MonitorApplicationClosures thread stopped.");
        }

        public void HandleApplicationClosure(string closedAppName)
        {
            try
            {
                closure_Log.Log("Finding Rules");

                List<RuleConfig> applicableRules;

                // Lock to ensure thread safety when accessing _rules
                lock (_rulesLock)
                {
                    if (_rules == null)
                    {
                        closure_Log.Log("Rules list is null.");
                        return;
                    }

                    // Filter rules that match current node and closed application
                    applicableRules = _rules
                        .Where(r =>
                            r.SourceNode != null &&
                            r.TriggerApp != null &&
                            _nodeConfig?.NodeName != null &&
                            r.SourceNode.Equals(_nodeConfig.NodeName, StringComparison.OrdinalIgnoreCase) &&
                            r.TriggerApp.Equals(closedAppName, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                // Log the matched rules for debugging
                if (applicableRules.Count == 0)
                {
                    closure_Log.Log("No applicable rules found.");
                    return;
                }

                foreach (var rule in applicableRules)
                {
                    if (rule.TargetNodes == null || rule.TargetNodes.Count == 0)
                    {
                        closure_Log.Log("Rule has no TargetNodes.");
                        continue;
                    }

                    foreach (var target in rule.TargetNodes)
                    {
                        if (string.IsNullOrWhiteSpace(target.NodeName))
                        {
                            closure_Log.Log("Target NodeName is null or empty.");
                            continue;
                        }

                        // Lock while accessing shared _peers list
                        lock (_peersLock)
                        {
                            var peer = _peers.FirstOrDefault(p =>
                                p.NodeName.Equals(target.NodeName, StringComparison.OrdinalIgnoreCase));

                            if (peer != null)
                            {
                                closure_Log.Log($"Sending command to peer: {peer.NodeName} to close: {target.LaunchApp} {target.LaunchArguments}");
                                var fixedEndPoint = new IPEndPoint(peer.EndPoint.Address, 12348);
                                SendCloseCommand(target.LaunchApp, fixedEndPoint);
                                
                            }
                            else
                            {
                                closure_Log.Log($"Peer not found for node: {target.NodeName}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                closure_Log.Log("Error in HandleApplicationClosure: " + ex.Message);
                closure_Log.Log("StackTrace: " + ex.StackTrace);
            }
        }

        public void SendCloseCommand(string appName, IPEndPoint targetEndPoint)
        {
            try
            {
                //string token = SecurityHelper.GenerateToken("APP_CLOSE");
                string message = $"APP_CLOSE:{appName}";
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                _udpClient.Send(bytes, bytes.Length, targetEndPoint);
            }
            catch (Exception ex)
            {
                // Log error
            }
        }


        // UDP LISTENER FOR APPLICATION CLOSURE

        public void ListenForUdpClosureCommands()
        {
            try
            {
                while (_isRunning)
                {
                    IPEndPoint remoteEndPoint = null;
                    byte[] receivedBytes = _udpClient.Receive(ref remoteEndPoint);
                    string receivedMessage = Encoding.UTF8.GetString(receivedBytes);

                    closure_Log.Log($"Received message from {remoteEndPoint}: {receivedMessage}");

                    // Process the message
                    ProcessAppClosureMessage(receivedMessage, remoteEndPoint);
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
            {
                // Expected when closing the listener
            }
            catch (Exception ex)
            {
                closure_Log.Log($"Error in UDP listener: {ex.Message}");
            }
        }

        private void ProcessAppClosureMessage(string message, IPEndPoint sourceEndPoint)
        {
            // Expected format: "APP_CLOSE:appName:arguments:token"
            string[] parts = message.Split(':');
            if (parts.Length != 2 || parts[0] != "APP_CLOSE")
            {
                closure_Log.Log("Invalid message format");
                return;
            }

            string command = parts[0];
            string appName = parts[1];
            //string arguments = parts[2];
            //string token = parts[3];

            // Validate the token
            /*if (!SecurityHelper.ValidateToken(token, "APP_CLOSE"))
            {
                closure_Log.Log("Invalid token received");
                return;
            }*/

            // Forward the command to the named pipe server
            SendToAppCloseNamedPipe(command, appName);
        }

        public void SendToAppCloseNamedPipe(string command, string appName)
        {
            try
            {
                using (var pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out))
                {
                    // Try to connect within 5 seconds
                    pipeClient.Connect(5000);

                    if (pipeClient.IsConnected)
                    {
                        string message = $"{command}:{appName}";
                        byte[] bytes = Encoding.UTF8.GetBytes(message);
                        pipeClient.Write(bytes, 0, bytes.Length);
                        closure_Log.Log($"Sent to named pipe: {message}");
                    }
                    else
                    {
                        closure_Log.Log("Could not connect to named pipe server");
                    }
                }
            }
            catch (TimeoutException)
            {
                closure_Log.Log("Timeout while connecting to named pipe server");
            }
            catch (Exception ex)
            {
                closure_Log.Log($"Error communicating with named pipe: {ex.Message}");
            }
        }

        public void UpdateMonitoredApps(List<string> latestApps)
        {
            // Use a separate lock object instead of locking on the list itself
            lock (_monitoredAppsLock)
            {
                _monitoredApplications = latestApps;
            }
        }

    }
}
