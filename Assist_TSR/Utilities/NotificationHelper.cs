using System.Windows.Forms;

namespace Assist_TSR.Utilities
{
    public static class NotificationHelper
    {
        public static void ShowBalloonTip(NotifyIcon notifyIcon, string title, string message, ToolTipIcon icon)
        {
            notifyIcon?.ShowBalloonTip(1000, title, message, icon);
        }

        public static void ShowNotification(Label notificationLabel, Panel notificationPanel, string message)
        {
            notificationLabel.Text = message;
            notificationPanel.Visible = true;
        }
    }
}