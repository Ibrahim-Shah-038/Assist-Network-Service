using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assist_TSR.Event_Handler
{
    public class launcher
    {
        public bool LaunchApplication(string appName)
        {
            try
            {
                Process.Start(new ProcessStartInfo(appName) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error launching application: {ex.Message}");
                return false;
            }
        }
    }
}
