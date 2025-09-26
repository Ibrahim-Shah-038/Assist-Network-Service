using System;
using System.IO;
using System.Reflection;

namespace Assist_TSR.Utilities
{
    internal class TSR_Logging
    {
        private readonly string logPath;

        public TSR_Logging()
        {
            // Ensure logs folder exists in the same directory as the EXE
            string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                             ?? AppDomain.CurrentDomain.BaseDirectory;

            string logDir = Path.Combine(baseDir, "Logs");
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            logPath = Path.Combine(logDir, "TSR_Debug.log");
        }

        public void Log(string message)
        {
            try
            {
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {message}{Environment.NewLine}";
                File.AppendAllText(logPath, logEntry);

                // Still print to Debug window
                System.Diagnostics.Debug.WriteLine(message);
            }
            catch (Exception ex)
            {
                // Fallback in case logging itself fails
                Console.WriteLine($"[TSR_LOG ERROR] {ex.Message}");
            }
        }
    }
}
