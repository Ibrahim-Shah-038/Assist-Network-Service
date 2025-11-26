using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Assist_TSR.Forms;

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

                // Enable visual styles
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // ----------------------------
                // Show LoginForm first
                // ----------------------------
                /*LoginForm login = new LoginForm();
                if (login.ShowDialog() != DialogResult.OK)
                {
                    // Exit if login cancelled or failed
                    return;
                }*/

                // ----------------------------
                // Run TSR main form
                // ----------------------------
                Forms.Form1 mainForm = new Forms.Form1();

                // Wait for service in a background thread so UI remains responsive
                Thread serviceWaitThread = new Thread(() =>
                {
                    WaitForServiceToStart("Assist_Service", TimeSpan.FromSeconds(90));

                    mainForm.Invoke((MethodInvoker)delegate
                    {
                        // Optional: show notification after service starts
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
                            Thread.Sleep(1000); // extra 1 sec
                            return;
                        }

                        Thread.Sleep(1000);
                    }
                }
                catch
                {
                    Thread.Sleep(1000);
                }
            }
        }

        private static void EnsureStartupEntry()
        {
            try
            {
                string appPath = "\"" + Application.ExecutablePath + "\"";
                Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

                var currentValue = key.GetValue("Assist_TSR") as string;
                if (currentValue != appPath)
                    key.SetValue("Assist_TSR", appPath);

                key.Close();
            }
            catch
            {
                // Ignore registry errors
            }
        }

        // ----------------------------
        // LOGIN VALIDATION HELPER
        // ----------------------------
        public static bool ValidateLogin(string usernameInput, string passwordInput)
        {
            try
            {
                string folderPath = @"C:\ProgramData\Assist\Auth";
                string filePath = System.IO.Path.Combine(folderPath, "credentials.bin");

                if (!System.IO.File.Exists(filePath))
                    return false;

                string savedUsername, savedPasswordEncrypted;

                // Read encrypted credentials
                using (var fs = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                using (var br = new System.IO.BinaryReader(fs))
                {
                    savedUsername = br.ReadString();
                    savedPasswordEncrypted = br.ReadString();
                }

                // Decrypt password using DPAPI
                byte[] encryptedBytes = Convert.FromBase64String(savedPasswordEncrypted);
                byte[] decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                string savedPassword = Encoding.UTF8.GetString(decryptedBytes);

                return usernameInput == savedUsername && passwordInput == savedPassword;
            }
            catch
            {
                return false;
            }
        }
    }
}
