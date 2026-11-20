using System;
using System.Windows.Forms;

namespace NoctyraSetup
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool uninstall = Array.Exists(args, a => string.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase));
            Application.Run(new MainForm(uninstall));
        }
    }
}
