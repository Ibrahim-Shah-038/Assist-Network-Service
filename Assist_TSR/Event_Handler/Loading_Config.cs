using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assist_TSR.Forms;
using Newtonsoft.Json;
using System.Windows.Forms;
using Assist_TSR.Classes;

namespace Assist_TSR.Event_Handler
{
    public class Loading_Config
    {
        // Method to load and return the config
        public List<NodeConfig> LoadConfig()
        {
            // List of possible config file locations
            List<string> possibleConfigPaths = new List<string>
            {
                @"E:\Assist\Assist_Service\Assist_Service\bin\Debug\RulesConfig.json",
                Path.Combine(Application.StartupPath, "RulesConfig.json"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Assist", "RulesConfig.json")
            };

            string configPath = null;

            // Find first existing config file
            foreach (var path in possibleConfigPaths)
            {
                if (File.Exists(path))
                {
                    configPath = path;
                    break;
                }
            }

            if (configPath == null)
            {
                throw new FileNotFoundException(
                    $"Config file not found at any of these locations:\n" +
                    $"- {possibleConfigPaths[0]}\n" +
                    $"- {possibleConfigPaths[1]}\n" +
                    $"- {possibleConfigPaths[2]}"
                );
            }

            // Load and parse the JSON
            string json = File.ReadAllText(configPath);
            var config = JsonConvert.DeserializeObject<List<NodeConfig>>(json);

            return config;
        }
    }
}
