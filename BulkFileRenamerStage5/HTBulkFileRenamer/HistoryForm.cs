using HTREASURE.Branding;
using HTREASURE.Branding.WinForms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace HTBulkFileRenamer
{
    // Lists past batches from history.tsv and can undo a rename batch even after a restart.
    public class HistoryForm : Form
    {
        readonly DataGridView batchGrid = new DataGridView(), moveGrid = new DataGridView();
        readonly Button undoButton = new Button(), openLogButton = new Button(), closeButton = new Button();
        readonly Label summary = new Label();
        readonly SplitContainer split = new SplitContainer();
        readonly Func<HistoryBatch, IWin32Window, bool> undo;

        public HistoryForm(Func<HistoryBatch, IWin32Window, bool> undo)
        {
            this.undo = undo;
            Text = "Rename history — " + BrandInfo.ProductName;
            Icon = BrandWinForms.ProductIcon;
            Size = new Size(920, 620);
            MinimumSize = new Size(640, 420);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);

            summary.Dock = DockStyle.Top; summary.Height = 34; summary.Padding = new Padding(12, 9, 12, 0);

            ConfigureGrid(batchGrid);
            batchGrid.Columns.Add("when", "When");
            batchGrid.Columns.Add("operation", "Operation");
            batchGrid.Columns.Add("count", "Files");
            batchGrid.Columns.Add("folder", "Folder");
            batchGrid.Columns.Add("state", "State");
            batchGrid.Columns[0].FillWeight = 55; batchGrid.Columns[1].FillWeight = 35; batchGrid.Columns[2].FillWeight = 25; batchGrid.Columns[4].FillWeight = 55;
            batchGrid.MultiSelect = false;
            batchGrid.SelectionChanged += (s, e) => ShowSelected();

            ConfigureGrid(moveGrid);
            moveGrid.Columns.Add("from", "From");
            moveGrid.Columns.Add("to", "To");

            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.Panel1.Controls.Add(batchGrid);
            split.Panel2.Controls.Add(moveGrid);
            split.Panel2.Controls.Add(new Label { Text = "Files in the selected batch", Dock = DockStyle.Top, Height = 24, Padding = new Padding(12, 5, 0, 0), Tag = ThemeManager.AccentTag });

            // Design template: standard buttons, the one primary action in the accent colour.
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(10, 8, 10, 5), FlowDirection = FlowDirection.RightToLeft };
            StyleButton(closeButton, "Close", 92);
            StyleButton(openLogButton, "Open Log File", 120);
            StyleButton(undoButton, "Undo Selected Batch", 150);
            undoButton.Tag = ThemeManager.PrimaryTag;
            buttons.Controls.AddRange(new Control[] { closeButton, openLogButton, undoButton });
            closeButton.Click += (s, e) => Close();
            openLogButton.Click += (s, e) => OpenLog();
            undoButton.Click += (s, e) => UndoSelected();
            CancelButton = closeButton;

            Controls.Add(split);
            Controls.Add(buttons);
            Controls.Add(summary);
            ThemeManager.Apply(this);
            Load += (s, e) => { split.SplitterDistance = split.Height * 55 / 100; LoadBatches(); };
        }

        void ConfigureGrid(DataGridView grid)
        {
            grid.Dock = DockStyle.Fill;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersHeight = 26;
            grid.RowTemplate.Height = 24;
        }

        static void StyleButton(Button b, string text, int width)
        {
            b.Text = text; b.Width = width; b.Height = 30;
            b.Margin = new Padding(6, 1, 0, 1);
        }

        void LoadBatches()
        {
            List<HistoryBatch> batches;
            string loadError = null;
            try { batches = RenameHistory.Load(); }
            catch (Exception ex) { batches = new List<HistoryBatch>(); loadError = "Could not read history: " + ex.Message; }
            batchGrid.Rows.Clear();
            foreach (var batch in Enumerable.Reverse(batches))
            {
                int row = batchGrid.Rows.Add(batch.TimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"), batch.Operation, batch.Moves.Count, Folder(batch), State(batch));
                batchGrid.Rows[row].Tag = batch;
                if (batch.Operation != "RENAME" || batch.Undone) batchGrid.Rows[row].DefaultCellStyle.ForeColor = ThemeManager.Current.DimText;
            }
            summary.Text = loadError ?? (batches.Count == 0
                ? "No rename history has been recorded yet."
                : batches.Count + " batch(es), newest first. Select a rename batch to see its files or undo it.");
            openLogButton.Enabled = File.Exists(RenameHistory.LogPath);
            ShowSelected();
        }

        static string Folder(HistoryBatch batch)
        {
            var folders = batch.Moves.Select(m => Path.GetDirectoryName(m.Item1)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return folders.Count == 1 ? folders[0] : folders.Count + " folders";
        }

        static string State(HistoryBatch batch)
        {
            if (batch.Operation == "UNDO") return "Undo of an earlier batch";
            return batch.Undone ? "Undone" : "Not undone";
        }

        HistoryBatch Selected()
        {
            return batchGrid.SelectedRows.Count == 1 ? batchGrid.SelectedRows[0].Tag as HistoryBatch : null;
        }

        void ShowSelected()
        {
            var batch = Selected();
            moveGrid.Rows.Clear();
            if (batch != null)
                foreach (var move in batch.Moves)
                {
                    int row = moveGrid.Rows.Add(Path.GetFileName(move.Item1), Path.GetFileName(move.Item2));
                    moveGrid.Rows[row].Cells[0].ToolTipText = move.Item1;
                    moveGrid.Rows[row].Cells[1].ToolTipText = move.Item2;
                }
            undoButton.Enabled = batch != null && batch.Operation == "RENAME" && !batch.Undone;
            ThemeManager.UpdatePrimary(undoButton);
        }

        void UndoSelected()
        {
            var batch = Selected();
            if (batch == null || batch.Operation != "RENAME" || batch.Undone) return;
            string when = batch.TimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            if (MessageBox.Show(this, "Restore " + batch.Moves.Count + " file(s) to the names they had before the rename on " + when + "?",
                "Undo batch", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            undoButton.Enabled = false;
            ThemeManager.UpdatePrimary(undoButton);
            if (undo(batch, this)) LoadBatches(); else ShowSelected();
        }

        void OpenLog()
        {
            try { Process.Start("notepad.exe", "\"" + RenameHistory.LogPath + "\""); }
            catch (Exception ex) { MessageBox.Show(this, "Unable to open history: " + ex.Message); }
        }
    }
}
