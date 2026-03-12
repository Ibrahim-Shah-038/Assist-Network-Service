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
            string pipeName = requestType == "GET_NODE_NAME"
                ? "AssistNodeNamePipe"
                : "AssistActivePresetPipe";

            int retryCount = 5;

            for (int i = 0; i < retryCount; i++)
            {
                NamedPipeClientStream pipeClient = null;

                try
                {
                    pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);

                    var connectTask = pipeClient.ConnectAsync(2000);
                    await connectTask;

                    var writer = new StreamWriter(pipeClient) { AutoFlush = true };
                    await writer.WriteLineAsync(requestType);

                    var reader = new StreamReader(pipeClient);
                    string response = await reader.ReadLineAsync();

                    ResetWarning();
                    return response ?? "Unknown";
                }
                catch
                {
                    // wait before retry
                    await Task.Delay(1000);
                }
                finally
                {
                    pipeClient?.Dispose();
                }
            }

            WarnOnce("Service not ready");
            return "Unknown";
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

        private void ResetWarning()
        {
            if (hasWarnedUser)
            {
                hasWarnedUser = false;
                form.HideNotificationSafe(); // You must implement this
            }
        }


    }
}
