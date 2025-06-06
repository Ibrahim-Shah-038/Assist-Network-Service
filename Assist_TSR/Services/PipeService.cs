using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace Assist_TSR.Services
{
    public class PipeService
    {
        public void HandleLaunchRequests(Func<string, bool> launchHandler)
        {
            try
            {
                using (var pipeServer = new NamedPipeServerStream("LaunchHandlerPipe", PipeDirection.InOut))
                {
                    pipeServer.WaitForConnection();

                    using (var reader = new StreamReader(pipeServer))
                    using (var writer = new StreamWriter(pipeServer))
                    {
                        string request = reader.ReadLine();
                        Debug.WriteLine($"Received request: {request}");

                        if (request != null && request.StartsWith("LAUNCH:"))
                        {
                            string appName = request.Substring("LAUNCH:".Length);
                            writer.WriteLine(launchHandler(appName) ? "SUCCESS" : "ERROR: Failed to launch");
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
                Debug.WriteLine($"Error in Launch Handler: {ex.Message}");
            }
        }
    }
}