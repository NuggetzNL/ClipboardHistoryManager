using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ClipboardHistoryManager
{
    public class ClipboardMonitor : Form
    {
        public event Action<string, string> OnClipboardText;
        public event Action<string, Image> OnClipboardImage;

        private const int WM_CLIPBOARDUPDATE = 0x031D;
        private static IntPtr HWND_MESSAGE = new IntPtr(-3);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        public ClipboardMonitor()
        {
            this.Visible = false;
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;
            AddClipboardFormatListener(this.Handle);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_CLIPBOARDUPDATE)
            {
                if (Clipboard.ContainsText())
                {
                    OnClipboardText?.Invoke("text", Clipboard.GetText());
                }
                else if (Clipboard.ContainsImage())
                {
                    OnClipboardImage?.Invoke("image", Clipboard.GetImage());
                }
            }
            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            RemoveClipboardFormatListener(this.Handle);
            base.Dispose(disposing);
        }
    }
}
