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
using Assist_Service.Services;
using Newtonsoft.Json;
using System.Data;

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
        private static string _storedPath; // In-memory storage
        private static bool _serverStarted = false;
        private static Logging Logger = new Logging();
        private string _activeConfigPath;
        private readonly object _pathLock = new object();

        private NamedPipeServerStream _server;
        private readonly string _pipeName = "CustomRulesConfigPipe";
        private CancellationTokenSource _cancellationTokenSource;
        private const string AddressFileName = "RulesAddress.txt";

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
            new Thread(StartClosurePipeServer).Start();
            new Thread(StartPeersPipeServer).Start();
            new Thread(StartNodeNamePipeServer).Start();
            new Thread(StartActivePresetPipeServer).Start();

            _cancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => RunServer(_cancellationTokenSource.Token));
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

        private void StartClosurePipeServer()
        {
            while (_isRunning)
            {
                try
                {
                    using (var pipeServer = new NamedPipeServerStream(
                        "AppClosurePipe",  // Different pipe name for closures
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

                            if (request.StartsWith("APP_CLOSE:"))
                            {
                                string appName = request.Substring("APP_CLOSE:".Length);
                                bool success = ForwardClosureNotification(appName);
                                writer.WriteLine(success ? "SUCCESS" : "ERROR: Unable to process closure notification");
                                writer.Flush();
                            }
                            else
                            {
                                writer.WriteLine("ERROR: Invalid closure request");
                                writer.Flush();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error: $"Pipe server error: {ex.Message}"
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

        private async Task RunServer(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message);
                    await _server.WaitForConnectionAsync(cancellationToken);

                    using (var reader = new StreamReader(_server))
                    using (var writer = new StreamWriter(_server))
                    {
                        // Read the path from the client
                        string path = await reader.ReadLineAsync();

                        // Simply echo back the same path (you could add validation here)
                        await writer.WriteLineAsync(path);
                        await writer.FlushAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Server was stopped
                    break;
                }
                catch (Exception ex)
                {
                    // Log error and restart
                    Console.WriteLine($"CustomRulesServer error: {ex.Message}");
                    Thread.Sleep(1000); // Wait before restarting
                }
                finally
                {
                    _server?.Dispose();
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

                            while (true)
                            {
                                if (request == "GET_ACTIVE_PRESET")
                                {
                                    string configPath = GetActiveConfigPath();
                                    string configFileName = Path.GetFileName(configPath);
                                    Logger.Log($"The file path is returned to the server and returning to client: {configFileName}");
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
                }
                catch (Exception ex)
                {
                    // Log error
                }
            }
        }

        public string GetActiveConfigPath()
        {
            string selectedPath;
            Logger.Log("Attempting to get configuration path from CustomRulesConfigPipe...");

            // First try to get path from Custom_Rules_Server
            string customPathFromServer = TryGetPathFromCustomRulesServer();

            if (!string.IsNullOrEmpty(customPathFromServer))
            {
                if (File.Exists(customPathFromServer))
                {
                    Logger.Log($"Using config path from server: {customPathFromServer}");
                    UpdateRulesAddressFile(customPathFromServer); // Update with new address
                    return customPathFromServer;
                }
                else
                {
                    Logger.Log($"Path received from server does not exist: {customPathFromServer}");
                }
            }
            else
            {
                Logger.Log("No valid path received from server. Falling back to default path.");
            }

            // 1. First check the default dev/debug path
            string defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RulesConfig.json");
            if (File.Exists(defaultPath))
            {
                selectedPath = defaultPath;
                Logger.Log($"Using default config path: {selectedPath}");
            }
            else
            {
                // 2. Try the actual path from where the service is running
                string servicePath = Path.Combine(
                    Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "RulesConfig.json"
                );

                if (File.Exists(servicePath))
                {
                    selectedPath = servicePath;
                    Logger.Log($"Using service executable path: {selectedPath}");
                }
                else
                {
                    // 3. If file doesn't exist in either place, create at service path
                    selectedPath = servicePath;
                    Logger.Log("RulesConfig.json not found. Creating at service path.");

                    Directory.CreateDirectory(Path.GetDirectoryName(servicePath));

                    var defaultRules = new List<Rule>(); // or default values if needed
                    string json = JsonConvert.SerializeObject(defaultRules, Formatting.Indented);
                    File.WriteAllText(servicePath, json);
                }
            }

            // ✅ NOW call the update function after the final path is chosen
            UpdateRulesAddressFile(selectedPath);

            return selectedPath;

            
        }

        private void UpdateRulesAddressFile(string currentRulesPath)
        {
            try
            {
                string addressFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AddressFileName);
                File.WriteAllText(addressFilePath, currentRulesPath);
                Logger.Log($"Updated rules address file with path: {currentRulesPath}");
            }
            catch (Exception ex)
            {
                Logger.Log($"Error updating rules address file: {ex.Message}");
            }
        }

        public string ReadRulesPathFromAddressFile()
        {
            try
            {
                string addressFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AddressFileName);

                if (File.Exists(addressFilePath))
                {
                    string path = File.ReadAllText(addressFilePath).Trim();
                    Logger.Log($"Read rule path from address file: {path}");
                    return path;
                }
                else
                {
                    string def_path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RuleConfig.json");
                    Logger.Log($"Rules address file not found: {addressFilePath} executing default rules path");
                    return def_path;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error reading rules address file: {ex.Message}");
                return null;
            }
        }

        public string GetLastKnownRulesAddress()
        {
            string addressFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AddressFileName);
            if (File.Exists(addressFilePath))
            {
                return File.ReadAllText(addressFilePath);
            }
            return null;
        }

        private string TryGetPathFromCustomRulesServer()
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", "CustomRulesPipe", PipeDirection.InOut))
                {
                    client.Connect(1000);
                    Logger.Log("Connected to pipe server.");

                    using (var reader = new StreamReader(client))
                    using (var writer = new StreamWriter(client) { AutoFlush = true })
                    {
                        // Initial request
                        Logger.Log("Sending request to server: GET_PATH");
                        writer.WriteLine("GET_PATH");

                        while (client.IsConnected)
                        {
                            string response = reader.ReadLine();
                            if (string.IsNullOrWhiteSpace(response))
                            {
                                // Server closed connection or sent empty response
                                break;
                            }

                            Logger.Log($"Received response from server: {response}");
                            return response; // Or process it as needed
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to connect or read response: {ex.Message}");
            }

            Logger.Log("No valid path received from server. Falling back to default path.");
            return null;
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

        private bool ForwardClosureNotification(string appName)
        {
            try
            {
                using (var pipeClient = new NamedPipeClientStream(".", "ClosureHandlerPipe", PipeDirection.InOut))
                {
                    pipeClient.Connect(2000);
                    using (var writer = new StreamWriter(pipeClient))
                    using (var reader = new StreamReader(pipeClient))
                    {
                        writer.WriteLine($"CLOSURE:{appName}");
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