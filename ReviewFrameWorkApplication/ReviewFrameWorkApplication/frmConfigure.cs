using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace lsi
{
    public partial class frmConfigure : Form
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public frmConfigure()
        {
            InitializeComponent();
        }

        // Form Load
        private void frmConfigure_Load(object sender, EventArgs e)
        {
            this.txtDocumentDirectory.Text = LSICommon.Instance.LSIConfig.document_directory;
            this.txt_k_percent.Text = LSICommon.Instance.LSIConfig.k_percent_for_rank_approx.ToString();
        }

        // Save the configuration
        private void btnSaveConfig_Click(object sender, EventArgs e)
        {
            try
            {
                LSICommon.Instance.LSIConfig.document_directory = this.txtDocumentDirectory.Text;
                LSICommon.Instance.LSIConfig.k_percent_for_rank_approx = Convert.ToInt32(this.txt_k_percent.Text.Trim());
                LSICommon.Instance.LSIConfig.SaveToFile();
                this.Close();

                MessageBox.Show("You need to index again for the settings to work properly");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Exception occurred \n" + ex.Message + "\n" + ex.StackTrace);
            }
        }

        // Cancel
        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

    }
}