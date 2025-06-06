using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
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

            // Start all pipe servers in separate threads
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
                        "AssistServicePipe",
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
                                bool success = ForwardLaunchRequestToConsoleApp(appName);
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

        // Implement other pipe server methods similarly...
        private void StartPeersPipeServer() { /* ... */ }
        private void StartNodeNamePipeServer() { /* ... */ }
        private void StartActivePresetPipeServer() { /* ... */ }

        private PipeSecurity CreatePipeSecurity()
        {
            PipeSecurity pipeSecurity = new PipeSecurity();
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                PipeAccessRights.ReadWrite,
                AccessControlType.Allow));
            return pipeSecurity;
        }

        private bool ForwardLaunchRequestToConsoleApp(string appName)
        {
            // Implementation...
            return false;
        }
    }
}