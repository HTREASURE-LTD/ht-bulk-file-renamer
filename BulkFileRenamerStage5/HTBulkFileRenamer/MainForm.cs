using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using System.Windows.Forms;

namespace HTBulkFileRenamer
{
    public class MainForm : Form
    {
        readonly List<FileEntry> files = new List<FileEntry>();
        readonly List<Tuple<string, string>> undoBatch = new List<Tuple<string, string>>();
        string undoBatchId;
        readonly DataGridView grid = new DataGridView();
        readonly TextBox prefix = new TextBox(), suffix = new TextBox(), find = new TextBox(), replace = new TextBox();
        readonly CheckBox numbering = new CheckBox(), recursive = new CheckBox(), matchCase = new CheckBox(), useRegex = new CheckBox();
        readonly TextBox filter = new TextBox();
        readonly ToolStripProgressBar progress = new ToolStripProgressBar();
        readonly Button cancelButton = new Button();
        bool cancelRequested = false, busy = false, updatingGrid = false;
        int sortColumn = -1;
        bool sortDescending = false;
        readonly string settingsPath = Path.Combine(RenameHistory.DataDirectory, "settings.xml");
        readonly NumericUpDown start = new NumericUpDown(), digits = new NumericUpDown();
        readonly ComboBox casing = new ComboBox(), extensionMode = new ComboBox();
        readonly TextBox newExtension = new TextBox();
        readonly ToolStripStatusLabel status = new ToolStripStatusLabel();
        readonly Button renameButton = new Button(), undoButton = new Button();
        readonly TableLayoutPanel settings = new TableLayoutPanel();
        readonly FlowLayoutPanel actions = new FlowLayoutPanel();
        readonly ToolTip tips = new ToolTip { AutoPopDelay = 15000 };

        const int IncludeColumn = 0, OriginalColumn = 1, ProposedColumn = 2, ModifiedColumn = 3, StatusColumn = 4;

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        static extern int StrCmpLogicalW(string a, string b);

        public MainForm()
        {
            Text = Brand.ProductName;
            Icon = Brand.ProductIcon;
            Size = new Size(1180, 780);
            MinimumSize = new Size(960, 700);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            BackColor = Brand.PageGrey;
            AllowDrop = true;

            var menu = new MenuStrip { BackColor = Color.White };
            var fileMenu = new ToolStripMenuItem("&File");
            fileMenu.DropDownItems.Add("E&xit", null, (s, e) => Close());
            var helpMenu = new ToolStripMenuItem("&Help");
            helpMenu.DropDownItems.Add("&About " + Brand.ProductName, null, (s, e) => { using (var about = new AboutForm()) about.ShowDialog(this); });
            menu.Items.AddRange(new ToolStripItem[] { fileMenu, helpMenu });
            MainMenuStrip = menu;

            // Product header band: product icon, name and description on Deep Navy.
            var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Brand.DeepNavy };
            header.Controls.Add(new BrandImageBox { Image = Brand.ProductImage, Bounds = new Rectangle(16, 12, 40, 40) });
            header.Controls.Add(new Label { Text = Brand.ProductName, ForeColor = Color.White, AutoSize = true, Location = new Point(66, 10), Font = new Font("Segoe UI", 14F, FontStyle.Bold), UseMnemonic = false });
            header.Controls.Add(new Label { Text = Brand.ProductDescription, ForeColor = Brand.SilverGrey, AutoSize = true, Location = new Point(68, 38), UseMnemonic = false });

