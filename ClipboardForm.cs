using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ClipboardHistoryManager.Data;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace ClipboardHistoryManager
{
    public partial class ClipboardForm : Form
    {
        private DataGridView grid;
        private TextBox searchBox;
        private ClipboardMonitor monitor;
        private ComboBox tagFilterBox;
        private Button muteButton;

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
            
            #region DataGridView
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoGenerateColumns = false
            };

            #region Columns
            #region Type column
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Type",
                Width = 100,
                Name = "TypeColumn",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            });
            #endregion Type column

            #region Content column
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Content",
                Width = 450,
                Name = "ContentColumn",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 70
            });
            #endregion Content column

            #region Tag column (dropdown)
            var tagColumn = new DataGridViewComboBoxColumn
            {
                HeaderText = "Tag",
                Width = 150,
                Name = "TagColumn",
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };

            // Add always an empty option "no tag"
            tagColumn.Items.Add("");

            // Get all existing tags from DB
            foreach (var tag in Database.GetAllAvailableTags())
            {
                if (!string.IsNullOrWhiteSpace(tag) && !tagColumn.Items.Contains(tag))
                {
                    tagColumn.Items.Add(tag);
                }
            }

            grid.Columns.Add(tagColumn);
            #endregion Tag column (dropdown)
            #endregion Columns

            // Events for custom input
            grid.EditingControlShowing += Grid_EditingControlShowing;
            grid.CellValidating += Grid_CellValidating;

            #region Contextmenu
            var contextMenu = new ContextMenuStrip();

            var deleteItem = new ToolStripMenuItem("Delete");
            deleteItem.Click += DeleteSelectedItem;
            contextMenu.Items.Add(deleteItem);

            var getContentItem = new ToolStripMenuItem("Copy Content");
            getContentItem.Click += GetContentSelectedItem;
            contextMenu.Items.Add(getContentItem);

            var tagItem = new ToolStripMenuItem("Tag Item");
            tagItem.Click += TagSelectedItem;
            contextMenu.Items.Add(tagItem);

            grid.ContextMenuStrip = contextMenu;
            #endregion Contextmenu

            // Double-click to copy content
            grid.CellDoubleClick += GetContentSelectedItem;

            // Tag changed, save
            grid.CellValueChanged += Grid_CellValueChanged;

            panel.Controls.Add(grid);
            #endregion DataGridView

            // Add tag and search
            InitFilterAndSearch(panel);

            monitor = new ClipboardMonitor();
            monitor.OnClipboardText += SaveText;
            monitor.OnClipboardImage += SaveImage;
        }

        #region Tag methods
        private void InitFilterAndSearch(Panel panel)
        {
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                RowCount = 2,
                AutoSize = true,
                Padding = new Padding(5)
            };

            // Columns
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            // Rows
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var tagFilterLabel = new Label
            {
                Text = "Tag Filter:",
                Anchor = AnchorStyles.Left,
                AutoSize = true,
                Margin = new Padding(0, 5, 5, 5)
            };

            tagFilterBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // Load tags + "All" option
            var tags = Database.GetAllAvailableTags();
            tags.Insert(0, "All");
            tagFilterBox.DataSource = tags;

            tagFilterBox.SelectedIndexChanged += (s, e) =>
            {
                LoadHistory(searchBox.Text);
            };

            // Search label
            var searchLabel = new Label
            {
                Text = "Search: ",
                Anchor = AnchorStyles.Left,
                AutoSize = true,
                Margin = new Padding(0, 5, 5, 5)
            };

            searchBox = new TextBox
            {
                Dock = DockStyle.Fill,
                PlaceholderText = "Search..."
            };

            // Button mute clipboard events
            muteButton = new Button
            {
                Text = "Mute Clipboard",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(5)
            };

            muteButton.Click += (s, e) => MuteClipboard(s, e);

            searchBox.TextChanged += (s, e) => LoadHistory(searchBox.Text);

            table.Controls.Add(searchLabel, 0, 0);
            table.Controls.Add(searchBox, 1, 0);
            table.Controls.Add(tagFilterLabel, 0, 1);
            table.Controls.Add(tagFilterBox, 1, 1);
            table.Controls.Add(muteButton, 2, 0);

            panel.Controls.Add(table);
        }

        private void Grid_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (grid.CurrentCell.ColumnIndex == grid.Columns["TagColumn"].Index &&
                e.Control is ComboBox combo)
            {
                // Dropdown always open on edit (custom input)
                combo.DropDownStyle = ComboBoxStyle.DropDown;
            }
        }

        private void Grid_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.ColumnIndex == grid.Columns["TagColumn"].Index)
            {
                string newTag = e.FormattedValue?.ToString().Trim() ?? "";

                var tagColumn = (DataGridViewComboBoxColumn)grid.Columns["TagColumn"];

                if (!tagColumn.Items.Contains(newTag))
                {
                    // Add new Tag to Dropdown if it doesn't exist
                    tagColumn.Items.Add(newTag);
                }

                // Add tag to item and save in database
                var row = grid.Rows[e.RowIndex];
                if (row.Tag is int id)
                {
                    row.Cells["TagColumn"].Value = newTag;
                    Database.UpdateTag(id, newTag);
                }
            }
        }

        private void Grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (e.ColumnIndex == grid.Columns["TagColumn"].Index)
            {
                var row = grid.Rows[e.RowIndex];
                if (row.Tag is int id)
                {
                    string newTag = row.Cells["TagColumn"].Value?.ToString().Trim() ?? string.Empty;
                    Database.UpdateTag(id, newTag);
                }
            }
        }

        private void TagSelectedItem(object sender, EventArgs e)
        {
            if (grid.SelectedRows.Count == 0) return;

            var row = grid.SelectedRows[0];
            var cell = row.Cells["TagColumn"];

            grid.CurrentCell = cell;
            grid.BeginEdit(true);

            if (grid.EditingControl is ComboBox cb)
            {
                cb.DroppedDown = true;
            }
        }
        #endregion Tag methods
        
        #region Save methods
        private void SaveText(string type, string text)
        {
            if (suppressClipboardEvent) return;
            if (string.IsNullOrWhiteSpace(text)) return;
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
            byte[] bytes = ms.ToArray();
            string base64 = Convert.ToBase64String(bytes);

            string hash = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.Create().ComputeHash(bytes));

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
        #endregion Save methods

        private void DeleteSelectedItem(object sender, EventArgs e)
        {
            if (grid.SelectedRows.Count > 0)
            {
                var confirm = MessageBox.Show(
                    $"Are you sure to delete the selected {grid.SelectedRows.Count} item(s)?",
                    "Confirm",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm == DialogResult.Yes)
                {
                    foreach (DataGridViewRow row in grid.SelectedRows)
                    {
                        if (row.Tag is int id)
                        {
                            Database.Delete(id);
                        }
                    }
                    LoadHistory();
                }
            }
        }

        private void GetContentSelectedItem(object sender, EventArgs e)
        {
            if (grid.SelectedRows.Count != 1)
            {
                if (grid.SelectedRows.Count > 1)
                {
                    MessageBox.Show("Please select only one item to copy.",
                        "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }

            var row = grid.SelectedRows[0];
            if (row.Tag is not int id) return;

            string content = Database.GetContent(id);
            if (string.IsNullOrEmpty(content)) return;

            suppressClipboardEvent = true;

            if (string.Equals(row.Cells["TypeColumn"].Value?.ToString(), "image", StringComparison.OrdinalIgnoreCase))
            {
                byte[] imageBytes = Convert.FromBase64String(content);
                using var ms = new MemoryStream(imageBytes);
                Clipboard.SetImage(Image.FromStream(ms));

                lastImageHash = Convert.ToBase64String(
                    System.Security.Cryptography.SHA256.Create().ComputeHash(imageBytes));
            }
            else
            {
                Clipboard.SetText(content);
                lastTextContent = content;
            }

            Database.UpdateTimestamp(id);
            LoadHistory();

            var t = new WinFormsTimer { Interval = 100 };
            t.Tick += (s, ev) =>
            {
                suppressClipboardEvent = false;
                t.Stop();
                t.Dispose();
            };
            t.Start();
        }

        private void LoadHistory(string filter = "")
        {
            grid.Rows.Clear();

            var tagColumn = grid.Columns["TagColumn"] as DataGridViewComboBoxColumn;
            List<ClipboardItem> items;

            if (tagFilterBox != null && tagFilterBox.SelectedItem is string selectedTag && selectedTag != "All")
            {
                items = Database.GetByTag(selectedTag);
            }
            else
            {
                items = Database.GetAll();
            }

            foreach (var entry in items)
            {
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
                int rowIndex = grid.Rows.Add(entry.Type, display, string.IsNullOrEmpty(entry.Tag) ? "" : entry.Tag);
                grid.Rows[rowIndex].Tag = entry.Id;

                // tag dynamisch aanvullen (maar skip leeg)
                if (!string.IsNullOrEmpty(entry.Tag) && !tagColumn.Items.Contains(entry.Tag))
                {
                    tagColumn.Items.Add(entry.Tag);
                }
            }
        }

        private void MuteClipboard(object sender, EventArgs e)
        {
            if (muteButton.Text == "Mute Clipboard")
            {
                muteButton.Text = "Unmute Clipboard";
                suppressClipboardEvent = true;
            }
            else
            {
                muteButton.Text = "Mute Clipboard";
                suppressClipboardEvent = false;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LoadHistory();
        }
    }
}
