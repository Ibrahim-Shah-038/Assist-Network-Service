using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assist_TSR.Forms;
using System.Windows.Forms;
using Assist_TSR.Utilities;

namespace Assist_TSR.IPC_Handler
{
    public class Request_Data
    {
        private static bool hasWarnedUser = false;
        private Form1 form;
        private Logging Logger = new Logging();

        public Request_Data(Form1 formInstance)
        {
            form = formInstance;
        }

        

        // GETTING DATA FOR GENERAL TAB FROM SERVICE
        public async Task<string> RequestDataFromServiceAsync(string requestType)
        {
            NamedPipeClientStream pipeClient = null;
            try
            {
                string pipeName = requestType == "GET_NODE_NAME"
                    ? "AssistNodeNamePipe"
                    : "AssistActivePresetPipe";

                pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);

                // Connect with timeout (same as synchronous version)
                var connectTask = pipeClient.ConnectAsync();
                var timeoutTask = Task.Delay(1000);
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    throw new System.TimeoutException("Service connection timeout");
                }
                await connectTask; // This will throw if there was a connection error

                // Write request
                var writer = new StreamWriter(pipeClient) { AutoFlush = true };
                await writer.WriteLineAsync(requestType);

                // Read response
                var reader = new StreamReader(pipeClient);
                string response = await reader.ReadLineAsync();

                return response ?? "Unknown";
            }
            catch (System.TimeoutException)
            {
                WarnOnce("Service connection timeout");
                return "Unknown";
            }
            catch (Exception ex)
            {
                WarnOnce($"Service communication failed: {ex.Message}");
                return "Unknown";
            }
            finally
            {
                pipeClient?.Dispose();
            }
        }

        public void WarnOnce(string message)
        {
            if (!hasWarnedUser)
            {
                form.ShowNotificationSafe(message);
                hasWarnedUser = true;
            }
        }

        public async Task<string> FetchDataAsync(string requestType)
        {
            await Task.Delay(100); // Let server initialize
            return await RequestDataFromServiceAsync(requestType);
        }

    }
}
