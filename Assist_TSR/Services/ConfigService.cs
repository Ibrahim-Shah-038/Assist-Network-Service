using Assist_TSR.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace Assist_TSR.Services
{
    public class ConfigService
    {
        private List<NodeConfig> _config;

        public void LoadConfig()
        {
            string configPath = GetConfigFilePath();
            string json = File.ReadAllText(configPath);
            _config = JsonConvert.DeserializeObject<List<NodeConfig>>(json);
        }

        public List<NodeConfig> GetConfig() => _config;

        private string GetConfigFilePath()
        {
            List<string> possiblePaths = new List<string>
            {
                @"E:\Assist\Assist_Service\Assist_Service\bin\Debug\RulesConfig.json",
                Path.Combine(Application.StartupPath, "RulesConfig.json"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Assist", "RulesConfig.json")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path)) return path;
            }

            throw new FileNotFoundException("Config file not found in standard locations");
        }

        public string GetRulesConfigFilePath()
        {
            // Same implementation you had in RuleService.GetRulesConfigFilePath()
            List<string> possibleConfigPaths = new List<string>
    {
        @"E:\Assist\Assist_Service\Assist_Service\bin\Debug\RulesConfig.json",
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RulesConfig.json"),
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Assist",
            "RulesConfig.json"
        )
    };

            foreach (var path in possibleConfigPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            string defaultPath = possibleConfigPaths[2];
            Directory.CreateDirectory(Path.GetDirectoryName(defaultPath));
            File.WriteAllText(defaultPath, "[]");
            return defaultPath;
        }

    }
}