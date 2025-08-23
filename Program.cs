using ClipboardHistoryManager.Data;
using System;
using System.Data.Entity;
using System.Windows.Forms;

namespace ClipboardHistoryManager
{
    static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Data.Database.Init();
            Application.Run(new ClipboardForm());
        }
    }
}