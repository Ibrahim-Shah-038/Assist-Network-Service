using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Assist_InstallerUI
{
    public partial class RegisterForm : Form
    {
        float logoOpacity = 0f;
        Timer fadeTimer = new Timer();

        // Controls (so they can be referenced later if needed)
        private PictureBox pictureBoxAssist;
        private TextBox txtUsername;
        private TextBox txtPassword;
        private Button btnSave;

        public RegisterForm()
        {
            InitializeComponent();
            BuildUI();
        }

        private void RegisterForm_Load(object sender, EventArgs e)
        {
            StartFade();
        }

        // ---------------------------------------------------------
        // FADE ANIMATION FOR ASSIST ICON
        // ---------------------------------------------------------
        private void StartFade()
        {
            fadeTimer.Interval = 30;
            fadeTimer.Tick += (s, e) =>
            {
                if (logoOpacity < 1f)
                {
                    logoOpacity += 0.05f;
                    pictureBoxAssist.Invalidate();
                }
                else
                {
                    fadeTimer.Stop();
                }
            };
            fadeTimer.Start();
        }

        private void pictureBoxAssist_Paint(object sender, PaintEventArgs e)
        {
            if (pictureBoxAssist.Image != null)
            {
                ColorMatrix cm = new ColorMatrix();
                cm.Matrix33 = logoOpacity;

                ImageAttributes attr = new ImageAttributes();
                attr.SetColorMatrix(cm);

                e.Graphics.DrawImage(
                    pictureBoxAssist.Image,
                    new Rectangle(0, 0, pictureBoxAssist.Width, pictureBoxAssist.Height),
                    0, 0,
                    pictureBoxAssist.Image.Width,
                    pictureBoxAssist.Image.Height,
                    GraphicsUnit.Pixel,
                    attr
                );
            }
        }

        // ---------------------------------------------------------
        // BUILD COMPLETE UI PROGRAMMATICALLY
        // ---------------------------------------------------------
        private void BuildUI()
        {
            // Background color
            this.BackColor = Color.FromArgb(240, 243, 248);

            // -----------------------------------------------------
            // SHADOW PANEL
            // -----------------------------------------------------
            ShadowPanel shadow = new ShadowPanel();
            shadow.Size = new Size(650, 320);
            shadow.Location = new Point(40, 40);

            // -----------------------------------------------------
            // MAIN WHITE CARD
            // -----------------------------------------------------
            Panel card = new Panel();
            card.Size = new Size(642, 312);
            card.BackColor = Color.White;
            card.Location = new Point(0, 0);
            card.Padding = new Padding(20);

            shadow.Controls.Add(card);
            this.Controls.Add(shadow);

            // -----------------------------------------------------
            // HEADING
            // -----------------------------------------------------
            Label heading = new Label();
            heading.Text = "Admin Registration Form";
            heading.Font = new Font("Segoe UI Semibold", 18);
            heading.ForeColor = Color.FromArgb(45, 45, 45);
            heading.AutoSize = true;
            heading.Location = new Point(170, 10);
            card.Controls.Add(heading);

            // -----------------------------------------------------
            // USERNAME LABEL
            // -----------------------------------------------------
            Label lblUser = new Label();
            lblUser.Text = "Username";
            lblUser.Font = new Font("Segoe UI", 11);
            lblUser.Location = new Point(25, 80);
            lblUser.AutoSize = true;
            card.Controls.Add(lblUser);

            // USERNAME TEXTBOX
            txtUsername = new TextBox();
            txtUsername.Width = 220;
            txtUsername.Height = 35;
            txtUsername.Font = new Font("Segoe UI", 11);
            txtUsername.Location = new Point(25, 110);
            txtUsername.BackColor = Color.FromArgb(240, 242, 247);
            txtUsername.BorderStyle = BorderStyle.FixedSingle;
            card.Controls.Add(txtUsername);

            // -----------------------------------------------------
            // PASSWORD LABEL
            // -----------------------------------------------------
            Label lblPass = new Label();
            lblPass.Text = "Password";
            lblPass.Font = new Font("Segoe UI", 11);
            lblPass.Location = new Point(25, 160);
            lblPass.AutoSize = true;
            card.Controls.Add(lblPass);

            // PASSWORD TEXTBOX
            txtPassword = new TextBox();
            txtPassword.Width = 220;
            txtPassword.Height = 35;
            txtPassword.Font = new Font("Segoe UI", 11);
            txtPassword.Location = new Point(25, 190);
            txtPassword.PasswordChar = '*';
            txtPassword.BackColor = Color.FromArgb(240, 242, 247);
            txtPassword.BorderStyle = BorderStyle.FixedSingle;
            card.Controls.Add(txtPassword);

            // -----------------------------------------------------
            // SAVE BUTTON
            // -----------------------------------------------------
            btnSave = new Button();
            btnSave.Text = "Save";
            btnSave.Font = new Font("Segoe UI Semibold", 12);
            btnSave.Width = 140;
            btnSave.Height = 40;
            btnSave.Location = new Point(55, 240);
            btnSave.ForeColor = Color.White;
            btnSave.BackColor = Color.FromArgb(74, 144, 226);
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.FlatAppearance.BorderSize = 0;

            btnSave.Click += BtnSave_Click;
            card.Controls.Add(btnSave);

            // -----------------------------------------------------
            // ASSIST LOGO
            // -----------------------------------------------------
            pictureBoxAssist = new PictureBox();
            pictureBoxAssist.Size = new Size(220, 160);
            pictureBoxAssist.Location = new Point(360, 80);
            pictureBoxAssist.SizeMode = PictureBoxSizeMode.Zoom;

            // IMPORTANT: set your own project icon here
            // Example:
            // pictureBoxAssist.Image = Properties.Resources.assist_icon;
            pictureBoxAssist.Image = Properties.Resources.assist_logo;

            pictureBoxAssist.Paint += pictureBoxAssist_Paint;
            card.Controls.Add(pictureBoxAssist);

            // -----------------------------------------------------
            // ASSIST TEXT
            // -----------------------------------------------------
            Label brand = new Label();
            brand.Text = "ASSIST";
            brand.Font = new Font("Segoe UI Black", 20, FontStyle.Bold);
            brand.ForeColor = Color.FromArgb(50, 50, 50);
            brand.AutoSize = true;
            brand.Location = new Point(400, 240);
            card.Controls.Add(brand);
        }

        // ---------------------------------------------------------
        // SAVE BUTTON CLICK LOGIC
        // ---------------------------------------------------------
        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                // 1️⃣ Get input values
                string username = txtUsername.Text.Trim();
                string password = txtPassword.Text.Trim();

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("Please enter both username and password.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 2️⃣ Create folder if not exists
                string folderPath = @"C:\ProgramData\Assist\Auth";
                if (!System.IO.Directory.Exists(folderPath))
                {
                    System.IO.Directory.CreateDirectory(folderPath);
                }

                // 3️⃣ File path
                string filePath = System.IO.Path.Combine(folderPath, "credentials.bin");

                // 4️⃣ Save data in binary format
                using (var fs = new System.IO.FileStream(filePath, System.IO.FileMode.Create, System.IO.FileAccess.Write))
                using (var bw = new System.IO.BinaryWriter(fs))
                {
                    bw.Write(username);
                    bw.Write(password);
                }

                MessageBox.Show("Credentials saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving credentials: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }
}
