using System;
using System.IO;
using System.Management;
using System.ServiceProcess;
using System.Windows.Forms;

namespace Assist_TSR.IPC_Handler
{
    public class Get_Rules_Path
    {
        private const string ServiceName = "Service1";
        private const string AddressFileName = "RulesAddress.txt"; // File containing the path

        public string GetRulesConfigFilePath()
        {
            try
            {
                // 1. Get Service1's installation path
                string servicePath = GetServiceExecutablePath();
                if (!string.IsNullOrEmpty(servicePath))
                {
                    string serviceDir = Path.GetDirectoryName(servicePath);
                    string addressFilePath = Path.Combine(serviceDir, AddressFileName);

                    // 2. Read the rules file path from RulesAddress.txt
                    if (File.Exists(addressFilePath))
                    {
                        string rulesPath = File.ReadAllText(addressFilePath).Trim();
                        if (File.Exists(rulesPath))
                        {
                            return rulesPath;
                        }
                    }
                }

                // 3. Fallback to default locations if service path not found
                string[] fallbackPaths =
                {
                    // Common application data directory
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Assist", "RulesConfig.json"),
                    
                    // Application startup directory
                    Path.Combine(Application.StartupPath, "RulesConfig.json"),
                    
                    // Development path
                    @"E:\Assist\Assist_Service\Assist_Service\bin\Debug\RulesConfig.json"
                };

                foreach (string path in fallbackPaths)
                {
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }

                // 4. Return default path if nothing found
                return fallbackPaths[1]; // Default to application directory
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error accessing rules file: {ex.Message}", "Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return Path.Combine(Application.StartupPath, "RulesConfig.json"); // Safest fallback
            }
        }

        private string GetServiceExecutablePath()
        {
            try
            {
                // Query WMI to get service executable path
                string query = $"SELECT PathName FROM Win32_Service WHERE Name = '{ServiceName}'";
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject service in searcher.Get())
                    {
                        string path = service["PathName"]?.ToString();
                        if (!string.IsNullOrEmpty(path))
                        {
                            // Remove quotes and arguments if present
                            path = path.Trim('"');
                            path = path.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0];
                            return path;
                        }
                    }
                }
            }
            catch
            {
                // Service not found or access denied
                return null;
            }
            return null;
        }
    }
}