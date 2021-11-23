using MW_Market_Model;
using NW_Market_Collector;
using System;
using System.Windows.Forms;

namespace NW_Market_Collector_Form
{
    public partial class Form1 : Form
    {
        private readonly FormProperties formProperties = new FormProperties();

        public Form1()
        {
            InitializeComponent();

            ConfigurationDatabase configurationDatabase = new ConfigurationDatabase();
            comboBoxForServer.Items.AddRange(configurationDatabase.Content.ServerList.ToArray());
            buttonToStart.Enabled = formProperties.IsValid();
        }

        private void buttonToStart_Click(object sender, EventArgs e)
        {
            MarketCollector.Start(new ApplicationConfiguration
            {
                Credentials = formProperties.Credentials,
                Server = formProperties.Server,
                User = formProperties.User,
            });
        }

        private void textBoxForUser_TextChanged(object sender, EventArgs e)
        {
            formProperties.User = textBoxForUser.Text;
        }

        private void textBoxForCredentials_TextChanged(object sender, EventArgs e)
        {
            formProperties.Credentials = textBoxForCredentials.Text;
            buttonToStart.Enabled = formProperties.IsValid();
        }

        private void comboBoxForServer_SelectedIndexChanged(object sender, EventArgs e)
        {
            formProperties.Server = ((ServerListInfo)comboBoxForServer.SelectedItem).Id;
            buttonToStart.Enabled = formProperties.IsValid();
        }
    }

    public class FormProperties
    {
        public string User;
        public string Credentials;
        public string Server;

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Credentials) && !string.IsNullOrWhiteSpace(Server);
        }
    }
}
