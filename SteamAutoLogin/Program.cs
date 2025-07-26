using System.Text;

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
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }
}