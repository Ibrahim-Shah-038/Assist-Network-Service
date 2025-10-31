using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Assist_TSR
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Prevent multiple instances
            bool createdNew;
            using (System.Threading.Mutex mutex = new System.Threading.Mutex(true, "Assist_TSR_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    // Application is already running, exit silently
                    return;
                }

                // Add to startup registry if not already there
                EnsureStartupEntry();

                // Wait for service to start (max 90 seconds)
                WaitForServiceToStart("Assist_Service", TimeSpan.FromSeconds(90));

                // Original code - unchanged
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Forms.Form1());

                GC.KeepAlive(mutex);
            }
        }

        private static void WaitForServiceToStart(string serviceName, TimeSpan timeout)
        {
            var startTime = DateTime.Now;

            while (DateTime.Now - startTime < timeout)
            {
                try
                {
                    using (ServiceController service = new ServiceController(serviceName))
                    {
                        service.Refresh();

                        if (service.Status == ServiceControllerStatus.Running)
                        {
                            // Service is running, wait 1 more second for initialization
                            Thread.Sleep(1000);
                            return;
                        }

                        // Service is starting or stopped, keep waiting
                        Thread.Sleep(1000);
                    }
                }
                catch
                {
                    // Service doesn't exist yet or can't be accessed, keep waiting
                    Thread.Sleep(1000);
                }
            }

            // Timeout reached - continue anyway (app will still work)
        }

        private static void EnsureStartupEntry()
        {
            try
            {
                string appPath = "\"" + Application.ExecutablePath + "\"";
                Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

                var currentValue = key.GetValue("Assist_TSR") as string;

                // Only update if not set or different
                if (currentValue != appPath)
                {
                    key.SetValue("Assist_TSR", appPath);
                }

                key.Close();
            }
            catch
            {
                // Ignore registry errors - app will still work
            }
        }
    }
}