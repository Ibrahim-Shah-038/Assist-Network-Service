using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Assist_TSR.Forms;
using Assist_TSR.Classes;

namespace Assist_TSR.IPC_Handler
{
    public class Get_File_Path
    {
        public string GetConfigFilePath()
        {
            // List of possible config file locations (order determines priority)
            List<string> possibleConfigPaths = new List<string>
    {
        // 1. First check development path (only on dev machine)
        @"E:\Assist\Assist_Service\Assist_Service\bin\Debug\NodeConfig.json",
        
        // 2. Check application startup directory
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NodeConfig.json"),
        
        // 3. Check common application data directory
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Assist",
            "NodeConfig.json"
        ),
        
        // 4. Fallback to executable directory (for service)
        Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "NodeConfig.json"
        )
    };

            string configPath = null;

            // Find the first existing config file
            foreach (var path in possibleConfigPaths)
            {
                if (File.Exists(path))
                {
                    configPath = path;
                    break;
                }
            }

            // If no existing file found, determine where we should create it
            if (configPath == null)
            {
                // Choose where to create new config file based on environment
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    // Development environment - use debug folder
                    configPath = possibleConfigPaths[0];
                }
                else if (Environment.UserInteractive)
                {
                    // Running as application - use application data folder
                    configPath = possibleConfigPaths[2];

                    // Ensure directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                }
                else
                {
                    // Running as service - use executable directory
                    configPath = possibleConfigPaths[3];
                }

                // Create default config file
                //var defaultConfig = new NodeConfig { NodeName = "DefaultNode" };
                //FileHelper.WriteJsonWithRetry(configPath, defaultConfig);
            }

            return configPath;
        }
    }
}
