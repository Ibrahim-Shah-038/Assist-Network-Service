using System;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace Assist_Service
{
    internal static class Program
    {
        static void Main()
        {
            // 🔴 GLOBAL EXCEPTION HANDLERS (MANDATORY FOR SERVICE STABILITY)

            // Handles non-UI thread crashes
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                try
                {
                    Exception ex = e.ExceptionObject as Exception;
                    LogFatal("UnhandledException", ex);
                }
                catch
                {
                    // Never allow logging failure to crash the process
                }
            };

            // Handles unobserved Task exceptions
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                try
                {
                    LogFatal("UnobservedTaskException", e.Exception);
                    e.SetObserved(); // prevents process termination
                }
                catch
                {
                }
            };

            // 🔵 ORIGINAL FUNCTIONALITY (UNCHANGED)
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new Service1()
            };
            ServiceBase.Run(ServicesToRun);
        }

        // 🔧 Minimal safe logger (replace with your existing logger if any)
        private static void LogFatal(string source, Exception ex)
        {
            try
            {
                System.IO.File.AppendAllText(
                    @"C:\Assist\logs\assist_fatal.log",
                    $"[{DateTime.Now}] {source}\n{ex}\n\n"
                );
            }
            catch
            {
                // Absolute last safety net
            }
        }
    }
}
