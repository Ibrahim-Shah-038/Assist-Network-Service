using System;
using System.ServiceProcess;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Assist_TSR.Forms;
using System.IO;
using System.Diagnostics;

namespace Assist_TSR
{
    internal static class Program
    {
        private const string TaskName = "ASSIST_TSR_AutoStart";

        [STAThread]
        static void Main()
        {
            // ----------------------------
            // SINGLE INSTANCE PROTECTION
            // ----------------------------
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Assist_TSR_SingleInstance", out createdNew))
            {
                if (!createdNew)
                    return;

                // ----------------------------
                // REGISTER TASK SCHEDULER (ONCE)
                // ----------------------------
                if (!TaskExists(TaskName))
                {
                    RegisterTSRTask();
                    return; // exit after registering task
                }

                // ----------------------------
                // NORMAL TSR STARTUP
                // ----------------------------
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Form1 mainForm = new Form1();

                // ----------------------------
                // WAIT FOR SERVICE IN BACKGROUND
                // ----------------------------
                Thread serviceWaitThread = new Thread(() =>
                {
                    WaitForServiceToStart("Assist_Service", TimeSpan.FromSeconds(90));
                });
                serviceWaitThread.IsBackground = true;
                serviceWaitThread.Start();

                Application.Run(mainForm);

                GC.KeepAlive(mutex);
            }

            // ----------------------------
            // GLOBAL EXCEPTION LOGGING
            // ----------------------------
            Application.ThreadException += (s, e) =>
            {
                File.AppendAllText("Assist_TSR_FATAL.txt", e.Exception.ToString());
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                File.AppendAllText("Assist_TSR_FATAL.txt", e.ExceptionObject.ToString());
            };
        }

        // =====================================================
        // TASK SCHEDULER LOGIC
        // =====================================================

        private static void RegisterTSRTask()
        {
            string exePath = Process.GetCurrentProcess().MainModule.FileName;

            string command =
                $"/create /f /sc onlogon /rl highest " +
                $"/tn \"{TaskName}\" " +
                $"/tr \"\\\"{exePath}\\\"\"";

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = command,
                Verb = "runas", // force admin (UAC once)
                UseShellExecute = true,
                CreateNoWindow = true
            };

            Process.Start(psi);
        }

        private static bool TaskExists(string taskName)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/query /tn \"{taskName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process p = Process.Start(psi))
            {
                p.WaitForExit();
                return p.ExitCode == 0;
            }
        }

        // =====================================================
        // SERVICE WAIT LOGIC
        // =====================================================

        private static void WaitForServiceToStart(string serviceName, TimeSpan timeout)
        {
            DateTime startTime = DateTime.Now;

            while (DateTime.Now - startTime < timeout)
            {
                try
                {
                    using (ServiceController service = new ServiceController(serviceName))
                    {
                        service.Refresh();
                        if (service.Status == ServiceControllerStatus.Running)
                        {
                            Thread.Sleep(1000);
                            return;
                        }
                    }
                }
                catch { }

                Thread.Sleep(1000);
            }
        }

        // =====================================================
        // LOGIN VALIDATION (UNCHANGED)
        // =====================================================

        public static bool ValidateLogin(string usernameInput, string passwordInput)
        {
            try
            {
                string filePath = @"C:\ProgramData\Assist\Auth\credentials.bin";
                if (!File.Exists(filePath))
                    return false;

                string savedUsername, savedPasswordEncrypted;

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    savedUsername = br.ReadString();
                    savedPasswordEncrypted = br.ReadString();
                }

                byte[] encryptedBytes = Convert.FromBase64String(savedPasswordEncrypted);
                byte[] decryptedBytes = ProtectedData.Unprotect(
                    encryptedBytes, null, DataProtectionScope.LocalMachine);

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
