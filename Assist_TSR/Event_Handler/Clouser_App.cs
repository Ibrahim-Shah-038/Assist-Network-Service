using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assist_TSR.Event_Handler
{
    public class Clouser_App
    {
        public bool CloseApplication(string appName)
        {
            try
            {
                // Get all processes with the specified name
                var processes = Process.GetProcessesByName(appName);

                if (processes.Length == 0)
                {
                    Debug.WriteLine($"No running instances of {appName} found");
                    return false;
                }

                // Close all instances of the application
                bool allClosed = true;
                foreach (var process in processes)
                {
                    try
                    {
                        // Try to close the main window first (graceful shutdown)
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            process.CloseMainWindow();

                            // Wait for the process to exit gracefully
                            if (!process.WaitForExit(5000)) // 5 second timeout
                            {
                                process.Kill();
                                Debug.WriteLine($"Forcefully killed {appName} (PID: {process.Id})");
                            }
                        }
                        else
                        {
                            process.Kill();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error closing {appName} (PID: {process.Id}): {ex.Message}");
                        allClosed = false;
                    }
                }

                return allClosed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CloseApplication for {appName}: {ex.Message}");
                return false;
            }
        }
    }
}
