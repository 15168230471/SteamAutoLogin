using System.Text;
using System.Windows.Forms;

namespace SteamAutoLogin
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Register code pages provider for ExcelDataReader (it uses System.Text.EncodingCodePages)
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }
}