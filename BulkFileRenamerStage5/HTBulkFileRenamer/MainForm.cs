using HTREASURE.Branding;
using HTREASURE.Branding.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using System.Windows.Forms;
using HTREASURE.Standards.Updates;
using HTREASURE.Standards.WinForms;

namespace HTBulkFileRenamer
{
    public class MainForm : Form
    {
        internal const string WebsiteUrl = "https://www.htreasure.com";
        /// <summary>Advisory history and update service, configured from the values the build compiled in.</summary>
        internal static readonly Lazy<HTStandardsRuntime> Standards = new Lazy<HTStandardsRuntime>(() => HTStandardsRuntime.FromAssembly(typeof(MainForm).Assembly));

        readonly List<FileEntry> files = new List<FileEntry>();
        readonly List<Tuple<string, string>> undoBatch = new List<Tuple<string, string>>();
        string undoBatchId;
        readonly DataGridView grid = new DataGridView();
        readonly TextBox prefix = new TextBox(), suffix = new TextBox(), find = new TextBox(), replace = new TextBox();
        readonly CheckBox numbering = new CheckBox(), recursive = new CheckBox(), matchCase = new CheckBox(), useRegex = new CheckBox();
        readonly TextBox filter = new TextBox();
        readonly ToolStripProgressBar progress = new ToolStripProgressBar();
        bool cancelRequested = false, busy = false, updatingGrid = false;
        int sortColumn = -1;
        bool sortDescending = false;
        readonly string settingsPath = Path.Combine(RenameHistory.DataDirectory, "settings.xml");
        readonly NumericUpDown start = new NumericUpDown(), digits = new NumericUpDown();
        readonly ComboBox casing = new ComboBox(), extensionMode = new ComboBox();
        readonly TextBox newExtension = new TextBox();
        readonly ToolStripStatusLabel status = new ToolStripStatusLabel(), statusRight = new ToolStripStatusLabel(), attribution = new ToolStripStatusLabel();
        StatusKind statusKind = StatusKind.Neutral;
        readonly ToolStripMenuItem renameItem, undoItem, cancelItem;
        readonly Button renameButton = new Button(), cancelButton = new Button(), undoButton = new Button();
        // Menu commands that are disabled while files are being moved.
        readonly List<ToolStripMenuItem> commandItems = new List<ToolStripMenuItem>();
        // Inputs and buttons that are disabled while files are being moved.
        readonly List<Control> lockedWhileBusy = new List<Control>();
        readonly Dictionary<AppTheme, ToolStripMenuItem> themeItems = new Dictionary<AppTheme, ToolStripMenuItem>();
        AppTheme theme = AppTheme.System;
        readonly ToolTip tips = new ToolTip { AutoPopDelay = 15000 };

        const int IncludeColumn = 0, OriginalColumn = 1, ProposedColumn = 2, ModifiedColumn = 3, StatusColumn = 4;

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        static extern int StrCmpLogicalW(string a, string b);

