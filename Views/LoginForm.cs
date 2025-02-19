using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SaveSync.Views
{
    public partial class LoginForm : Form
    {
        private ProgressBar progressBar;
        private TextBox logTextBox;
        private Button loginButton;
        private FolderBrowserDialog folderDialog;

        public LoginForm()
        {
            InitializeComponent();
            progressBar = new ProgressBar { Location = new Point(10, 10), Width = 300 };
            logTextBox = new TextBox { Location = new Point(10, 40), Width = 300, Height = 150, Multiline = true };
            loginButton = new Button { Text = "Login", Location = new Point(10, 200) };
            folderDialog = new FolderBrowserDialog();

            loginButton.Click += LoginButton_Click;

            Controls.Add(progressBar);
            Controls.Add(logTextBox);
            Controls.Add(loginButton);
        }

        private async void LoginButton_Click(object sender, EventArgs e)
        {
            string userEmail = "user@example.com"; // Substitua pelo campo de entrada de e-mail
            string diretorio = @"C:\Caminho\para\SaveSync"; // Use a seleção de diretório

            logTextBox.AppendText("Iniciando sincronização...\n");

            var progress = new Progress<int>(percent =>
            {
                progressBar.Value = percent;
            });

            var login = new LoginController();

            login.Login(txtEmail.Text, txtDiretorio.Text, progress);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var login = new LoginController();

            //login.Login(txtEmail.Text, txtDiretorio.Text);
        }
    }
}
