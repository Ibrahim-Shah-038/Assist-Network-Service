using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assist_TSR.Forms;
using Newtonsoft.Json;
using Assist_TSR.Classes;

namespace Assist_TSR.IPC_Handler
{
    public class Get_List_Rules
    {
        private const string PipeName = "CustomRulesConfigPipe";

        // Get config from service via named pipe
        public async Task<List<RuleConfig>> GetConfigFromService()
        {
            return await Task.Run(() =>
            {
                using (var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
                {
                    pipeClient.Connect(5000); // 5 second timeout
                    using (var writer = new StreamWriter(pipeClient))
                    using (var reader = new StreamReader(pipeClient))
                    {
                        writer.Write("GET_CONFIG");
                        writer.Flush();
                        string json = reader.ReadToEnd();
                        return JsonConvert.DeserializeObject<List<RuleConfig>>(json);
                    }
                }
            });
        }
    }
}
