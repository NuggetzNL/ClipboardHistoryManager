using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClipboardHistoryManager
{
    public class ClipboardItem
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } // text, image, file, etc.
        public string Content { get; set; } // plain text of base64 encoded image or file path
    }
}
