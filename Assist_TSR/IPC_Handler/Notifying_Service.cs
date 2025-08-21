using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assist_TSR.Event_Handler;
using Assist_TSR.Utilities;

namespace Assist_TSR.IPC_Handler
{
    public class Notifying_Service
    {
        Logging loger;
        public void NotifyServiceAboutConfigChange(string newNodeName)
        {
            loger = new Logging();
            try
            {
                using (var pipeClient = new NamedPipeClientStream(".", "AssistNodeNamePipe", PipeDirection.InOut))
                {
                    pipeClient.Connect(3000); // 3 second timeout

                    var writer = new StreamWriter(pipeClient);
                    var reader = new StreamReader(pipeClient);

                    writer.WriteLine("UPDATE_NODE_NAME:" + newNodeName);
                    writer.Flush();

                    string response = reader.ReadLine();

                    if (response != "OK")
                    {
                        loger.Log($"Service responded with: {response}");
                    }
                }
            }
            catch (Exception ex)
            {
                loger.Log($"Error notifying service: {ex.Message}");
            }
        }
    }
}
