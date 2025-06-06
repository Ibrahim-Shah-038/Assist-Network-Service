using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assist_Service.Helpers;
using Assist_Service.Models;

namespace Assist_Service.Services
{
    public class PipeCommunicationService
    {
        private readonly NodeConfig _nodeConfig;
        private readonly List<Peer> _peers;
        private readonly object _peersLock;
        private readonly List<RuleConfig> _rules;
        private bool _isRunning;
        private const string PipeName = "AssistServicePipe";
        private const string PeersPipeName = "AssistPeersPipe";
        private const string NodeNamePipeName = "AssistNodeNamePipe";
        private const string ActivePresetPipeName = "AssistActivePresetPipe";

        public PipeCommunicationService(
            NodeConfig nodeConfig,
            List<Peer> peers,
            object peersLock,
            List<RuleConfig> rules)
        {
            _nodeConfig = nodeConfig;
            _peers = peers;
            _peersLock = peersLock;
            _rules = rules;
        }

        public void Start()
        {
            _isRunning = true;
            new Thread(StartMainPipeServer).Start();
            new Thread(StartPeersPipeServer).Start();
            new Thread(StartNodeNamePipeServer).Start();
            new Thread(StartActivePresetPipeServer).Start();
        }

        public void Stop()
        {
            _isRunning = false;
        }

        private void StartMainPipeServer()
        {
            while (_isRunning)
            {
                try
                {
                    using (var pipeServer = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.None,
                        4096,
                        4096,
                        CreatePipeSecurity()))
                    {
                        pipeServer.WaitForConnection();

                        using (var reader = new StreamReader(pipeServer))
                        using (var writer = new StreamWriter(pipeServer))
                        {
                            string request = reader.ReadLine();

                            if (request.StartsWith("LAUNCH:"))
                            {
                                string appName = request.Substring("LAUNCH:".Length);
                                bool success = ForwardLaunchRequest(appName);
                                writer.WriteLine(success ? "SUCCESS" : "ERROR: Unable to forward launch request");
                                writer.Flush();
                            }
                            else
                            {
                                writer.WriteLine("ERROR: Invalid request");
                                writer.Flush();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error
                }
            }
        }

        private void StartPeersPipeServer()
        {
            while (_isRunning)
            {
                try
                {
                    using (var pipeServer = new NamedPipeServerStream(
                        PeersPipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.None,
                        4096,
                        4096,
                        CreatePipeSecurity()))
                    {
                        pipeServer.WaitForConnection();

                        using (var reader = new StreamReader(pipeServer))
                        using (var writer = new StreamWriter(pipeServer))
                        {
                            string request = reader.ReadLine();

                            if (request == "GET_PEERS")
                            {
                                lock (_peersLock)
                                {
                                    var peersList = _peers.Select(p => p.NodeName).ToList();
                                    string peersJson = Newtonsoft.Json.JsonConvert.SerializeObject(peersList);
                                    writer.WriteLine(peersJson);
                                    writer.Flush();
                                }
                            }
                            else
                            {
                                writer.WriteLine("ERROR: Invalid request");
                                writer.Flush();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error
                }
            }
        }

        private void StartNodeNamePipeServer()
        {
            while (_isRunning)
            {
                NamedPipeServerStream pipeServer = null;
                try
                {
                    pipeServer = new NamedPipeServerStream(
                        NodeNamePipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.None,
                        4096,
                        4096,
                        CreatePipeSecurity());

                    pipeServer.WaitForConnection();

                    using (var reader = new StreamReader(pipeServer))
                    using (var writer = new StreamWriter(pipeServer))
                    {
                        string request = reader.ReadLine();

                        if (request == "GET_NODE_NAME")
                        {
                            writer.WriteLine(_nodeConfig?.NodeName ?? "Unknown");
                            writer.Flush();
                        }
                        else if (request?.StartsWith("UPDATE_NODE_NAME:") == true)
                        {
                            string newNodeName = request.Substring("UPDATE_NODE_NAME:".Length);
                            UpdateNodeConfig(newNodeName);
                            writer.WriteLine("OK");
                            writer.Flush();
                        }
                        else
                        {
                            writer.WriteLine("ERROR: Invalid request");
                            writer.Flush();
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error
                }
                finally
                {
                    pipeServer?.Dispose();
                }
            }
        }

        private void StartActivePresetPipeServer()
        {
            while (_isRunning)
            {
                try
                {
                    using (var pipeServer = new NamedPipeServerStream(
                        ActivePresetPipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.None,
                        4096,
                        4096,
                        CreatePipeSecurity()))
                    {
                        pipeServer.WaitForConnection();

                        using (var reader = new StreamReader(pipeServer))
                        using (var writer = new StreamWriter(pipeServer))
                        {
                            string request = reader.ReadLine();

                            if (request == "GET_ACTIVE_PRESET")
                            {
                                string configPath = GetActiveConfigPath();
                                string configFileName = Path.GetFileName(configPath);
                                writer.WriteLine(configFileName);
                                writer.Flush();
                            }
                            else
                            {
                                writer.WriteLine("ERROR: Invalid request");
                                writer.Flush();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error
                }
            }
        }

        private string GetActiveConfigPath()
        {
            string customPath = GetStoredCustomPath();
            return !string.IsNullOrEmpty(customPath) && File.Exists(customPath) ?
                customPath :
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RulesConfig.json");
        }

        private string GetStoredCustomPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CustomConfigPath.txt");
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }

        private void UpdateNodeConfig(string newNodeName)
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NodeConfig.json");
            var config = new NodeConfig { NodeName = newNodeName };
            FileHelper.WriteJsonWithRetry(configPath, config);
            _nodeConfig.NodeName = newNodeName;
        }

        private bool ForwardLaunchRequest(string appName)
        {
            try
            {
                using (var pipeClient = new NamedPipeClientStream(".", "LaunchHandlerPipe", PipeDirection.InOut))
                {
                    pipeClient.Connect(2000);
                    using (var writer = new StreamWriter(pipeClient))
                    using (var reader = new StreamReader(pipeClient))
                    {
                        writer.WriteLine($"LAUNCH:{appName}");
                        writer.Flush();
                        string response = reader.ReadLine();
                        return response == "SUCCESS";
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private PipeSecurity CreatePipeSecurity()
        {
            var pipeSecurity = new PipeSecurity();
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                PipeAccessRights.ReadWrite,
                AccessControlType.Allow));
            return pipeSecurity;
        }
    }
}