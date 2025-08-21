using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Assist_TSR.IPC_Handler
{
    public class Get_Peers
    {
        public async Task<List<string>> GetPeersAsync()
        {
            var peers = new List<string>();

            using (var pipeClient = new NamedPipeClientStream(".", "AssistPeersPipe", PipeDirection.InOut))
            {
                await pipeClient.ConnectAsync();

                using (var reader = new StreamReader(pipeClient))
                using (var writer = new StreamWriter(pipeClient))
                {
                    // Send GET_PEERS request
                    await writer.WriteLineAsync("GET_PEERS");
                    await writer.FlushAsync();

                    // Read the response
                    string response = await reader.ReadLineAsync();
                    peers = JsonConvert.DeserializeObject<List<string>>(response);
                }
            }

            return peers;
        }
    }
}
