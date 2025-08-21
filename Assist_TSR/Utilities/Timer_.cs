using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Assist_TSR.Utilities
{
    public class Timer_
    {
        private System.Windows.Forms.Timer refreshTimer;

        private readonly Func<Task> _updatePeersCallback;

        public Timer_(Func<Task> updatePeersCallback)
        {
            _updatePeersCallback = updatePeersCallback;
        }

        public void InitializeTimer()
        {
            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = 5000; // Refresh every 5 seconds
            refreshTimer.Tick += RefreshTimer_Tick;
            refreshTimer.Start();
        }

        public async void RefreshTimer_Tick(object sender, EventArgs e)
        {
            if (_updatePeersCallback != null)
            {
                await _updatePeersCallback();
            }
        }
    }
}
