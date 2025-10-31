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

                // Original code - unchanged
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Create and show the form first (so system tray icon appears)
                Forms.Form1 mainForm = new Forms.Form1();

                // Wait for service in a background thread so UI remains responsive
                Thread serviceWaitThread = new Thread(() => {
                    WaitForServiceToStart("Assist_Service", TimeSpan.FromSeconds(90));

                    // After service starts, show notification
                    mainForm.Invoke((MethodInvoker)delegate {
                        // You can add a notification here if needed
                        // mainForm.notifyIcon.ShowBalloonTip(2000, "Assist TSR", "Service connected.", ToolTipIcon.Info);
                    });
                });
                serviceWaitThread.IsBackground = true;
                serviceWaitThread.Start();

                // Run the application (form will show system tray icon immediately)
                Application.Run(mainForm);

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