        // Layout follows the HTREASURE design template used by HT ISO Ripper: blue header banner, menu bar,
        // captioned group boxes with a label column, one accent primary button in the bottom action row.
        public MainForm()
        {
            Text = BrandInfo.ProductName;
            BrandWinForms.ApplyIcon(this);
            Font = new Font("Segoe UI", 9F);
            // Pixel sizes below are at 96 DPI; scale them on high-DPI screens (PerMonitorV2, as in the template).
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            Size = new Size(1040, 780);
            MinimumSize = new Size(900, 700);
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;

            // ---- Header banner
            var header = new Panel { Dock = DockStyle.Top, Height = 44, Tag = ThemeManager.HeaderTag };
            header.Controls.Add(new PictureBox { Image = BrandWinForms.ProductImage, SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(16, 4, 36, 36) });
            header.Controls.Add(new Label { Text = BrandInfo.ProductName, AutoSize = true, Location = new Point(60, 1), Font = new Font("Segoe UI Semibold", 14.25F, FontStyle.Bold), UseMnemonic = false });
            header.Controls.Add(new Label { Text = BrandInfo.ProductDescription + " Add files to begin.", AutoSize = true, Location = new Point(62, 27), Tag = ThemeManager.DimTag, UseMnemonic = false });
            var website = new LinkLabel { Text = "www.htreasure.com", TextAlign = ContentAlignment.MiddleRight, Size = new Size(160, 18), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            website.Location = new Point(header.Width - website.Width - 16, 13);
            website.LinkClicked += (s, e) => OpenWebsite(this);
            tips.SetToolTip(website, "Open the HTREASURE LTD website (" + WebsiteUrl + ")");
            header.Controls.Add(website);

            // ---- Menus. Shortcuts avoid keys the text boxes need (Del, Ctrl+Z, Alt+arrows);
            // Del and Alt+Up/Down are handled by the file list itself (GridKeyDown) and only shown here.
            var menu = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("&File");
            Command(fileMenu, "Add &Files...", AddFilesDialog, Keys.Control | Keys.O);
            Command(fileMenu, "Add F&older...", AddFolderDialog, Keys.Control | Keys.Shift | Keys.O);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            Command(fileMenu, "&Load Preset...", LoadPreset);
            Command(fileMenu, "&Save Preset...", SavePreset, Keys.Control | Keys.S);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            Command(fileMenu, "&Export Preview to CSV...", ExportPreview, Keys.Control | Keys.E,
                tip: "Save the preview (folder, original name, proposed name, status) as a CSV file for Excel.");
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("E&xit", null, (s, e) => Close());

            var listMenu = new ToolStripMenuItem("&List");
            Command(listMenu, "&Remove Selected", RemoveSelected, keyText: "Del");
            Command(listMenu, "&Clear List", ClearList);
            listMenu.DropDownItems.Add(new ToolStripSeparator());
            const string orderTip = "Numbering follows the list order. Click the Original filename or Modified header to sort.";
            Command(listMenu, "Move &Up", () => MoveSelected(-1), keyText: "Alt+Up", tip: orderTip);
            Command(listMenu, "Move &Down", () => MoveSelected(1), keyText: "Alt+Down", tip: orderTip);
            listMenu.DropDownItems.Add(new ToolStripSeparator());
            Command(listMenu, "&Include All", () => SetIncluded(Enumerable.Range(0, files.Count), true));
            Command(listMenu, "E&xclude All", () => SetIncluded(Enumerable.Range(0, files.Count), false));

            var renameMenu = new ToolStripMenuItem("&Rename");
            Command(renameMenu, "Refresh &Preview", RefreshPreview, Keys.F5);
            renameItem = Command(renameMenu, "&Rename Files...", RenameFiles, Keys.Control | Keys.R);
            undoItem = Command(renameMenu, "&Undo Last Rename", Undo);
            cancelItem = Command(renameMenu, "&Cancel Rename", RequestCancel);
            renameMenu.DropDownItems.Add(new ToolStripSeparator());
            Command(renameMenu, "&History...", OpenHistory, Keys.Control | Keys.H);

            var viewMenu = new ToolStripMenuItem("&View");
            var themeMenu = new ToolStripMenuItem("&Theme");
            foreach (var choice in new[] { Tuple.Create(AppTheme.System, "Same as &Windows"), Tuple.Create(AppTheme.Light, "&Light"), Tuple.Create(AppTheme.Dark, "&Dark") })
            {
                var value = choice.Item1;
                var item = new ToolStripMenuItem(choice.Item2, null, (s, e) => SetTheme(value));
                themeItems[value] = item;
                themeMenu.DropDownItems.Add(item);
            }
            viewMenu.DropDownItems.Add(themeMenu);

            var toolsMenu = new ToolStripMenuItem("&Tools");
            toolsMenu.DropDownItems.Add("&Open Data Folder", null, (s, e) => OpenDataFolder());
            toolsMenu.DropDownItems[0].ToolTipText = "Settings, saved presets and the rename history (history.tsv).";

            // Mandatory HTREASURE standard: Help → About → Security Advisory History, and Help → Security Advisories.
            var helpMenu = new ToolStripMenuItem("&Help");
            helpMenu.DropDownItems.Add(SecurityAdvisoryUi.CreateAboutMenu(this, ShowAbout, () => Standards.Value));
            helpMenu.DropDownItems.Add(SecurityAdvisoryUi.CreateAdvisoriesMenuItem(this, () => Standards.Value));
            helpMenu.DropDownItems.Add(new ToolStripSeparator());
            helpMenu.DropDownItems.Add("&Check for Updates", null, (s, e) => CheckForUpdates());
            helpMenu.DropDownItems.Add("&Licence Agreement", null, (s, e) => ShowLicence(this));
            menu.Items.AddRange(new ToolStripItem[] { fileMenu, listMenu, renameMenu, viewMenu, toolsMenu, helpMenu });
            MainMenuStrip = menu;

            // ---- Name Rules
            var rules = Group("Name Rules", 4);
            var rulesTable = Table(rules, 4, 4);
            Row(rulesTable, 0, "Prefix:", prefix, "Suffix:", suffix);
            Row(rulesTable, 1, "Find:", find, "Replace with:", replace);
            matchCase.Text = "Match case"; matchCase.AutoSize = true; matchCase.Checked = true;
            useRegex.Text = "Use regular expression"; useRegex.AutoSize = true; useRegex.Margin = new Padding(16, 3, 3, 3);
            var findOptions = Flow(matchCase, useRegex);
            rulesTable.Controls.Add(findOptions, 3, 2);
            var tokenHint = new Label { Text = "Tokens for prefix and suffix: {n} {date} {created} {taken} {parent}, e.g. {taken:yyyyMMdd}_  (hover for details)", AutoSize = true, Tag = ThemeManager.DimTag, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 3) };
            rulesTable.Controls.Add(tokenHint, 0, 3); rulesTable.SetColumnSpan(tokenHint, 4);
            foreach (Control c in new Control[] { prefix, suffix, tokenHint }) tips.SetToolTip(c, RenameEngine.TokenHelp);
            tips.SetToolTip(useRegex, "Find is a .NET regular expression; use $1, $2 in Replace for captured groups.");

            // ---- Numbering, Case and Extension
            var format = Group("Numbering, Case and Extension", 2);
            var formatTable = Table(format, 6, 2);
            numbering.Text = "Add number at start (001_)"; numbering.AutoSize = true; numbering.Checked = true;
            tips.SetToolTip(numbering, "Adds the number at the start. Put {n} in the prefix or suffix to place it yourself.");
            start.Minimum = 0; start.Maximum = 999999; start.Value = 1; start.Width = 90;
            digits.Minimum = 1; digits.Maximum = 8; digits.Value = 3; digits.Width = 60;
            formatTable.Controls.Add(Caption("Numbering:"), 0, 0);
            formatTable.Controls.Add(Flow(numbering), 1, 0);
            formatTable.Controls.Add(Caption("Start at:"), 2, 0);
            formatTable.Controls.Add(Flow(start, Caption("Digits:"), digits), 3, 0);
            formatTable.SetColumnSpan(formatTable.GetControlFromPosition(3, 0), 3);
            casing.DropDownStyle = ComboBoxStyle.DropDownList;
            casing.Items.AddRange(new object[] { "Keep original", "lowercase", "UPPERCASE", "Title Case" });
            casing.SelectedIndex = 0;
            extensionMode.DropDownStyle = ComboBoxStyle.DropDownList;
            extensionMode.Items.AddRange(new object[] { "Keep original", "lowercase", "UPPERCASE", "Replace with..." });
            extensionMode.SelectedIndex = 0;
            newExtension.Enabled = false; newExtension.Width = 90;
            tips.SetToolTip(newExtension, "New extension, with or without the dot. Leave empty to remove the extension.");
            Row(formatTable, 1, "Filename case:", casing, "Extension:", null);
            extensionMode.Width = 150;
            formatTable.Controls.Add(Flow(extensionMode, newExtension, new Label { Text = "Only this setting changes the extension.", AutoSize = true, Tag = ThemeManager.DimTag, Margin = new Padding(6, 7, 3, 3) }), 3, 1);
            formatTable.SetColumnSpan(formatTable.GetControlFromPosition(3, 1), 3);

            // ---- Files: filters, add buttons and the preview grid
            var filesGroup = new GroupBox { Text = "Files", Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 10) };
            var filesBar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 36, ColumnCount = 2 };
            filesBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            filesBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filter.Width = 150; filter.Text = "*.*";
            recursive.Text = "Include subfolders"; recursive.AutoSize = true; recursive.Margin = new Padding(16, 6, 3, 3);
            tips.SetToolTip(filter, "Only add files matching these patterns, e.g. *.jpg;*.png. Applies to files added from now on.");
            tips.SetToolTip(recursive, "When adding a folder, also add files from its subfolders.");
            filesBar.Controls.Add(Flow(Caption("Filters:"), filter, recursive), 0, 0);
            var addFiles = MakeButton("Add Files...", AddFilesDialog, 100);
            var addFolder = MakeButton("Add Folder...", AddFolderDialog, 100);
            var clear = MakeButton("Clear List", ClearList, 90);
            filesBar.Controls.Add(Flow(addFiles, addFolder, clear), 1, 0);

