using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Assist_Service.Helpers
{
    public class Logging
    {
        public void Log(string message)
        {
            // Simple implementation - you can enhance this as needed
            System.Diagnostics.Debug.WriteLine(message);

            // Optional: Write to a log file
            string logPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "service.log");
            File.AppendAllText(logPath, $"{DateTime.Now}: {message}{Environment.NewLine}");
        }
    }
}
