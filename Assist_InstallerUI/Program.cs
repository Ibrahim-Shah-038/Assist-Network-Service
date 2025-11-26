using System;
using System.Windows.Forms;

namespace Assist_InstallerUI
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Run your registration form directly
            Application.Run(new RegisterForm());
        }
    }
}