            grid.Dock = DockStyle.Fill;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 26;
            grid.RowTemplate.Height = 24;
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "include", HeaderText = "Include", FillWeight = 22 });
            grid.Columns.Add("original", "Original filename");
            grid.Columns.Add("proposed", "Proposed filename");
            grid.Columns.Add("modified", "Modified");
            grid.Columns.Add("result", "Status");
            grid.Columns[ModifiedColumn].FillWeight = 45;
            grid.Columns[StatusColumn].FillWeight = 50;
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.ReadOnly = column.Index != IncludeColumn;
                column.SortMode = column.Index == OriginalColumn || column.Index == ModifiedColumn ? DataGridViewColumnSortMode.Programmatic : DataGridViewColumnSortMode.NotSortable;
            }
            grid.AllowDrop = true;
            grid.ContextMenuStrip = BuildGridMenu();
            var gridSpacer = new Panel { Dock = DockStyle.Top, Height = 4 };
            filesGroup.Controls.Add(grid);
            filesGroup.Controls.Add(gridSpacer);
            filesGroup.Controls.Add(filesBar);

            // ---- Bottom action row: note on the left, primary action first on the right
            var actionRow = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 46, ColumnCount = 2, Padding = new Padding(0, 6, 0, 4) };
            actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actionRow.Controls.Add(new Label { Text = "Nothing is renamed until you confirm. Renames can be undone.", AutoSize = true, Tag = ThemeManager.DimTag, Anchor = AnchorStyles.Left }, 0, 0);
            renameButton.Text = "Rename Files"; renameButton.Size = new Size(115, 32); renameButton.Tag = ThemeManager.PrimaryTag;
            renameButton.Click += (s, e) => RenameFiles();
            cancelButton.Text = "Cancel"; cancelButton.Size = new Size(92, 32); cancelButton.Click += (s, e) => RequestCancel();
            undoButton.Text = "Undo Last Rename"; undoButton.Size = new Size(130, 32); undoButton.Click += (s, e) => Undo();
            var closeButton = new Button { Text = "Close", Size = new Size(92, 32) };
            closeButton.Click += (s, e) => Close();
            actionRow.Controls.Add(Flow(renameButton, cancelButton, undoButton, closeButton), 1, 0);
            lockedWhileBusy.AddRange(new Control[] { rules, format, filesBar, closeButton });

            var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 0) };
            content.Controls.Add(filesGroup);
            content.Controls.Add(format);
            content.Controls.Add(rules);
            content.Controls.Add(actionRow);

            // ---- Status strip: state on the left (coloured by kind), file counts and progress on the right
            var statusStrip = new StatusStrip { SizingGrip = false };
            status.Spring = true; status.TextAlign = ContentAlignment.MiddleLeft;
            progress.Size = new Size(140, 16); progress.Minimum = 0; progress.Maximum = 100; progress.Visible = false;
            statusStrip.Items.Add(status);
            statusStrip.Items.Add(statusRight);
            statusStrip.Items.Add(progress);
            // Brand guideline: discreet company attribution in the status bar.
            attribution.Text = BrandInfo.ShortCopyright + " · " + BrandInfo.Attribution;
            statusStrip.Items.Add(attribution);

            Controls.Add(content);
            Controls.Add(statusStrip);
            Controls.Add(menu);
            Controls.Add(header);

            foreach (TextBox box in new[] { prefix, suffix, find, replace, newExtension }) box.TextChanged += (s, e) => RefreshPreview();
            extensionMode.SelectedIndexChanged += (s, e) =>
            {
                newExtension.Enabled = extensionMode.SelectedIndex == 3;
                ThemeManager.Restyle(newExtension);
                RefreshPreview();
            };
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
                if (busy) { e.Cancel = true; SetStatus("Please wait for the current rename to finish, or press Cancel.", StatusKind.Warning); return; }
                SaveAppSettings();
            };
            // "Same as Windows" follows a light/dark switch made while the app is open.
            Microsoft.Win32.UserPreferenceChangedEventHandler preferenceChanged = (s, e) =>
            {
                if (theme == AppTheme.System && e.Category == Microsoft.Win32.UserPreferenceCategory.General && IsHandleCreated)
                    BeginInvoke((Action)ApplyTheme);
            };
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += preferenceChanged;
            // SystemEvents is static: unsubscribe or the closed form stays alive.
            FormClosed += (s, e) => Microsoft.Win32.SystemEvents.UserPreferenceChanged -= preferenceChanged;
            // HTREASURE standard: check for new security advisories once the window is open (silent when offline or
            // when no advisory service is configured; never installs anything).
            Shown += async (s, e) =>
            {
                try { await SecurityAdvisoryUi.CheckInBackgroundAsync(this, Standards.Value); }
                catch (Exception ex) { SetStatus("Security advisory check failed: " + ex.Message, StatusKind.Warning); }
            };
            string restoreProblem = LoadAppSettings();
            ApplyTheme();
            UpdateButtons();
            RefreshPreview();
            if (restoreProblem != null) SetStatus(restoreProblem, StatusKind.Warning);
            else SetStatus("Ready. Add files with Add Files..., Add Folder... or drag them into the window.");
        }

        // ---- Layout helpers for the design template's group boxes

        GroupBox Group(string title, int rows)
        {
            return new GroupBox { Text = title, Dock = DockStyle.Top, Height = 26 + rows * 30, Padding = new Padding(10, 4, 10, 4) };
        }
        static TableLayoutPanel Table(GroupBox group, int columns, int rows)
        {
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = columns, RowCount = rows };
            for (int i = 0; i < columns; i++)
                table.ColumnStyles.Add(i % 2 == 0 ? new ColumnStyle(SizeType.AutoSize) : new ColumnStyle(SizeType.Percent, 100f / (columns / 2)));
            for (int i = 0; i < rows; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            group.Controls.Add(table);
            return table;
        }
        static Label Caption(string text)
        {
            return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 8, 3) };
        }
        static void Row(TableLayoutPanel table, int row, string leftCaption, Control left, string rightCaption, Control right)
        {
            table.Controls.Add(Caption(leftCaption), 0, row);
            if (left != null) { left.Dock = DockStyle.Fill; left.Margin = new Padding(3, 3, 16, 3); table.Controls.Add(left, 1, row); }
            table.Controls.Add(Caption(rightCaption), 2, row);
            if (right != null) { right.Dock = DockStyle.Fill; table.Controls.Add(right, 3, row); }
        }
        static FlowLayoutPanel Flow(params Control[] controls)
        {
            var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0), Anchor = AnchorStyles.Left };
            flow.Controls.AddRange(controls);
            return flow;
        }
        Button MakeButton(string text, Action action, int width)
        {
            var button = new Button { Text = text, Size = new Size(width, 28), Margin = new Padding(6, 3, 0, 3) };
            button.Click += (s, e) => action();
            return button;
        }

        // ---- Theme and status

        void ApplyTheme()
        {
            ThemeManager.SetTheme(theme);
            ThemeManager.Apply(this);
            attribution.ForeColor = ThemeManager.Current.DimText;
            foreach (var pair in themeItems) pair.Value.Checked = pair.Key == theme;
            SetStatus(status.Text, statusKind);
            if (grid.Rows.Count > 0) RefreshPreview();
        }
        void SetTheme(AppTheme value)
        {
            theme = value;
            ApplyTheme();
        }
        void SetStatus(string text, StatusKind kind = StatusKind.Neutral)
        {
            statusKind = kind;
            status.Text = text;
            status.ForeColor = ThemeManager.Current.StatusColor(kind);
        }
        void RequestCancel()
        {
            if (!busy) return;
            cancelRequested = true;
            SetStatus("Cancellation requested; completing safe rollback...", StatusKind.Warning);
        }
        void ClearList()
        {
            if (busy) return;
            files.Clear();
            ClearSortGlyph();
            RefreshPreview();
        }
        void ShowAbout()
        {
            using (var about = new AboutForm()) about.ShowDialog(this);
        }
        void OpenDataFolder()
        {
            try
            {
                Directory.CreateDirectory(RenameHistory.DataDirectory);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(RenameHistory.DataDirectory) { UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Cannot open folder", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        // Uses the HTREASURE Software Update Service once a signed release feed is configured (HTReleaseFeedUrl).
        // Nothing is downloaded or installed without the user's approval.
        void CheckForUpdates()
        {
            if (Standards.Value.ReleaseFeedUrl == null)
            {
                MessageBox.Show(this, "Automatic update checks are not set up for this version yet.\n\nNew versions are published by HTREASURE LTD at " + WebsiteUrl + ".",
                    "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SecurityAdvisoryUi.ShowHistory(this, Standards.Value);
        }
        internal static void ShowLicence(IWin32Window owner)
        {
            using (var licence = new LicenceForm()) licence.ShowDialog(owner);
        }
        internal static void OpenWebsite(IWin32Window owner)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(WebsiteUrl) { UseShellExecute = true }); }
            catch (System.ComponentModel.Win32Exception)
            {
                MessageBox.Show(owner, "Could not open a web browser. The HTREASURE LTD website is " + WebsiteUrl,
                    BrandInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        ToolStripMenuItem Command(ToolStripMenuItem parent, string text, Action action, Keys keys = Keys.None, string keyText = null, string tip = null)
        {
            var item = new ToolStripMenuItem(text, null, (s, e) => action()) { ShortcutKeys = keys, ToolTipText = tip };
            if (keyText != null) item.ShortcutKeyDisplayString = keyText;
            parent.DropDownItems.Add(item);
            commandItems.Add(item);
            return item;
        }
        void AddFilesDialog()
        {
            using (var dlg = new OpenFileDialog { Multiselect = true, Title = "Choose files to rename" })
                if (dlg.ShowDialog() == DialogResult.OK) AddFiles(dlg.FileNames);
        }
        void AddFolderDialog()
        {
            using (var dlg = new FolderBrowserDialog { Description = "Choose a folder to add" })
                if (dlg.ShowDialog() == DialogResult.OK) AddFolder(dlg.SelectedPath);
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
            // Shown after the preview so its status message doesn't hide the warning.
            if (skippedFolderError != null)
            {
                SetStatus(skippedFolders + " folder(s) could not be read and were skipped (" + skippedFolderError + ").", StatusKind.Warning);
                skippedFolders = 0;
                skippedFolderError = null;
            }
        }
        int skippedFolders;
        string skippedFolderError;
        void NoteSkippedFolder(Exception ex)
        {
            skippedFolders++;
            if (skippedFolderError == null) skippedFolderError = ex.Message;
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
                catch (Exception ex) { NoteSkippedFolder(ex); }
                if (!recursive.Checked) continue;
                try
                {
                    foreach (string sub in Directory.GetDirectories(current))
                    {
                        if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) == 0) pending.Push(sub);
                    }
                }
                catch (Exception ex) { NoteSkippedFolder(ex); }
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
                    SetStatus("Preset saved: " + dialog.FileName, StatusKind.Success);
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
                    RefreshPreview(); SetStatus("Preset loaded: " + dialog.FileName, StatusKind.Success);
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
                AppTheme savedTheme;
                if (Enum.TryParse((string)root.Element("Theme"), out savedTheme) && Enum.IsDefined(typeof(AppTheme), savedTheme)) theme = savedTheme;
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
                root.Add(new XElement("Theme", theme));
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
            if (files.Count == 0) { SetStatus("Nothing to export — add files first.", StatusKind.Warning); return; }
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
                    SetStatus("Preview exported: " + dialog.FileName, StatusKind.Success);
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
                    row.DefaultCellStyle.ForeColor = file.Included ? ThemeManager.Current.Text : ThemeManager.Current.DimText;
                }
            }
            finally { updatingGrid = false; }
            int included = files.Count(f => f.Included);
            int changes = moves == null ? 0 : moves.Count(m => m.Item1 != m.Item2);
            statusRight.Text = files.Count == 0 ? "" : files.Count + " file(s)  •  " + included + " included  •  " + changes + " to rename";
            if (settingsError != null) SetStatus(settingsError, StatusKind.Error);
            else if (problem != null) SetStatus("Check: " + problem, StatusKind.Warning);
            else if (files.Count == 0) SetStatus("Ready. Add files with Add Files..., Add Folder... or drag them into the window.");
            else if (included == 0) SetStatus("Every file is excluded. Tick Include for the files you want to rename.", StatusKind.Warning);
            else if (changes == 0) SetStatus("Nothing to rename yet: set a prefix, suffix, numbering or find/replace rule.");
            else SetStatus("Preview ready. Check the new names, then click Rename Files.");
            UpdateButtons();
            canRename = moves != null && problem == null && changes > 0;
            UpdateRenameButton();
        }
        bool canRename;
        void UpdateRenameButton()
        {
            renameItem.Enabled = renameButton.Enabled = !busy && canRename;
            ThemeManager.UpdatePrimary(renameButton);
        }
        // Lock every input while files are being moved; Application.DoEvents keeps the UI live.
        void UpdateButtons()
        {
            foreach (var item in commandItems) item.Enabled = !busy;
            foreach (var control in lockedWhileBusy) control.Enabled = !busy;
            undoItem.Enabled = undoButton.Enabled = !busy && undoBatch.Count > 0;
            cancelItem.Enabled = cancelButton.Enabled = busy;
            UpdateRenameButton();
        }

        // Runs RenameExecutor with the window locked, the progress bar updating and Cancel live.
        bool ApplyMoves(List<Tuple<string, string>> moves, out string error)
        {
            cancelRequested = false;
            busy = true;
            UpdateButtons();
            progress.Value = 0;
            progress.Maximum = Math.Max(1, moves.Count * 2);
            progress.Visible = true;
            SetStatus("Renaming " + moves.Count + " file(s)...");
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
                progress.Visible = false;
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
            if (historyWarning == null) SetStatus("Renamed " + moves.Count + " file(s). To undo, click Undo Last Rename, or later use Rename > History.", StatusKind.Success);
            else SetStatus("Renamed " + moves.Count + " file(s), but: " + historyWarning, StatusKind.Warning);
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
            if (historyWarning == null) SetStatus("Undo complete — " + moves.Count + " filename(s) restored.", StatusKind.Success);
            else SetStatus("Undo complete — " + moves.Count + " filename(s) restored, but: " + historyWarning, StatusKind.Warning);
            return true;
        }
    }
}