            settings.Dock = DockStyle.Top; settings.Height = 228; settings.Padding = new Padding(15, 10, 15, 5);
            settings.ColumnCount = 8; settings.RowCount = 4; settings.BackColor = Color.White;
            for (int i = 0; i < 8; i++) settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
            for (int i = 0; i < 4; i++) settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
            AddField(settings, "Prefix (tokens allowed)", prefix, 0, 0, 2);
            AddField(settings, "Suffix (before extension)", suffix, 2, 0, 2);
            AddField(settings, "Find", find, 4, 0, 2);
            AddField(settings, "Replace with", replace, 6, 0, 2);
            numbering.Text = "Add numbering (001_)"; numbering.AutoSize = true; numbering.Checked = true; numbering.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            var numberPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            numberPanel.Controls.Add(numbering);
            settings.Controls.Add(numberPanel, 0, 1); settings.SetColumnSpan(numberPanel, 2);
            start.Minimum = 0; start.Maximum = 999999; start.Value = 1;
            digits.Minimum = 1; digits.Maximum = 8; digits.Value = 3;
            AddField(settings, "Start number", start, 2, 1, 1);
            AddField(settings, "Digits", digits, 3, 1, 1);
            matchCase.Text = "Match case"; matchCase.AutoSize = true; matchCase.Checked = true; matchCase.Margin = new Padding(0, 3, 12, 0);
            useRegex.Text = "Use regex"; useRegex.AutoSize = true; useRegex.Margin = new Padding(0, 3, 0, 0);
            var findOptions = new FlowLayoutPanel { WrapContents = false };
            findOptions.Controls.Add(matchCase);
            findOptions.Controls.Add(useRegex);
            AddField(settings, "Find options", findOptions, 4, 1, 2);
            casing.DropDownStyle = ComboBoxStyle.DropDownList;
            casing.Items.AddRange(new object[] { "Keep original", "lowercase", "UPPERCASE", "Title Case" });
            casing.SelectedIndex = 0;
            AddField(settings, "Filename case", casing, 6, 1, 2);
            extensionMode.DropDownStyle = ComboBoxStyle.DropDownList;
            extensionMode.Items.AddRange(new object[] { "Keep original", "lowercase", "UPPERCASE", "Replace with..." });
            extensionMode.SelectedIndex = 0;
            AddField(settings, "Extension", extensionMode, 0, 2, 2);
            newExtension.Enabled = false;
            AddField(settings, "New extension (empty removes it)", newExtension, 2, 2, 2);
            var extensionHint = new Label { Text = "Only this setting changes the extension.", AutoSize = true, ForeColor = Brand.MutedText, Padding = new Padding(3, 25, 0, 0) };
            settings.Controls.Add(extensionHint, 4, 2); settings.SetColumnSpan(extensionHint, 4);
            var extra = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
            extra.Controls.Add(new Label { Text = "Filters (e.g. *.txt;*.jpg):", AutoSize = true, Padding = new Padding(0, 7, 3, 0) });
            filter.Width = 170; filter.Text = "*.*"; extra.Controls.Add(filter);
            recursive.Text = "Include subfolders"; recursive.AutoSize = true;
            tips.SetToolTip(recursive, "When adding a folder, also add files from its subfolders.");
            recursive.Margin = new Padding(18, 6, 0, 0); extra.Controls.Add(recursive);
            var tokenHint = new Label { Text = "  Tokens: {n} {date} {created} {taken} {parent}, e.g. {taken:yyyyMMdd}", AutoSize = true, ForeColor = Brand.MutedText, Padding = new Padding(5, 7, 0, 0) };
            extra.Controls.Add(tokenHint);
            settings.Controls.Add(extra, 0, 3);
            settings.SetColumnSpan(extra, 8);
            foreach (Control c in new Control[] { prefix, suffix, tokenHint }) tips.SetToolTip(c, RenameEngine.TokenHelp);
            tips.SetToolTip(useRegex, "Find is a .NET regular expression; use $1, $2 in Replace for captured groups.");
            tips.SetToolTip(numbering, "Adds the number at the start. Put {n} in the prefix or suffix to place it yourself.");

            actions.Dock = DockStyle.Top; actions.Height = 106; actions.Padding = new Padding(15, 8, 10, 6);
            actions.BackColor = Brand.PageGrey; actions.WrapContents = true;
            // Brand button roles: Electric Blue for the one primary action, Technology Blue secondary, Edge neutral.
            var blue = Brand.TechnologyBlue;
            var neutral = Brand.Edge;
            var add = MakeButton("Add Files", blue, 100);
            var addFolder = MakeButton("Add Folder", blue, 105);
            var remove = MakeButton("Remove", neutral, 85);
            var clear = MakeButton("Clear List", neutral, 95);
            var moveUp = MakeButton("Move Up", neutral, 90);
            var moveDown = MakeButton("Move Down", neutral, 100);
            var preview = MakeButton("Refresh Preview", blue, 130);
            renameButton.Text = "Rename Files"; Brand.StyleButton(renameButton, Brand.ElectricBlue, 125);
            renameButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            undoButton.Text = "Undo Last"; Brand.StyleButton(undoButton, neutral, 100);
            var savePreset = MakeButton("Save Preset", blue, 105);
            var loadPreset = MakeButton("Load Preset", blue, 105);
            var historyButton = MakeButton("History", neutral, 85);
            var exportButton = MakeButton("Export CSV", neutral, 100);
            cancelButton.Text = "Cancel"; Brand.StyleButton(cancelButton, neutral, 85);
            cancelButton.Enabled = false;
            actions.Controls.AddRange(new Control[] { add, addFolder, remove, clear, moveUp, moveDown, preview, renameButton, undoButton, savePreset, loadPreset, historyButton, exportButton, cancelButton });
            exportButton.Click += (s, e) => ExportPreview();
            tips.SetToolTip(exportButton, "Save the preview (folder, original name, proposed name, status) as a CSV file for Excel.");
            savePreset.Click += (s, e) => SavePreset();
            loadPreset.Click += (s, e) => LoadPreset();
            historyButton.Click += (s, e) => OpenHistory();
            cancelButton.Click += (s, e) => { cancelRequested = true; status.Text = "Cancellation requested; completing safe rollback..."; };
            tips.SetToolTip(moveUp, "Numbering follows the list order. Click the Original filename or Modified header to sort.");
            tips.SetToolTip(moveDown, "Numbering follows the list order. Click the Original filename or Modified header to sort.");

