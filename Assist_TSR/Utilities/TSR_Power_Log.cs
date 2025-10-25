using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Assist_TSR.Utilities
{
    public class TSR_Power_Log
    {
        public void PWR_Log(string message)
        {
            // Simple implementation - you can enhance this as needed
            System.Diagnostics.Debug.WriteLine(message);

            // Optional: Write to a log file
            string logPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "tsr_power_management.log");
            File.AppendAllText(logPath, $"{DateTime.Now}: {message}{Environment.NewLine}");
        }
    }
}
