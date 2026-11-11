using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Diagnostics;
using System.Windows.Forms;

namespace HTBulkFileRenamer
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        readonly List<string> files = new List<string>();
        readonly List<Tuple<string, string>> undoBatch = new List<Tuple<string, string>>();
        readonly DataGridView grid = new DataGridView();
        readonly TextBox prefix = new TextBox(), suffix = new TextBox(), find = new TextBox(), replace = new TextBox();
        readonly CheckBox numbering = new CheckBox(), recursive = new CheckBox();
        readonly TextBox filter = new TextBox();
        readonly ProgressBar progress = new ProgressBar();
        readonly Button cancelButton = new Button();
        bool cancelRequested = false, busy = false;
        readonly string dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HTSoftware", "BulkFileRenamer");
        readonly NumericUpDown start = new NumericUpDown(), digits = new NumericUpDown();
        readonly ComboBox casing = new ComboBox();
        readonly Label status = new Label();
        readonly Button renameButton = new Button(), undoButton = new Button();
        readonly Color navy = Color.FromArgb(16, 42, 78), blue = Color.FromArgb(11, 61, 145);

        public MainForm()
        {
            Text = "HT Bulk File Renamer Pro — Stage 3";
            Size = new Size(1180, 780);
            MinimumSize = new Size(960, 700);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(245, 247, 251);
            AllowDrop = true;

            var header = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = navy, Padding = new Padding(20, 12, 20, 8) };
            header.Controls.Add(new Label { Text = "HT  /  BULK FILE RENAMER PRO", ForeColor = Color.White, Font = new Font("Segoe UI", 17, FontStyle.Bold), Dock = DockStyle.Top, Height = 36 });
            header.Controls.Add(new Label { Text = "Batch rename  •  Live preview  •  Safe undo  •  Drag & drop", ForeColor = Color.FromArgb(200, 218, 242), Dock = DockStyle.Bottom, Height = 22 });

            var settings = new TableLayoutPanel { Dock = DockStyle.Top, Height = 185, Padding = new Padding(15, 10, 15, 5), ColumnCount = 8, RowCount = 3, BackColor = Color.White };
            for (int i = 0; i < 8; i++) settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
            AddField(settings, "Prefix", prefix, 0, 0, 2);
            AddField(settings, "Suffix (before extension)", suffix, 2, 0, 2);
            AddField(settings, "Find", find, 4, 0, 2);
            AddField(settings, "Replace with", replace, 6, 0, 2);
            numbering.Text = "Add numbering (001_, 002_, 003_)"; numbering.AutoSize = true; numbering.Checked = true; numbering.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            var numberPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            numberPanel.Controls.Add(numbering);
            settings.Controls.Add(numberPanel, 0, 1); settings.SetColumnSpan(numberPanel, 2);
            start.Minimum = 0; start.Maximum = 999999; start.Value = 1;
            digits.Minimum = 1; digits.Maximum = 8; digits.Value = 3;
            AddField(settings, "Start number", start, 2, 1, 2);
            AddField(settings, "Number digits", digits, 4, 1, 2);
            casing.DropDownStyle = ComboBoxStyle.DropDownList;
            casing.Items.AddRange(new object[] { "Keep original", "lowercase", "UPPERCASE", "Title Case" });
            casing.SelectedIndex = 0;
            AddField(settings, "Filename case", casing, 6, 1, 2);
            var extra = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
            extra.Controls.Add(new Label { Text = "File filters (e.g. *.txt;*.jpg):", AutoSize = true, Padding = new Padding(0, 7, 3, 0) });
            filter.Width = 190; filter.Text = "*.*"; extra.Controls.Add(filter);
            recursive.Text = "Include subfolders when adding folder"; recursive.AutoSize = true;
            recursive.Margin = new Padding(18, 6, 0, 0); extra.Controls.Add(recursive);
            extra.Controls.Add(new Label { Text = "  Extensions are preserved; find/replace is case-sensitive.", AutoSize = true, ForeColor = Color.DimGray, Padding = new Padding(5, 7, 0, 0) });
            settings.Controls.Add(extra, 0, 2);
            settings.SetColumnSpan(extra, 8);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 106, Padding = new Padding(15, 8, 10, 6), BackColor = Color.FromArgb(233, 239, 249), WrapContents = true };
            var add = MakeButton("Add Files", blue, 110);
            var addFolder = MakeButton("Add Folder", blue, 115);
            var clear = MakeButton("Clear List", Color.FromArgb(88, 101, 120), 100);
            var preview = MakeButton("Refresh Preview", blue, 135);
            renameButton.Text = "Rename Files"; StyleButton(renameButton, Color.FromArgb(17, 119, 89), 130);
            undoButton.Text = "Undo Last"; StyleButton(undoButton, Color.FromArgb(153, 90, 23), 105);
            var savePreset = MakeButton("Save Preset", blue, 110);
            var loadPreset = MakeButton("Load Preset", blue, 110);
            var historyButton = MakeButton("History", Color.FromArgb(88, 101, 120), 85);
            cancelButton.Text = "Cancel"; StyleButton(cancelButton, Color.FromArgb(153, 55, 50), 85);
            cancelButton.Enabled = false;
            actions.Controls.AddRange(new Control[] { add, addFolder, clear, preview, renameButton, undoButton, savePreset, loadPreset, historyButton, cancelButton });
            savePreset.Click += (s, e) => SavePreset();
            loadPreset.Click += (s, e) => LoadPreset();
            historyButton.Click += (s, e) => OpenHistory();
            cancelButton.Click += (s, e) => { cancelRequested = true; status.Text = "Cancellation requested; completing safe rollback..."; };

            grid.Dock = DockStyle.Fill;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersHeight = 34;
            grid.RowTemplate.Height = 29;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = navy;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 249, 255);
            grid.Columns.Add("original", "Original filename");
            grid.Columns.Add("proposed", "Proposed filename");
            grid.Columns.Add("result", "Status");
            grid.Columns[2].FillWeight = 55;
            grid.AllowDrop = true;

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 36, BackColor = Color.White, Padding = new Padding(16, 8, 10, 4) };
            status.Dock = DockStyle.Fill;
            status.ForeColor = navy;
            status.Text = "Ready — add files or drag files/folders into the window.";
            progress.Dock = DockStyle.Right; progress.Width = 180; progress.Minimum = 0; progress.Maximum = 100;
            bottom.Controls.Add(progress);
            bottom.Controls.Add(status);

            Controls.Add(grid);
            Controls.Add(bottom);
            Controls.Add(actions);
            Controls.Add(settings);
            Controls.Add(header);

            add.Click += (s, e) => { using (var dlg = new OpenFileDialog { Multiselect = true, Title = "Choose files to rename" }) if (dlg.ShowDialog() == DialogResult.OK) AddFiles(dlg.FileNames); };
            addFolder.Click += (s, e) => { using (var dlg = new FolderBrowserDialog { Description = "Choose a folder (top-level files only)" }) if (dlg.ShowDialog() == DialogResult.OK) AddFolder(dlg.SelectedPath); };
            clear.Click += (s, e) => { files.Clear(); RefreshPreview(); };
            preview.Click += (s, e) => RefreshPreview();
            renameButton.Click += (s, e) => RenameFiles();
            undoButton.Click += (s, e) => Undo();
            foreach (Control c in new Control[] { prefix, suffix, find, replace }) ((TextBox)c).TextChanged += (s, e) => RefreshPreview();
            numbering.CheckedChanged += (s, e) => RefreshPreview();
            start.ValueChanged += (s, e) => RefreshPreview();
            digits.ValueChanged += (s, e) => RefreshPreview();
            casing.SelectedIndexChanged += (s, e) => RefreshPreview();
            DragEnter += DragEnterFiles; DragDrop += DropFiles;
            grid.DragEnter += DragEnterFiles; grid.DragDrop += DropFiles;
            UpdateButtons();
        }

        void AddField(TableLayoutPanel panel, string title, Control field, int column, int row, int span)
        {
            var box = new Panel { Dock = DockStyle.Fill, Padding = new Padding(3, 0, 8, 0) };
            box.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 18, ForeColor = navy });
            field.Dock = DockStyle.Bottom;
            field.Height = 24;
            box.Controls.Add(field);
            panel.Controls.Add(box, column, row);
            panel.SetColumnSpan(box, span);
        }
        Button MakeButton(string label, Color color, int width)
        {
            var button = new Button { Text = label };
            StyleButton(button, color, width);
            return button;
        }
        void StyleButton(Button b, Color color, int width)
        {
            b.Width = width; b.Height = 34; b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0; b.BackColor = color; b.ForeColor = Color.White;
            b.Margin = new Padding(3, 1, 5, 1);
        }
        void DragEnterFiles(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }
        void DropFiles(object sender, DragEventArgs e)
        {
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
            var existing = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
            var patterns = GetPatterns();
            foreach (var p in paths)
            {
                var full = Path.GetFullPath(p);
                if (File.Exists(full) && MatchesFilter(Path.GetFileName(full), patterns) && existing.Add(full)) files.Add(full);
            }
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
        bool IsInvalidWindowsName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.EndsWith(".") || name.EndsWith(" ")) return true;
            string stem = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            var reserved = new[] { "CON", "PRN", "AUX", "NUL" };
            return reserved.Contains(stem) || (stem.Length == 4 &&
                ((stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] >= '1' && stem[3] <= '9'));
        }
        void SavePreset()
        {
            using (var dialog = new SaveFileDialog { Title = "Save rename preset", Filter = "HT Rename Preset (*.htpreset)|*.htpreset", DefaultExt = "htpreset" })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                try
                {
                    var doc = new XDocument(new XElement("HTRenamePreset", new XAttribute("version", "1"),
                        new XElement("Prefix", prefix.Text), new XElement("Suffix", suffix.Text),
                        new XElement("Find", find.Text), new XElement("Replace", replace.Text),
                        new XElement("Numbering", numbering.Checked), new XElement("Start", start.Value),
                        new XElement("Digits", digits.Value), new XElement("Case", casing.SelectedIndex),
                        new XElement("Filter", filter.Text), new XElement("Recursive", recursive.Checked)));
                    doc.Save(dialog.FileName);
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
                    string read(string name) { return (string)root.Element(name) ?? ""; }
                    decimal startValue = decimal.Parse(read("Start"), System.Globalization.CultureInfo.InvariantCulture);
                    decimal digitValue = decimal.Parse(read("Digits"), System.Globalization.CultureInfo.InvariantCulture);
                    int caseIndex = int.Parse(read("Case"));
                    bool num = bool.Parse(read("Numbering"));
                    bool recurse = bool.Parse(read("Recursive"));
                    if (startValue < start.Minimum || startValue > start.Maximum || digitValue < digits.Minimum || digitValue > digits.Maximum || caseIndex < 0 || caseIndex >= casing.Items.Count)
                        throw new InvalidDataException("Preset contains out-of-range values.");
                    prefix.Text = read("Prefix"); suffix.Text = read("Suffix");
                    find.Text = read("Find"); replace.Text = read("Replace");
                    numbering.Checked = num; start.Value = startValue; digits.Value = digitValue;
                    casing.SelectedIndex = caseIndex; filter.Text = read("Filter"); recursive.Checked = recurse;
                    RefreshPreview(); status.Text = "Preset loaded: " + dialog.FileName;
                }
                catch (Exception ex) { MessageBox.Show("Could not load preset: " + ex.Message); }
            }
        }
        void WriteHistory(string operation, List<Tuple<string, string>> moves)
        {
            try
            {
                Directory.CreateDirectory(dataDirectory);
                string log = Path.Combine(dataDirectory, "history.tsv");
                using (var writer = new StreamWriter(log, true, Encoding.UTF8))
                {
                    foreach (var move in moves)
                    {
                        writer.WriteLine(DateTime.UtcNow.ToString("o") + "\t" + operation + "\t" +
                            Convert.ToBase64String(Encoding.UTF8.GetBytes(move.Item1)) + "\t" +
                            Convert.ToBase64String(Encoding.UTF8.GetBytes(move.Item2)));
                    }
                }
            }
            catch (Exception ex) { status.Text = "Rename succeeded but history could not be saved: " + ex.Message; }
        }
        void OpenHistory()
        {
            string log = Path.Combine(dataDirectory, "history.tsv");
            if (!File.Exists(log)) { MessageBox.Show("No rename history has been recorded yet."); return; }
            try { Process.Start("notepad.exe", "\"" + log + "\""); }
            catch (Exception ex) { MessageBox.Show("Unable to open history: " + ex.Message); }
        }
        string NewPath(string path, int index)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(find.Text)) name = name.Replace(find.Text, replace.Text);
            switch (casing.SelectedIndex)
            {
                case 1: name = name.ToLowerInvariant(); break;
                case 2: name = name.ToUpperInvariant(); break;
                case 3: name = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower()); break;
            }
            string number = numbering.Checked ? ((long)start.Value + index).ToString("D" + (int)digits.Value) + "_" : "";
            return Path.Combine(Path.GetDirectoryName(path), prefix.Text + number + name + suffix.Text + extension);
        }
        List<Tuple<string, string>> PlannedMoves()
        {
            return files.Select((f, i) => Tuple.Create(f, NewPath(f, i))).ToList();
        }
        string Validate(List<Tuple<string, string>> moves)
        {
            if (moves.Any(m => !File.Exists(m.Item1))) return "One or more source files no longer exist.";
            if (moves.Any(m => string.IsNullOrWhiteSpace(Path.GetFileName(m.Item2)))) return "A proposed filename is empty.";
            if (moves.Any(m => Path.GetFileName(m.Item2).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)) return "A proposed filename contains invalid characters.";
            if (moves.Any(m => IsInvalidWindowsName(Path.GetFileName(m.Item2)))) return "A filename is reserved, ends with a dot/space, or is invalid on Windows.";
            if (moves.Any(m => Path.GetFileName(m.Item2).Length > 240)) return "A proposed filename is too long (limit 240 characters for this tool).";
            if (moves.Any(m => m.Item2.Length >= 260)) return "A proposed path exceeds 259 characters (legacy Windows compatibility limit).";
            if (moves.Any(m => Directory.Exists(m.Item2))) return "A proposed destination is already a directory.";
            if (moves.Select(m => m.Item2).Distinct(StringComparer.OrdinalIgnoreCase).Count() != moves.Count) return "Two files would receive the same destination filename.";
            var sources = new HashSet<string>(moves.Select(m => m.Item1), StringComparer.OrdinalIgnoreCase);
            if (moves.Any(m => File.Exists(m.Item2) && !sources.Contains(m.Item2))) return "A destination filename is already in use by another file.";
            return null;
        }
        void RefreshPreview()
        {
            if (busy) return;
            grid.Rows.Clear();
            List<Tuple<string, string>> moves;
            try { moves = PlannedMoves(); }
            catch (Exception ex) { status.Text = "Invalid rename settings: " + ex.Message; renameButton.Enabled = false; return; }
            string problem = Validate(moves);
            foreach (var m in moves)
            {
                bool changed = !string.Equals(m.Item1, m.Item2, StringComparison.Ordinal);
                grid.Rows.Add(Path.GetFileName(m.Item1), Path.GetFileName(m.Item2), changed ? "Pending" : "Unchanged");
            }
            status.Text = problem == null ? files.Count + " file(s) loaded — " + moves.Count(m => m.Item1 != m.Item2) + " change(s) previewed. Numbering: " + (numbering.Checked ? "ON" : "OFF") : "Check: " + problem;
            renameButton.Enabled = problem == null && moves.Any(m => m.Item1 != m.Item2);
            UpdateButtons();
        }
        void UpdateButtons() { undoButton.Enabled = !busy && undoBatch.Count > 0; cancelButton.Enabled = busy; }

        // Stage all changed sources under temporary names to safely handle swaps and cycles.
        // On error, attempt to roll back both completed and staged moves.
        bool ApplyMoves(List<Tuple<string, string>> moves, out string error)
        {
            error = null;
            cancelRequested = false;
            busy = true;
            UpdateButtons();
            progress.Value = 0;
            progress.Maximum = Math.Max(1, moves.Count * 2);
            int completed = 0;
            var staged = new List<Tuple<string, string, string>>();
            try
            {
                foreach (var move in moves)
                {
                    Application.DoEvents();
                    if (cancelRequested) throw new OperationCanceledException("Operation cancelled by user.");
                    string temp;
                    do { temp = Path.Combine(Path.GetDirectoryName(move.Item1), ".ht-renamer-" + Guid.NewGuid().ToString("N") + ".tmp"); }
                    while (File.Exists(temp) || Directory.Exists(temp));
                    File.Move(move.Item1, temp);
                    staged.Add(Tuple.Create(move.Item1, temp, move.Item2));
                    progress.Value = ++completed;
                }
                foreach (var item in staged)
                {
                    // Do not interrupt the commit phase: finish moving staged files first.
                    File.Move(item.Item2, item.Item3);
                    progress.Value = ++completed;
                    Application.DoEvents();
                }
                return true;
            }
            catch (Exception ex)
            {
                var failures = new List<string>();
                foreach (var item in staged.AsEnumerable().Reverse())
                {
                    try
                    {
                        if (File.Exists(item.Item3) && !File.Exists(item.Item2)) File.Move(item.Item3, item.Item2);
                    }
                    catch (Exception rollbackEx) { failures.Add(rollbackEx.Message); }
                }
                foreach (var item in staged.AsEnumerable().Reverse())
                {
                    try { if (File.Exists(item.Item2) && !File.Exists(item.Item1)) File.Move(item.Item2, item.Item1); }
                    catch (Exception rollbackEx) { failures.Add(rollbackEx.Message); }
                }
                error = ex.Message + (failures.Count > 0 ? "\nRollback incomplete. Check the folder for .ht-renamer temporary files.\n" + string.Join("; ", failures) : "\nRollback attempted; verify your files.");
                return false;
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
            var all = PlannedMoves();
            var problem = Validate(all);
            if (problem != null) { MessageBox.Show(problem, "Cannot rename", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            var moves = all.Where(m => m.Item1 != m.Item2).ToList();
            if (moves.Count == 0) return;
            if (MessageBox.Show("Rename " + moves.Count + " file(s)?\n\nReview the preview before continuing.", "Confirm batch rename", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string error;
            if (!ApplyMoves(moves, out error)) { MessageBox.Show(error, "Rename error", MessageBoxButtons.OK, MessageBoxIcon.Error); RefreshPreview(); return; }
            undoBatch.Clear(); undoBatch.AddRange(moves);
            WriteHistory("RENAME", moves);
            for (int i = 0; i < files.Count; i++) files[i] = all[i].Item2;
            RefreshPreview();
            status.Text = "Renamed " + moves.Count + " file(s) successfully. Undo is available until the next rename or app exit.";
        }
        void Undo()
        {
            if (undoBatch.Count == 0) return;
            var reverse = undoBatch.Select(m => Tuple.Create(m.Item2, m.Item1)).ToList();
            string problem = Validate(reverse);
            if (problem != null) { MessageBox.Show("Undo cannot proceed: " + problem); return; }
            string error;
            if (!ApplyMoves(reverse, out error)) { MessageBox.Show("Undo failed: " + error); return; }
            for (int i = 0; i < files.Count; i++)
            {
                var item = undoBatch.FirstOrDefault(m => string.Equals(m.Item2, files[i], StringComparison.OrdinalIgnoreCase));
                if (item != null) files[i] = item.Item1;
            }
            WriteHistory("UNDO", reverse);
            undoBatch.Clear(); RefreshPreview();
            status.Text = "Undo complete — previous filenames restored.";
        }
    }
}