            grid.Dock = DockStyle.Fill;
            Brand.StyleGrid(grid);
            grid.BorderStyle = BorderStyle.None;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 34;
            grid.RowTemplate.Height = 29;
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "include", HeaderText = "Include", FillWeight = 22 });
            grid.Columns.Add("original", "Original filename");
            grid.Columns.Add("proposed", "Proposed filename");
            grid.Columns.Add("modified", "Modified");
            grid.Columns.Add("result", "Status");
            grid.Columns[ModifiedColumn].FillWeight = 45;
            grid.Columns[StatusColumn].FillWeight = 45;
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.ReadOnly = column.Index != IncludeColumn;
                column.SortMode = column.Index == OriginalColumn || column.Index == ModifiedColumn ? DataGridViewColumnSortMode.Programmatic : DataGridViewColumnSortMode.NotSortable;
            }
            grid.AllowDrop = true;
            grid.ContextMenuStrip = BuildGridMenu();

            // Status bar with the discreet company attribution on the right.
            var footer = new StatusStrip { SizingGrip = false, BackColor = Brand.PageGrey, ForeColor = Brand.MutedText };
            status.Spring = true; status.TextAlign = ContentAlignment.MiddleLeft; status.ForeColor = Brand.DeepNavy;
            progress.Size = new Size(160, 16); progress.Minimum = 0; progress.Maximum = 100;
            footer.Items.Add(status);
            footer.Items.Add(progress);
            footer.Items.Add(new ToolStripStatusLabel(Brand.ShortCopyright + " · " + Brand.Attribution) { ForeColor = Brand.MutedText });

            Controls.Add(grid);
            Controls.Add(footer);
            Controls.Add(actions);
            Controls.Add(settings);
            Controls.Add(header);
            Controls.Add(menu);

            add.Click += (s, e) => { using (var dlg = new OpenFileDialog { Multiselect = true, Title = "Choose files to rename" }) if (dlg.ShowDialog() == DialogResult.OK) AddFiles(dlg.FileNames); };
            addFolder.Click += (s, e) => { using (var dlg = new FolderBrowserDialog { Description = "Choose a folder to add" }) if (dlg.ShowDialog() == DialogResult.OK) AddFolder(dlg.SelectedPath); };
            remove.Click += (s, e) => RemoveSelected();
            clear.Click += (s, e) => { files.Clear(); ClearSortGlyph(); RefreshPreview(); };
            moveUp.Click += (s, e) => MoveSelected(-1);
            moveDown.Click += (s, e) => MoveSelected(1);
            preview.Click += (s, e) => RefreshPreview();
            renameButton.Click += (s, e) => RenameFiles();
            undoButton.Click += (s, e) => Undo();
            foreach (TextBox box in new[] { prefix, suffix, find, replace, newExtension }) box.TextChanged += (s, e) => RefreshPreview();
            extensionMode.SelectedIndexChanged += (s, e) => { newExtension.Enabled = extensionMode.SelectedIndex == 3; RefreshPreview(); };
            foreach (CheckBox box in new[] { numbering, matchCase, useRegex }) box.CheckedChanged += (s, e) => RefreshPreview();
            start.ValueChanged += (s, e) => RefreshPreview();
            digits.ValueChanged += (s, e) => RefreshPreview();
            casing.SelectedIndexChanged += (s, e) => RefreshPreview();
            DragEnter += DragEnterFiles; DragDrop += DropFiles;
            grid.DragEnter += DragEnterFiles; grid.DragDrop += DropFiles;
            grid.CellBeginEdit += (s, e) => { if (busy) e.Cancel = true; };
            grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                // Commit checkbox clicks immediately so the preview updates without leaving the cell.
                if (grid.IsCurrentCellDirty && grid.CurrentCell.ColumnIndex == IncludeColumn) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            grid.CellValueChanged += GridCellValueChanged;
            grid.ColumnHeaderMouseClick += (s, e) => { if (e.ColumnIndex == OriginalColumn || e.ColumnIndex == ModifiedColumn) SortFiles(e.ColumnIndex); };
            grid.CellMouseDown += (s, e) =>
            {
                // Right-clicking an unselected row selects it so the menu acts on what was clicked.
                if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && !grid.Rows[e.RowIndex].Selected) SelectRows(new[] { e.RowIndex });
            };
            grid.KeyDown += GridKeyDown;
            // Closing mid-operation would abandon files under temporary names.
            FormClosing += (s, e) =>
            {
                if (busy) { e.Cancel = true; status.Text = "Please wait for the current operation to finish."; return; }
                SaveAppSettings();
            };
            string restoreProblem = LoadAppSettings();
            UpdateButtons();
            status.Text = restoreProblem ?? "Ready — add files or drag files/folders into the window.";
        }

        void AddField(TableLayoutPanel panel, string title, Control field, int column, int row, int span)
        {
            var box = new Panel { Dock = DockStyle.Fill, Padding = new Padding(3, 0, 8, 0) };
            box.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 18, ForeColor = Brand.DeepNavy });
            field.Dock = DockStyle.Bottom;
            field.Height = 24;
            box.Controls.Add(field);
            panel.Controls.Add(box, column, row);
            panel.SetColumnSpan(box, span);
        }
        Button MakeButton(string label, Color color, int width)
        {
            var button = new Button { Text = label };
            Brand.StyleButton(button, color, width);
            return button;
        }
        ContextMenuStrip BuildGridMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Include selected", null, (s, e) => SetIncluded(SelectedIndices(), true));
            menu.Items.Add("Exclude selected", null, (s, e) => SetIncluded(SelectedIndices(), false));
            menu.Items.Add("Include all", null, (s, e) => SetIncluded(Enumerable.Range(0, files.Count), true));
            menu.Items.Add("Exclude all", null, (s, e) => SetIncluded(Enumerable.Range(0, files.Count), false));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Move up", null, (s, e) => MoveSelected(-1));
            menu.Items.Add("Move down", null, (s, e) => MoveSelected(1));
            menu.Items.Add("Remove from list", null, (s, e) => RemoveSelected());
            menu.Opening += (s, e) => { if (busy || files.Count == 0) e.Cancel = true; };
            return menu;
        }
        void DragEnterFiles(object sender, DragEventArgs e)
        {
            e.Effect = !busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }
        void DropFiles(object sender, DragEventArgs e)
        {
            if (busy) return;
            var dropped = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (dropped == null) return;
            try
            {
                AddFiles(dropped.SelectMany(p => File.Exists(p) ? new[] { p } : Directory.Exists(p) ? GetFolderFiles(p) : new string[0]));
            }
            catch (Exception ex) { MessageBox.Show("Unable to add dropped items: " + ex.Message); }
        }
        void AddFiles(IEnumerable<string> paths)
        {
            var existing = new HashSet<string>(files.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
            var patterns = GetPatterns();
            int before = files.Count;
            foreach (var p in paths)
            {
                var full = Path.GetFullPath(p);
                if (File.Exists(full) && MatchesFilter(Path.GetFileName(full), patterns) && existing.Add(full)) files.Add(FileEntry.FromPath(full));
            }
            if (files.Count != before) ClearSortGlyph();
            RefreshPreview();
        }
        void AddFolder(string folder)
        {
            try { AddFiles(GetFolderFiles(folder)); }
            catch (Exception ex) { MessageBox.Show("Unable to read folder: " + ex.Message); }
        }
        IEnumerable<string> GetFolderFiles(string folder)
        {
            // Enumerate only files; don't follow reparse-point subdirectories.
            var results = new List<string>();
            var pending = new Stack<string>();
            pending.Push(folder);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                try { results.AddRange(Directory.GetFiles(current)); }
                catch (Exception ex) { status.Text = "Skipped unreadable folder: " + ex.Message; }
                if (!recursive.Checked) continue;
                try
                {
                    foreach (string sub in Directory.GetDirectories(current))
                    {
                        if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) == 0) pending.Push(sub);
                    }
                }
                catch (Exception ex) { status.Text = "Skipped subfolders: " + ex.Message; }
            }
            return results;
        }
        string[] GetPatterns()
        {
            var patterns = filter.Text.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            return patterns.Length == 0 ? new[] { "*.*" } : patterns;
        }
        bool MatchesFilter(string name, string[] patterns)
        {
            foreach (string pattern in patterns)
            {
                if (pattern == "*" || pattern == "*.*") return true;
                string regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                    .Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
                if (System.Text.RegularExpressions.Regex.IsMatch(name, regex,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return true;
            }
            return false;
        }

        // ---- List selection and ordering ----

        List<int> SelectedIndices()
        {
            return grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Index).Where(i => i < files.Count).OrderBy(i => i).ToList();
        }
        void SelectRows(IEnumerable<int> indices)
        {
            var list = indices.Where(i => i >= 0 && i < grid.Rows.Count).ToList();
            if (list.Count == 0) return;
            grid.CurrentCell = grid.Rows[list[0]].Cells[OriginalColumn];
            grid.ClearSelection();
            foreach (int i in list) grid.Rows[i].Selected = true;
        }
        void SetIncluded(IEnumerable<int> indices, bool included)
        {
            if (busy) return;
            foreach (int i in indices.ToList()) files[i].Included = included;
            RefreshPreview();
        }
        void MoveSelected(int direction)
        {
            if (busy) return;
            var selected = SelectedIndices();
            if (selected.Count == 0) return;
            if (direction < 0 ? selected[0] == 0 : selected[selected.Count - 1] == files.Count - 1) return;
            if (direction > 0) selected.Reverse();
            foreach (int i in selected)
            {
                var item = files[i];
                files[i] = files[i + direction];
                files[i + direction] = item;
            }
            ClearSortGlyph();
            RefreshPreview();
            SelectRows(selected.Select(i => i + direction).OrderBy(i => i));
        }
        void RemoveSelected()
        {
            if (busy) return;
            var selected = SelectedIndices();
            if (selected.Count == 0) return;
            for (int i = selected.Count - 1; i >= 0; i--) files.RemoveAt(selected[i]);
            RefreshPreview();
            if (files.Count > 0) SelectRows(new[] { Math.Min(selected[0], files.Count - 1) });
        }
        void SortFiles(int column)
        {
            if (busy || files.Count == 0) return;
            sortDescending = sortColumn == column && !sortDescending;
            sortColumn = column;
            Comparison<FileEntry> compare;
            if (column == OriginalColumn) compare = (a, b) => StrCmpLogicalW(Path.GetFileName(a.Path), Path.GetFileName(b.Path));
            else compare = (a, b) => a.Modified.CompareTo(b.Modified);
            var comparer = Comparer<FileEntry>.Create(compare);
            var sorted = sortDescending ? files.OrderByDescending(f => f, comparer).ToList() : files.OrderBy(f => f, comparer).ToList();
            files.Clear();
            files.AddRange(sorted);
            foreach (DataGridViewColumn c in grid.Columns) c.HeaderCell.SortGlyphDirection = SortOrder.None;
            grid.Columns[column].HeaderCell.SortGlyphDirection = sortDescending ? SortOrder.Descending : SortOrder.Ascending;
            RefreshPreview();
        }
        void ClearSortGlyph()
        {
            sortColumn = -1;
            foreach (DataGridViewColumn c in grid.Columns) c.HeaderCell.SortGlyphDirection = SortOrder.None;
        }
        void GridCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (updatingGrid || e.ColumnIndex != IncludeColumn || e.RowIndex < 0 || e.RowIndex >= files.Count) return;
            files[e.RowIndex].Included = Equals(grid.Rows[e.RowIndex].Cells[IncludeColumn].Value, true);
            // Refresh after the grid finishes its own event handling.
            BeginInvoke((Action)RefreshPreview);
        }
        void GridKeyDown(object sender, KeyEventArgs e)
        {
            if (busy) return;
            if (e.KeyCode == Keys.Space && grid.SelectedRows.Count > 0)
            {
                var selected = SelectedIndices();
                SetIncluded(selected, !selected.All(i => files[i].Included));
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Delete) { RemoveSelected(); e.Handled = e.SuppressKeyPress = true; }
            else if (e.Alt && e.KeyCode == Keys.Up) { MoveSelected(-1); e.Handled = e.SuppressKeyPress = true; }
            else if (e.Alt && e.KeyCode == Keys.Down) { MoveSelected(1); e.Handled = e.SuppressKeyPress = true; }
        }

        // ---- Presets and remembered settings ----

        XElement SettingsXml(string rootName)
        {
            return new XElement(rootName, new XAttribute("version", "3"),
                new XElement("Prefix", prefix.Text), new XElement("Suffix", suffix.Text),
                new XElement("Find", find.Text), new XElement("Replace", replace.Text),
                new XElement("MatchCase", matchCase.Checked), new XElement("UseRegex", useRegex.Checked),
                new XElement("Numbering", numbering.Checked), new XElement("Start", start.Value),
                new XElement("Digits", digits.Value), new XElement("Case", casing.SelectedIndex),
                new XElement("ExtensionMode", extensionMode.SelectedIndex), new XElement("NewExtension", newExtension.Text),
                new XElement("Filter", filter.Text), new XElement("Recursive", recursive.Checked));
        }
        void ApplySettingsXml(XElement root)
        {
            Func<string, string> read = name => (string)root.Element(name) ?? "";
            // Stage 3 presets have no MatchCase/UseRegex; keep Stage 3 behaviour for them.
            Func<string, bool, bool> readFlag = (name, fallback) => root.Element(name) == null ? fallback : bool.Parse(read(name));
            decimal startValue = decimal.Parse(read("Start"), System.Globalization.CultureInfo.InvariantCulture);
            decimal digitValue = decimal.Parse(read("Digits"), System.Globalization.CultureInfo.InvariantCulture);
            int caseIndex = int.Parse(read("Case"));
            bool num = bool.Parse(read("Numbering"));
            bool recurse = bool.Parse(read("Recursive"));
            bool caseSensitive = readFlag("MatchCase", true);
            bool regex = readFlag("UseRegex", false);
            // Stage 4 and earlier presets have no extension settings: keep the extension.
            int extensionIndex = root.Element("ExtensionMode") == null ? 0 : int.Parse(read("ExtensionMode"));
            if (startValue < start.Minimum || startValue > start.Maximum || digitValue < digits.Minimum || digitValue > digits.Maximum || caseIndex < 0 || caseIndex >= casing.Items.Count
                || extensionIndex < 0 || extensionIndex >= extensionMode.Items.Count)
                throw new InvalidDataException("Preset contains out-of-range values.");
            prefix.Text = read("Prefix"); suffix.Text = read("Suffix");
            find.Text = read("Find"); replace.Text = read("Replace");
            matchCase.Checked = caseSensitive; useRegex.Checked = regex;
            extensionMode.SelectedIndex = extensionIndex; newExtension.Text = read("NewExtension");
            numbering.Checked = num; start.Value = startValue; digits.Value = digitValue;
            casing.SelectedIndex = caseIndex; filter.Text = read("Filter"); recursive.Checked = recurse;
        }
        void SavePreset()
        {
            using (var dialog = new SaveFileDialog { Title = "Save rename preset", Filter = "HT Rename Preset (*.htpreset)|*.htpreset", DefaultExt = "htpreset" })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                try
                {
                    new XDocument(SettingsXml("HTRenamePreset")).Save(dialog.FileName);
                    status.Text = "Preset saved: " + dialog.FileName;
                }
                catch (Exception ex) { MessageBox.Show("Could not save preset: " + ex.Message); }
            }
        }
        void LoadPreset()
        {
            using (var dialog = new OpenFileDialog { Title = "Load rename preset", Filter = "HT Rename Preset (*.htpreset)|*.htpreset" })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                try
                {
                    var root = XDocument.Load(dialog.FileName).Root;
                    if (root == null || root.Name != "HTRenamePreset") throw new InvalidDataException("Invalid preset file.");
                    ApplySettingsXml(root);
                    RefreshPreview(); status.Text = "Preset loaded: " + dialog.FileName;
                }
                catch (Exception ex) { MessageBox.Show("Could not load preset: " + ex.Message); }
            }
        }
        // Returns null on success, or a message for the status bar.
        string LoadAppSettings()
        {
            if (!File.Exists(settingsPath)) return null;
            try
            {
                var root = XDocument.Load(settingsPath).Root;
                if (root == null) return null;
                ApplySettingsXml(root);
                var window = root.Element("Window");
                if (window != null)
                {
                    var bounds = new Rectangle((int)window.Attribute("x"), (int)window.Attribute("y"), (int)window.Attribute("width"), (int)window.Attribute("height"));
                    // Skip bounds from a monitor that is no longer connected.
                    if (bounds.Width >= MinimumSize.Width && bounds.Height >= MinimumSize.Height && Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(bounds)))
                    {
                        StartPosition = FormStartPosition.Manual;
                        Bounds = bounds;
                    }
                    if ((bool?)window.Attribute("maximized") == true) WindowState = FormWindowState.Maximized;
                }
                return null;
            }
            catch (Exception ex) { return "Last-used settings could not be restored: " + ex.Message; }
        }
        void SaveAppSettings()
        {
            try
            {
                var root = SettingsXml("HTRenamerSettings");
                var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                root.Add(new XElement("Window", new XAttribute("x", bounds.X), new XAttribute("y", bounds.Y),
                    new XAttribute("width", bounds.Width), new XAttribute("height", bounds.Height),
                    new XAttribute("maximized", WindowState == FormWindowState.Maximized)));
                Directory.CreateDirectory(RenameHistory.DataDirectory);
                new XDocument(root).Save(settingsPath);
            }
            catch (Exception) { /* Closing; nowhere useful to report this. */ }
        }

        // ---- Preview and renaming ----

        RenameOptions CurrentOptions()
        {
            return new RenameOptions
            {
                Prefix = prefix.Text, Suffix = suffix.Text, Find = find.Text, Replace = replace.Text,
                MatchCase = matchCase.Checked, UseRegex = useRegex.Checked,
                Numbering = numbering.Checked, Start = (long)start.Value, Digits = (int)digits.Value, Case = casing.SelectedIndex,
                ExtensionMode = extensionMode.SelectedIndex, NewExtension = newExtension.Text
            };
        }
        // Status text shared by the grid and the CSV export.
        static string RowStatus(FileEntry file, Tuple<string, string> move, bool usesTaken)
        {
            if (!file.Included) return "Excluded";
            if (move == null) return "—";
            if (string.Equals(move.Item1, move.Item2, StringComparison.Ordinal)) return "Unchanged";
            return usesTaken && file.Taken == null ? "Pending (no photo date)" : "Pending";
        }
        void ExportPreview()
        {
            if (busy) return;
            if (files.Count == 0) { status.Text = "Nothing to export — add files first."; return; }
            var options = CurrentOptions();
            List<Tuple<string, string>> moves;
            try { moves = RenameEngine.PlanMoves(files, options); }
            catch (Exception ex) { MessageBox.Show("Invalid rename settings: " + ex.Message, "Export CSV", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            bool usesTaken = RenameEngine.UsesToken(options, "taken");
            var rows = new List<string[]> { new[] { "Include", "Folder", "Original filename", "Proposed filename", "Modified", "Status" } };
            for (int i = 0; i < files.Count; i++)
                rows.Add(new[] { files[i].Included ? "Yes" : "No", Path.GetDirectoryName(files[i].Path), Path.GetFileName(files[i].Path),
                    Path.GetFileName(moves[i].Item2), files[i].Modified.ToString("yyyy-MM-dd HH:mm"), RowStatus(files[i], moves[i], usesTaken) });
            using (var dialog = new SaveFileDialog { Title = "Export preview", Filter = "CSV file (*.csv)|*.csv", DefaultExt = "csv", FileName = "rename-preview.csv" })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                try
                {
                    // UTF-8 with BOM so Excel shows non-English filenames correctly.
                    File.WriteAllText(dialog.FileName, RenameEngine.ToCsv(rows), new System.Text.UTF8Encoding(true));
                    status.Text = "Preview exported: " + dialog.FileName;
                }
                catch (Exception ex) { MessageBox.Show("Could not export preview: " + ex.Message, "Export CSV", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
        void OpenHistory()
        {
            using (var form = new HistoryForm((batch, owner) => UndoMoves(batch.Moves, batch.Id, owner)))
                form.ShowDialog(this);
        }
        void RefreshPreview()
        {
            if (busy) return;
            var options = CurrentOptions();
            List<Tuple<string, string>> moves = null;
            string settingsError = null;
            try { moves = RenameEngine.PlanMoves(files, options); }
            catch (Exception ex) { settingsError = "Invalid rename settings: " + ex.Message; }
            string problem = moves == null ? null : RenameEngine.Validate(moves);
            bool usesTaken = RenameEngine.UsesToken(options, "taken");
            updatingGrid = true;
            try
            {
                grid.EndEdit();
                // Update rows in place when the count is unchanged so scroll position and selection survive typing.
                if (grid.Rows.Count != files.Count)
                {
                    grid.Rows.Clear();
                    if (files.Count > 0) grid.Rows.Add(files.Count);
                }
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    var row = grid.Rows[i];
                    row.Cells[IncludeColumn].Value = file.Included;
                    row.Cells[OriginalColumn].Value = Path.GetFileName(file.Path);
                    row.Cells[ProposedColumn].Value = moves == null ? "" : Path.GetFileName(moves[i].Item2);
                    row.Cells[ModifiedColumn].Value = file.Modified.ToString("yyyy-MM-dd HH:mm");
                    row.Cells[StatusColumn].Value = RowStatus(file, moves == null ? null : moves[i], usesTaken);
                    row.DefaultCellStyle.ForeColor = file.Included ? Brand.DeepNavy : Brand.MutedText;
                }
            }
            finally { updatingGrid = false; }
            int included = files.Count(f => f.Included);
            int changes = moves == null ? 0 : moves.Count(m => m.Item1 != m.Item2);
            string numberingText = RenameEngine.UsesNumberToken(options) ? "{n}" : numbering.Checked ? "ON" : "OFF";
            status.Text = settingsError ?? (problem == null
                ? files.Count + " file(s) loaded, " + included + " included — " + changes + " change(s) previewed. Numbering: " + numberingText
                : "Check: " + problem);
            UpdateButtons();
            renameButton.Enabled = moves != null && problem == null && changes > 0;
        }
        // Lock every input while files are being moved; Application.DoEvents keeps the UI live.
        void UpdateButtons()
        {
            foreach (Control c in actions.Controls) c.Enabled = !busy;
            settings.Enabled = !busy;
            undoButton.Enabled = !busy && undoBatch.Count > 0;
            cancelButton.Enabled = busy;
        }

        // Runs RenameExecutor with the window locked, the progress bar updating and Cancel live.
        bool ApplyMoves(List<Tuple<string, string>> moves, out string error)
        {
            cancelRequested = false;
            busy = true;
            UpdateButtons();
            progress.Value = 0;
            progress.Maximum = Math.Max(1, moves.Count * 2);
            try
            {
                return RenameExecutor.Apply(moves, () => cancelRequested,
                    done => { progress.Value = Math.Min(done, progress.Maximum); Application.DoEvents(); }, out error);
            }
            finally
            {
                busy = false;
                cancelRequested = false;
                progress.Value = 0;
                UpdateButtons();
            }
        }
        void RenameFiles()
        {
            if (busy) return;
            List<Tuple<string, string>> all;
            try { all = RenameEngine.PlanMoves(files, CurrentOptions()); }
            catch (Exception ex) { MessageBox.Show("Invalid rename settings: " + ex.Message, "Cannot rename", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            var problem = RenameEngine.Validate(all);
            if (problem != null) { MessageBox.Show(problem, "Cannot rename", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            var moves = all.Where(m => m.Item1 != m.Item2).ToList();
            if (moves.Count == 0) return;
            if (MessageBox.Show("Rename " + moves.Count + " file(s)?\n\nReview the preview before continuing.", "Confirm batch rename", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string error;
            if (!ApplyMoves(moves, out error)) { MessageBox.Show(error, "Rename error", MessageBoxButtons.OK, MessageBoxIcon.Error); RefreshPreview(); return; }
            string batchId = RenameHistory.NewBatchId();
            undoBatch.Clear(); undoBatch.AddRange(moves); undoBatchId = batchId;
            string historyWarning = RenameHistory.Append("RENAME", moves, batchId, null);
            for (int i = 0; i < files.Count; i++) files[i].Path = all[i].Item2;
            RefreshPreview();
            status.Text = "Renamed " + moves.Count + " file(s) successfully. Undo Last, or undo it later from History."
                + (historyWarning == null ? "" : " Warning: " + historyWarning);
        }
        void Undo()
        {
            if (undoBatch.Count > 0) UndoMoves(undoBatch.ToList(), undoBatchId, this);
        }
        // Reverses one rename batch, from this session or from history. Returns true when files were restored.
        bool UndoMoves(List<Tuple<string, string>> moves, string batchId, IWin32Window owner)
        {
            if (busy || moves.Count == 0) return false;
            var reverse = moves.Select(m => Tuple.Create(m.Item2, m.Item1)).ToList();
            string problem = RenameEngine.Validate(reverse);
            if (problem != null) { MessageBox.Show(owner, "Undo cannot proceed: " + problem, "Undo", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            string error;
            if (!ApplyMoves(reverse, out error)) { MessageBox.Show(owner, "Undo failed: " + error, "Undo", MessageBoxButtons.OK, MessageBoxIcon.Error); RefreshPreview(); return false; }
            var restored = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in moves) restored[m.Item2] = m.Item1;
            foreach (var file in files)
            {
                string original;
                if (restored.TryGetValue(file.Path, out original)) file.Path = original;
            }
            string historyWarning = RenameHistory.Append("UNDO", reverse, RenameHistory.NewBatchId(), batchId);
            if (batchId != null && batchId == undoBatchId) { undoBatch.Clear(); undoBatchId = null; }
            RefreshPreview();
            status.Text = "Undo complete — " + moves.Count + " filename(s) restored." + (historyWarning == null ? "" : " Warning: " + historyWarning);
            return true;
        }
    }
}
