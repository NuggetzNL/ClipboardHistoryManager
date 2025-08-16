using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ClipboardHistoryManager;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace ClipboardHistoryManager
{
    public partial class ClipboardForm : Form
    {
        private ListView listView;
        private ClipboardMonitor monitor;
        private bool suppressClipboardEvent = false;
        private string lastImageHash = null;
        private string lastTextContent = null;

        public ClipboardForm()
        {
            Text = "Clipboard History Manager";
            Width = 700;
            Height = 400;

            var panel = new Panel { Dock = DockStyle.Fill };
            Controls.Add(panel);

            #region ListView
            listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = true
            };
            listView.Columns.Add("Type", 100);
            listView.Columns.Add("Content", 450);
            listView.Columns.Add("Tag", 150);

            var contextMenu = new ContextMenuStrip();
            #region Deleting items
            var deleteItem = new ToolStripMenuItem("Delete");
            deleteItem.Click += DeleteSelectedItem;
            contextMenu.Items.Add(deleteItem);
            #endregion Deleting items

            #region Copy item content
            var getContentItem = new ToolStripMenuItem("Copy Content");
            getContentItem.Click += GetContentSelectedItem;
            contextMenu.Items.Add(getContentItem);

            // Double-click to copy content
            listView.DoubleClick += GetContentSelectedItem;
            #endregion Copy item content

            #region Tagging items
            var tagItem = new ToolStripMenuItem("Tag Item");
            tagItem.Click += TagSelectedItem;
            contextMenu.Items.Add(tagItem);
            #endregion Tagging items

            listView.ContextMenuStrip = contextMenu;
            panel.Controls.Add(listView);
            #endregion ListView

            #region SearchBox
            var searchBox = new TextBox()
            {
                Dock = DockStyle.Top,
                PlaceholderText = "Search...",
            };
            searchBox.TextChanged += (s, e) => LoadHistory(searchBox.Text);
            panel.Controls.Add(searchBox);
            #endregion SearchBox

            monitor = new ClipboardMonitor();
            monitor.OnClipboardText += SaveText;
            monitor.OnClipboardImage += SaveImage;
        }
        
        private void SaveText(string type, string text)
        {
            if (suppressClipboardEvent) return;

            if (string.IsNullOrWhiteSpace(text)) return;

            // Check for duplicate text
            if (text == lastTextContent) return;

            lastTextContent = text;

            var item = new ClipboardItem
            {
                Timestamp = DateTime.Now,
                Type = type,
                Content = text,
                Tag = ""
            };
            Database.Insert(item);
            LoadHistory();
        }

        private void SaveImage(string type, Image img)
        {
            if (suppressClipboardEvent) return;

            using var ms = new MemoryStream();
            img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            string base64 = Convert.ToBase64String(ms.ToArray());

            // Make hash to check for duplicates
            string hash =
                Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(ms.ToArray()));
            if (hash == lastImageHash) return;

            lastImageHash = hash;

            var item = new ClipboardItem
            {
                Timestamp = DateTime.Now,
                Type = type,
                Content = base64,
                Tag = ""
            };
            Database.Insert(item);
            LoadHistory();
        }

        private void DeleteSelectedItem(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count > 0)
            {
                var confirm = MessageBox.Show(
                    $"Are you sure to delete the selected {listView.SelectedItems.Count} item(s)?",
                    "Confirm",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm == DialogResult.Yes)
                {
                    foreach (ListViewItem item in listView.SelectedItems)
                    {
                        int id = (int)item.Tag; // Retrieve ID from ListView item
                        Database.Delete(id);
                    }
                    LoadHistory();
                }
            }
        }
        private void GetContentSelectedItem(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count != 0)
            {
                if (listView.SelectedItems.Count > 1)
                {
                    MessageBox.Show(
                        $"Please select only one item to copy.",
                        "Warning",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                var item = listView.SelectedItems[0];
                int id = (int)item.Tag; // Retrieve ID from ListView item
                string content = Database.GetContent(id);

                if (!string.IsNullOrEmpty(content))
                {
                    suppressClipboardEvent = true;

                    if (item.SubItems[0].Text == "image")
                    {
                        // Decode base64 image and copy to clipboard
                        byte[] imageBytes = Convert.FromBase64String(content);
                        using var ms = new MemoryStream(imageBytes);
                        Clipboard.SetImage(Image.FromStream(ms));

                        // Update lastImageHash to prevent duplicate image saves
                        lastImageHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(imageBytes));
                    }
                    else
                    {
                        // Copy text to clipboard
                        Clipboard.SetText(content);
                        lastTextContent = content; // Update last text content
                    }

                    // Update Timestamp
                    Database.UpdateTimestamp(id);
                    LoadHistory();

                    // Add delay for windows event
                    WinFormsTimer t = new WinFormsTimer();
                    t.Interval = 100; // 0,1 sec
                    t.Tick += (sender, e) =>
                    {
                        suppressClipboardEvent = false;
                        t.Stop();
                        t.Dispose();
                    };
                    t.Start();
                }
            }
        }

        private void TagSelectedItem(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count == 0) return;

            // Get available tags from the database
            var tags = Database.GetAllAvailableTags();

            // Create combobox
            var comboBox = new ComboBox
            {
                //DataSource = tags,
                DropDownStyle = ComboBoxStyle.DropDown,
                Width = 200
            };

            comboBox.Items.AddRange(tags.ToArray());
            comboBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            comboBox.AutoCompleteSource = AutoCompleteSource.ListItems;

            var prompt = new Form
            {
                Width = 250,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Select or create tag",
                StartPosition = FormStartPosition.CenterParent
            };

            var confirmation = new Button()
                { Text = "OK", Left = 150, Width = 60, Top = 40, DialogResult = DialogResult.OK };
            comboBox.Left = 20;
            comboBox.Top = 20;

            prompt.Controls.Add(comboBox);
            prompt.Controls.Add(confirmation);
            prompt.AcceptButton = confirmation;

            // Show the prompt dialog
            if (prompt.ShowDialog() == DialogResult.OK)
            {
                // Get selected tag or create new one
                string tag = comboBox.Text.Trim();
                if (string.IsNullOrEmpty(tag))
                {
                    MessageBox.Show("Tag cannot be empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                // Update the selected items with the new tag
                foreach (ListViewItem item in listView.SelectedItems)
                {
                    int id = (int)item.Tag; // Retrieve ID from ListView item
                    Database.UpdateTag(id, tag);
                }
                LoadHistory();
            }
        }

        private void LoadHistory(string filter = "")
        {
            listView.Items.Clear();
            foreach (var entry in Database.GetAll())
            {
                // Filter on content of type
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    if (!(entry.Content?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                          entry.Type?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                          entry.Tag?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        continue;
                    }
                }

                string display = entry.Type == "text" ? entry.Content : "[Image]";
                var lvi = new ListViewItem(new[] { entry.Type, display, entry.Tag });
                lvi.Tag = entry.Id; // Store ID for deletion
                listView.Items.Add(lvi);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LoadHistory();
        }
    }
}