using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.Windows.Forms;

namespace Assist_InstallerUI
{
    [RunInstaller(true)]
    public partial class RegistrationInstaller : Installer
    {
        public RegistrationInstaller()
        {
            InitializeComponent();
        }

        // This method is called when the installer runs
        public override void Install(IDictionary stateSaver)
        {
            // Call base method first
            base.Install(stateSaver);

            try
            {
                // Open the registration form during installation
                RegisterForm form = new RegisterForm();

                // Show as modal dialog
                form.ShowDialog();
            }
            catch (Exception ex)
            {
                // Optional: handle exceptions during install
                MessageBox.Show("Error opening registration form: " + ex.Message, "Installer Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
