using System;
using System.IO;
using System.Reflection;

namespace Assist_Service.Helpers
{
    public class Remote_Power_Log
    {
        private static readonly object _lock = new object();

        public void PWR_Log(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);

            string logPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "service_power_management.log");

            lock (_lock)
            {
                File.AppendAllText(
                    logPath,
                    $"{DateTime.Now}: {message}{Environment.NewLine}"
                );
            }
        }
    }
}