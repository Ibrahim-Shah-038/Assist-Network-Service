using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using Assist_TSR.Event_Handler;
using Assist_TSR.Forms;
using System.Threading;
using Assist_TSR.Utilities;

namespace Assist_TSR.IPC_Handler
{
    public class Server
    {
        private Form1 form;
        private Clouser_App _closureApp; // Only new field added
        private readonly Logging logger = new Logging();

        public Server(Form1 formInstance)
        {
            form = formInstance;
            _closureApp = new Clouser_App(); // Only new initialization
        }

        // YOUR EXISTING CODE - COMPLETELY UNCHANGED
        public void StartLaunchServer()
        {
            while (form.isRunning)
            {
                try
                {
                    using (NamedPipeServerStream pipeServer = new NamedPipeServerStream("LaunchHandlerPipe", PipeDirection.InOut))
                    {
                        logger.Log("Waiting for launch request...");
                        pipeServer.WaitForConnection();
                        logger.Log("Launch request received.");

                        using (StreamReader reader = new StreamReader(pipeServer))
                        using (StreamWriter writer = new StreamWriter(pipeServer))
                        {
                            string request = reader.ReadLine();
                            logger.Log($"Received request: {request}");

                            if (request != null && request.StartsWith("LAUNCH:"))
                            {
                                string appName = request.Substring("LAUNCH:".Length);

                                launcher launch_app = new launcher();

                                if (launch_app.LaunchApplication(appName))
                                {
                                    writer.WriteLine("SUCCESS");
                                    logger.Log($"Successfully launched: {appName}");
                                }
                                else
                                {
                                    writer.WriteLine("ERROR: Failed to launch");
                                    logger.Log($"Failed to launch: {appName}");
                                }

                                writer.Flush();
                            }
                            else
                            {
                                writer.WriteLine("ERROR: Invalid request");
                                writer.Flush();
                                logger.Log("Invalid request received.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Log($"Error in Launch Handler: {ex.Message}");
                }
            }
        }

        // NEW CODE ADDED BELOW - YOUR CLOSURE SERVER
        public void StartClosureServer()
        {
            while (form.isRunning)
            {
                try
                {
                    using (NamedPipeServerStream pipeServer = new NamedPipeServerStream(
                        "ClosureHandlerPipe", 
                        PipeDirection.InOut))
                    {
                        logger.Log("[Closure] Waiting for closure request...");
                        pipeServer.WaitForConnection();
                        logger.Log("[Closure] Request received.");

                        using (StreamReader reader = new StreamReader(pipeServer))
                        using (StreamWriter writer = new StreamWriter(pipeServer))
                        {
                            string request = reader.ReadLine();
                            logger.Log($"[Closure] Received: {request}");

                            if (request != null && request.StartsWith("CLOSURE:"))
                            {
                                string appName = request.Substring("CLOSURE:".Length).Trim();

                                if (_closureApp.CloseApplication(appName))
                                {
                                    writer.WriteLine("SUCCESS");
                                    logger.Log($"[Closure] Closed: {appName}");
                                }
                                else
                                {
                                    writer.WriteLine("ERROR: Close failed");
                                    logger.Log($"[Closure] Failed to close: {appName}");
                                }
                            }
                            else
                            {
                                writer.WriteLine("ERROR: Invalid closure request");
                                logger.Log("[Closure] Invalid request format.");
                            }

                            writer.Flush();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Closure Error] {ex.Message}");
                }
            }
        }
    }
}