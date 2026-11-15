using System;
using System.Windows.Forms;

namespace SoundReplacer.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // PerMonitorV2 — чтобы на 125/150% масштабировании интерфейс не мылился.
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}