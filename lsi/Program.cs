using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace lsi
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            LSICommon.Instance.SetLSIAppPath(Application.StartupPath);
            LSICommon.Instance.Init();

            Application.Run(new frmMain());
        }
    }
}