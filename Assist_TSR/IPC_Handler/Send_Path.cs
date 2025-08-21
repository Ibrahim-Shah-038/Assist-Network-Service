using System;
using System.IO.Pipes;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Assist_TSR.Utilities;
using System.Text;

namespace Assist_TSR.IPC_Handler
{
    public class Send_Path
    {
        private readonly Logging logger = new Logging();
        private const string PipeName = "CustomRulesConfigPipe";
        private const string PipeServerName = "CustomRulesPipe";
        private static string _latestPath = null;
        private static readonly object _lock = new object();
        private static bool _serverRunning = false;
        private static bool _isRunning = true;
        private string _currentPath = null;
        


        public Task SendPathToService(string path)
        {
            // Update the current path
            lock (_lock)
            {
                _currentPath = path;
            }

            // If server is already running, just update the path
            if (_serverRunning)
                return Task.CompletedTask;

            _serverRunning = true;

            return Task.Run(() =>
            {
                logger.Log("Starting Named Pipe Server...");

                while (true)
                {
                    try
                    {
                        using (var pipeServer = new NamedPipeServerStream(PipeServerName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                        {
                            logger.Log("Waiting for client connection...");
                            pipeServer.WaitForConnection();
                            logger.Log("Client connected.");

                            using (var reader = new StreamReader(pipeServer))
                            using (var writer = new StreamWriter(pipeServer) { AutoFlush = true })
                            {
                                string input = reader.ReadLine();
                                logger.Log($"Received input from client: {input}");

                                if (input == "GET_PATH")
                                {
                                    string pathToSend;
                                    lock (_lock)
                                    {
                                        pathToSend = _currentPath;
                                    }

                                    writer.WriteLine(pathToSend ?? "");
                                    pipeServer.WaitForPipeDrain();
                                    logger.Log($"Sent stored path to client: {pathToSend}");
                                }
                                else
                                {
                                    // Handle other commands if needed
                                    logger.Log($"Unknown command received: {input}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Log($"Pipe server error: {ex.Message}");
                        Thread.Sleep(1000); // Prevent tight loop on errors
                    }
                }
            });
        }
    }
}
