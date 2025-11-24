using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Assist_TSR.Classes;

namespace Assist_TSR.Forms
{
    public partial class LoginForm : Form
    {
        float logoOpacity = 0f;
        Timer fadeTimer = new Timer();

        // Controls
        private PictureBox pictureBoxAssist;
        private TextBox txtUsername;
        private TextBox txtPassword;
        private Button btnLogin;

        public LoginForm()
        {
            InitializeComponent();
            BuildUI();
        }

        private void LoginForm_Load(object sender, EventArgs e)
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
            heading.Text = "Admin Login Form";
            heading.Font = new Font("Segoe UI Semibold", 18);
            heading.ForeColor = Color.FromArgb(45, 45, 45);
            heading.AutoSize = true;
            heading.Location = new Point(200, 10);
            card.Controls.Add(heading);

            // -----------------------------------------------------
            // USERNAME LABEL & TEXTBOX
            // -----------------------------------------------------
            Label lblUser = new Label();
            lblUser.Text = "Username";
            lblUser.Font = new Font("Segoe UI", 11);
            lblUser.Location = new Point(25, 80);
            lblUser.AutoSize = true;
            card.Controls.Add(lblUser);

            txtUsername = new TextBox();
            txtUsername.Width = 220;
            txtUsername.Height = 35;
            txtUsername.Font = new Font("Segoe UI", 11);
            txtUsername.Location = new Point(25, 110);
            txtUsername.BackColor = Color.FromArgb(240, 242, 247);
            txtUsername.BorderStyle = BorderStyle.FixedSingle;
            card.Controls.Add(txtUsername);

            // -----------------------------------------------------
            // PASSWORD LABEL & TEXTBOX
            // -----------------------------------------------------
            Label lblPass = new Label();
            lblPass.Text = "Password";
            lblPass.Font = new Font("Segoe UI", 11);
            lblPass.Location = new Point(25, 160);
            lblPass.AutoSize = true;
            card.Controls.Add(lblPass);

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
            // LOGIN BUTTON
            // -----------------------------------------------------
            btnLogin = new Button();
            btnLogin.Text = "Login";
            btnLogin.Font = new Font("Segoe UI Semibold", 12);
            btnLogin.Width = 140;
            btnLogin.Height = 40;
            btnLogin.Location = new Point(55, 240);
            btnLogin.ForeColor = Color.White;
            btnLogin.BackColor = Color.FromArgb(74, 144, 226);
            btnLogin.FlatStyle = FlatStyle.Flat;
            btnLogin.FlatAppearance.BorderSize = 0;

            btnLogin.Click += BtnLogin_Click;
            card.Controls.Add(btnLogin);

            // -----------------------------------------------------
            // ASSIST LOGO
            // -----------------------------------------------------
            pictureBoxAssist = new PictureBox();
            pictureBoxAssist.Size = new Size(220, 160);
            pictureBoxAssist.Location = new Point(360, 80);
            pictureBoxAssist.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBoxAssist.Image = Properties.Resources.assist_logo; // your logo
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
        // LOGIN BUTTON CLICK LOGIC
        // ---------------------------------------------------------
        private void BtnLogin_Click(object sender, EventArgs e)
        {
            try
            {
                string username = txtUsername.Text.Trim();
                string password = txtPassword.Text.Trim();

                // Check empty fields
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("Please enter both username and password.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Use centralized validation from Program.cs (with DPAPI)
                if (Program.ValidateLogin(username, password))
                {
                    // Login successful → close form with OK
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    // Invalid credentials
                    MessageBox.Show("Invalid username or password.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                // Preserve previous error handling
                MessageBox.Show("Error during login: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }
}
