
namespace NW_Market_Collector_Form
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.buttonToStart = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.textBoxForUser = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textBoxForCredentials = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.comboBoxForServer = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // buttonToStart
            // 
            this.buttonToStart.Location = new System.Drawing.Point(13, 13);
            this.buttonToStart.Name = "buttonToStart";
            this.buttonToStart.Size = new System.Drawing.Size(75, 23);
            this.buttonToStart.TabIndex = 0;
            this.buttonToStart.Text = "Start";
            this.buttonToStart.UseVisualStyleBackColor = true;
            this.buttonToStart.Click += new System.EventHandler(this.buttonToStart_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 43);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(30, 15);
            this.label1.TabIndex = 1;
            this.label1.Text = "User";
            // 
            // textBoxForUser
            // 
            this.textBoxForUser.Location = new System.Drawing.Point(13, 62);
            this.textBoxForUser.Name = "textBoxForUser";
            this.textBoxForUser.Size = new System.Drawing.Size(121, 23);
            this.textBoxForUser.TabIndex = 2;
            this.textBoxForUser.TextChanged += new System.EventHandler(this.textBoxForUser_TextChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 92);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(66, 15);
            this.label2.TabIndex = 3;
            this.label2.Text = "Credentials";
            // 
            // textBoxForCredentials
            // 
            this.textBoxForCredentials.Location = new System.Drawing.Point(13, 111);
            this.textBoxForCredentials.Name = "textBoxForCredentials";
            this.textBoxForCredentials.Size = new System.Drawing.Size(121, 23);
            this.textBoxForCredentials.TabIndex = 4;
            this.textBoxForCredentials.UseSystemPasswordChar = true;
            this.textBoxForCredentials.TextChanged += new System.EventHandler(this.textBoxForCredentials_TextChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(13, 141);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(39, 15);
            this.label3.TabIndex = 5;
            this.label3.Text = "Server";
            // 
            // comboBoxForServer
            // 
            this.comboBoxForServer.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.comboBoxForServer.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.comboBoxForServer.DisplayMember = "Name";
            this.comboBoxForServer.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxForServer.FormattingEnabled = true;
            this.comboBoxForServer.Location = new System.Drawing.Point(13, 160);
            this.comboBoxForServer.Name = "comboBoxForServer";
            this.comboBoxForServer.Size = new System.Drawing.Size(121, 23);
            this.comboBoxForServer.TabIndex = 6;
            this.comboBoxForServer.ValueMember = "Id";
            this.comboBoxForServer.SelectedIndexChanged += new System.EventHandler(this.comboBoxForServer_SelectedIndexChanged);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.comboBoxForServer);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textBoxForCredentials);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textBoxForUser);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.buttonToStart);
            this.Name = "Form1";
            this.Text = "NW Market";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button buttonToStart;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBoxForUser;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textBoxForCredentials;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox comboBoxForServer;
    }
}

