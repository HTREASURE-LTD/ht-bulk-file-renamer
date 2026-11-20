using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HTBulkFileRenamer
{
    public enum AppTheme { System, Light, Dark }

    public enum StatusKind { Neutral, Success, Warning, Error }

    /// <summary>
    /// HTREASURE LTD application design template (shared with HT ISO Ripper and HT ISO Creator): white window,
    /// dark-blue header banner, accent-coloured group captions, standard buttons with one accent primary button.
    /// </summary>
    public sealed class ThemePalette
    {
        public bool IsDark { get; private set; }
        public Color Window { get; private set; }
        public Color Text { get; private set; }
        public Color DimText { get; private set; }
        public Color Accent { get; private set; }
        public Color AccentText { get; private set; }
        public Color DisabledAccent { get; private set; }
        public Color InputBack { get; private set; }
        public Color ButtonBack { get; private set; }
        public Color Border { get; private set; }
        public Color ErrorText { get; private set; }
        public Color WarningText { get; private set; }
        public Color SuccessText { get; private set; }
        public Color StripBack { get; private set; }
        public Color Highlight { get; private set; }
        public Color HeaderBack { get; private set; }
        public Color HeaderText { get; private set; }
        public Color HeaderDimText { get; private set; }

        public static readonly ThemePalette Light = new ThemePalette
        {
            IsDark = false,
            Window = Color.White,
            Text = SystemColors.ControlText,
            DimText = Color.FromArgb(96, 96, 96),
            Accent = Color.FromArgb(22, 58, 110),
            AccentText = Color.White,
            DisabledAccent = Color.FromArgb(196, 202, 212),
            InputBack = SystemColors.Window,
            ButtonBack = SystemColors.Control,
            Border = Color.FromArgb(204, 204, 204),
            ErrorText = Color.FromArgb(176, 32, 32),
            WarningText = Color.FromArgb(156, 96, 0),
            SuccessText = Color.FromArgb(30, 126, 52),
            StripBack = Color.FromArgb(243, 245, 248),
            Highlight = Color.FromArgb(220, 230, 245),
            HeaderBack = Color.FromArgb(22, 58, 110),
            HeaderText = Color.White,
            HeaderDimText = Color.FromArgb(196, 212, 236)
        };

        public static readonly ThemePalette Dark = new ThemePalette
        {
            IsDark = true,
            Window = Color.FromArgb(32, 34, 38),
            Text = Color.FromArgb(230, 232, 235),
            DimText = Color.FromArgb(160, 166, 174),
            Accent = Color.FromArgb(110, 160, 232),
            AccentText = Color.FromArgb(20, 22, 26),
            DisabledAccent = Color.FromArgb(70, 76, 86),
            InputBack = Color.FromArgb(45, 48, 54),
            ButtonBack = Color.FromArgb(52, 56, 63),
            Border = Color.FromArgb(78, 84, 94),
            ErrorText = Color.FromArgb(255, 120, 120),
            WarningText = Color.FromArgb(240, 190, 90),
            SuccessText = Color.FromArgb(110, 210, 130),
            StripBack = Color.FromArgb(40, 43, 48),
            Highlight = Color.FromArgb(62, 76, 98),
            HeaderBack = Color.FromArgb(24, 44, 76),
            HeaderText = Color.White,
            HeaderDimText = Color.FromArgb(176, 194, 222)
        };

        public Color StatusColor(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Success: return SuccessText;
                case StatusKind.Warning: return WarningText;
                case StatusKind.Error: return ErrorText;
                default: return Text;
            }
        }
    }

    /// <summary>
    /// Applies the light or dark palette to forms (ported from HT ISO Ripper). Controls opt into special colours
    /// with their Tag: "accent" (headings), "dim" (secondary text), "primary" (main button) or "header" (the banner).
    /// </summary>
    public static class ThemeManager
    {
        public const string HeaderTag = "header";
        public const string AccentTag = "accent";
        public const string DimTag = "dim";
        public const string PrimaryTag = "primary";

        static ThemePalette current = ThemePalette.Light;

        public static ThemePalette Current { get { return current; } }

        public static void SetTheme(AppTheme theme)
        {
            current = Resolve(theme);
        }

        public static ThemePalette Resolve(AppTheme theme)
        {
            switch (theme)
            {
                case AppTheme.Light: return ThemePalette.Light;
                case AppTheme.Dark: return ThemePalette.Dark;
                default: return WindowsUsesDarkApps() ? ThemePalette.Dark : ThemePalette.Light;
            }
        }

        public static void Apply(Control root)
        {
            ThemePalette palette = current;
            root.BackColor = palette.Window;
            root.ForeColor = palette.Text;
            ApplyToChildren(root, palette);
            if (root.ContextMenuStrip != null) ApplyToToolStrip(root.ContextMenuStrip, palette);

            var form = root as Form;
            if (form == null) return;
            if (form.IsHandleCreated) SetDarkTitleBar(form.Handle, palette.IsDark);
            else form.HandleCreated += (s, e) => SetDarkTitleBar(form.Handle, current.IsDark);
        }

        /// <summary>Re-colours one control after its state changes (e.g. a text box being enabled or disabled).</summary>
        public static void Restyle(Control control)
        {
            ApplyToControl(control, current);
        }

        /// <summary>Colours a primary button for its current enabled state (call after changing Enabled).</summary>
        public static void UpdatePrimary(Button button)
        {
            button.BackColor = button.Enabled ? current.Accent : current.DisabledAccent;
            button.ForeColor = current.AccentText;
        }

        static void ApplyToChildren(Control parent, ThemePalette palette)
        {
            foreach (Control control in parent.Controls)
            {
                if (HeaderTag.Equals(control.Tag))
                {
                    ApplyToHeader(control, palette);
                    continue;
                }
                ApplyToControl(control, palette);
                if (control.ContextMenuStrip != null) ApplyToToolStrip(control.ContextMenuStrip, palette);
                ApplyToChildren(control, palette);
            }
        }

        static void ApplyToHeader(Control header, ThemePalette palette)
        {
            header.BackColor = palette.HeaderBack;
            foreach (Control child in header.Controls)
            {
                child.BackColor = palette.HeaderBack;
                child.ForeColor = DimTag.Equals(child.Tag) ? palette.HeaderDimText : palette.HeaderText;
                var link = child as LinkLabel;
                if (link != null)
                {
                    link.LinkColor = palette.HeaderText;
                    link.ActiveLinkColor = palette.HeaderText;
                    link.VisitedLinkColor = palette.HeaderText;
                }
            }
        }

        static void ApplyToControl(Control control, ThemePalette palette)
        {
            string tag = control.Tag as string;

            var toolStrip = control as ToolStrip;
            if (toolStrip != null)
            {
                ApplyToToolStrip(toolStrip, palette);
                return;
            }

            var grid = control as DataGridView;
            if (grid != null)
            {
                ApplyToGrid(grid, palette);
                return;
            }

            var button = control as Button;
            if (button != null)
            {
                if (tag == PrimaryTag)
                {
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderSize = 0;
                    button.UseVisualStyleBackColor = false;
                    UpdatePrimary(button);
                }
                else if (palette.IsDark)
                {
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderSize = 1;
                    button.FlatAppearance.BorderColor = palette.Border;
                    button.UseVisualStyleBackColor = false;
                    button.BackColor = palette.ButtonBack;
                    button.ForeColor = palette.Text;
                }
                else
                {
                    button.FlatStyle = FlatStyle.Standard;
                    button.BackColor = SystemColors.Control;
                    button.UseVisualStyleBackColor = true;
                    button.ForeColor = palette.Text;
                }
                return;
            }

            if (control is ListView || control is ListBox || control is NumericUpDown)
            {
                control.BackColor = palette.InputBack;
                control.ForeColor = palette.Text;
                return;
            }

            if (control is TextBox || control is ComboBox)
            {
                control.BackColor = control.Enabled ? palette.InputBack : palette.StripBack;
                control.ForeColor = palette.Text;
                var combo = control as ComboBox;
                if (combo != null) combo.FlatStyle = palette.IsDark ? FlatStyle.Flat : FlatStyle.Standard;
                return;
            }

            var link = control as LinkLabel;
            if (link != null)
            {
                link.LinkColor = palette.Accent;
                link.ActiveLinkColor = palette.Accent;
                link.VisitedLinkColor = palette.Accent;
                link.BackColor = Color.Transparent;
                return;
            }

            if (control is GroupBox || tag == AccentTag)
            {
                control.ForeColor = palette.Accent;
                control.BackColor = palette.Window;
                return;
            }

            if (control is Label || control is CheckBox || control is RadioButton || control is Panel
                || control is TableLayoutPanel || control is FlowLayoutPanel || control is UserControl)
            {
                control.ForeColor = tag == DimTag ? palette.DimText : palette.Text;
                control.BackColor = palette.Window;
            }
        }

        static void ApplyToGrid(DataGridView grid, ThemePalette palette)
        {
            grid.BackgroundColor = palette.Window;
            grid.GridColor = palette.Border;
            grid.DefaultCellStyle.BackColor = palette.InputBack;
            grid.DefaultCellStyle.ForeColor = palette.Text;
            grid.DefaultCellStyle.SelectionBackColor = palette.Highlight;
            grid.DefaultCellStyle.SelectionForeColor = palette.Text;
            grid.AlternatingRowsDefaultCellStyle.BackColor = palette.StripBack;
            // Light: standard Windows column headers, as the ISO Ripper's list views. Dark: themed headers.
            grid.EnableHeadersVisualStyles = !palette.IsDark;
            grid.ColumnHeadersDefaultCellStyle.BackColor = palette.ButtonBack;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = palette.Text;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = palette.ButtonBack;
        }

        public static void ApplyToToolStrip(ToolStrip toolStrip)
        {
            ApplyToToolStrip(toolStrip, current);
        }

        static void ApplyToToolStrip(ToolStrip toolStrip, ThemePalette palette)
        {
            toolStrip.BackColor = palette.StripBack;
            toolStrip.ForeColor = palette.Text;
            toolStrip.Renderer = new ToolStripProfessionalRenderer(new ThemedColorTable(palette));
            foreach (ToolStripItem item in toolStrip.Items) ApplyToToolStripItem(item, palette);
        }

        static void ApplyToToolStripItem(ToolStripItem item, ThemePalette palette)
        {
            item.ForeColor = palette.Text;
            var menuItem = item as ToolStripMenuItem;
            if (menuItem == null) return;
            menuItem.DropDown.BackColor = palette.StripBack;
            foreach (ToolStripItem child in menuItem.DropDownItems) ApplyToToolStripItem(child, palette);
        }

        static bool WindowsUsesDarkApps()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                    return value is int && (int)value == 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        const int DwmwaUseImmersiveDarkMode = 20;

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>Dark title bar on Windows 10 2004+ and Windows 11; ignored on older Windows.</summary>
        static void SetDarkTitleBar(IntPtr handle, bool dark)
        {
            try
            {
                int value = dark ? 1 : 0;
                DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
            }
            catch (Exception)
            {
                // dwmapi missing or attribute unsupported (Windows 7/8): keep the default title bar.
            }
        }

        sealed class ThemedColorTable : ProfessionalColorTable
        {
            readonly ThemePalette palette;

            public ThemedColorTable(ThemePalette palette)
            {
                this.palette = palette;
                UseSystemColors = false;
            }

            public override Color MenuStripGradientBegin { get { return palette.StripBack; } }
            public override Color MenuStripGradientEnd { get { return palette.StripBack; } }
            public override Color StatusStripGradientBegin { get { return palette.StripBack; } }
            public override Color StatusStripGradientEnd { get { return palette.StripBack; } }
            public override Color ToolStripDropDownBackground { get { return palette.StripBack; } }
            public override Color ImageMarginGradientBegin { get { return palette.StripBack; } }
            public override Color ImageMarginGradientMiddle { get { return palette.StripBack; } }
            public override Color ImageMarginGradientEnd { get { return palette.StripBack; } }
            public override Color MenuItemSelected { get { return palette.Highlight; } }
            public override Color MenuItemSelectedGradientBegin { get { return palette.Highlight; } }
            public override Color MenuItemSelectedGradientEnd { get { return palette.Highlight; } }
            public override Color MenuItemPressedGradientBegin { get { return palette.Highlight; } }
            public override Color MenuItemPressedGradientEnd { get { return palette.Highlight; } }
            public override Color MenuItemBorder { get { return palette.Border; } }
            public override Color MenuBorder { get { return palette.Border; } }
            public override Color SeparatorDark { get { return palette.Border; } }
            public override Color SeparatorLight { get { return palette.StripBack; } }
        }
    }
}
