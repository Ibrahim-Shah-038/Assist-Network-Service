using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace Assist_TSR.Services
{
    public class NodeService
    {
        public async Task<string> GetNodeNameAsync()
        {
            return await RequestDataFromServiceAsync("GET_NODE_NAME");
        }

        public async Task<string> GetActivePresetAsync()
        {
            return await RequestDataFromServiceAsync("GET_ACTIVE_PRESET");
        }

        public async Task<List<string>> GetPeersAsync()
        {
            try
            {
                using (var pipeClient = new NamedPipeClientStream(".", "AssistPeersPipe", PipeDirection.InOut))
                {
                    await pipeClient.ConnectAsync();

                    using (var reader = new StreamReader(pipeClient))
                    using (var writer = new StreamWriter(pipeClient))
                    {
                        await writer.WriteLineAsync("GET_PEERS");
                        await writer.FlushAsync();

                        string response = await reader.ReadLineAsync();
                        return Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(response);
                    }
                }
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        public ServiceControllerStatus GetServiceStatus(string serviceName)
        {
            try
            {
                using (ServiceController sc = new ServiceController(serviceName))
                {
                    return sc.Status;
                }
            }
            catch
            {
                throw;
            }
        }

        private async Task<string> RequestDataFromServiceAsync(string requestType)
        {
            NamedPipeClientStream pipeClient = null;
            try
            {
                string pipeName = requestType == "GET_NODE_NAME"
                    ? "AssistNodeNamePipe"
                    : "AssistActivePresetPipe";

                pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);

                await pipeClient.ConnectAsync(3000);

                var writer = new StreamWriter(pipeClient) { AutoFlush = true };
                await writer.WriteLineAsync(requestType);

                var reader = new StreamReader(pipeClient);
                return await reader.ReadLineAsync() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
            finally
            {
                pipeClient?.Dispose();
            }
        }
    }
}