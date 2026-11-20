using System;
using System.Windows.Forms;
using HTREASURE.Branding.WinForms;

namespace HTBulkFileRenamer
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // High-DPI mode, visual styles and default font from the .csproj (HT-Software-Template).
            ApplicationConfiguration.Initialize();
            // Brief HTREASURE splash, as in the template: shown only while the main window is built.
            MainForm main;
            using (var splash = new BrandSplashForm())
            {
                splash.Show();
                splash.Report(0.5, "Loading settings…");
                Application.DoEvents();
                main = new MainForm();
                splash.Report(1.0, "Ready");
            }
            Application.Run(main);
        }
    }
}
