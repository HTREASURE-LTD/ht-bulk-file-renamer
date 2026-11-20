using HTREASURE.Branding;
using HTREASURE.Branding.WinForms;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace HTBulkFileRenamer
{
    // About box in the HTREASURE design template used by HT ISO Ripper: logo, product name, tagline,
    // publisher, version, copyright and company details, a website link and one primary button.
    public class AboutForm : Form
    {
        public AboutForm()
        {
            Text = "About " + BrandInfo.ProductName;
            Icon = BrandWinForms.ProductIcon;
            Font = new Font("Segoe UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var details = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            details.Controls.Add(MakeLabel(BrandInfo.ProductName, ThemeManager.AccentTag, 0, new Font("Segoe UI Semibold", 15F, FontStyle.Bold)));
            details.Controls.Add(MakeLabel(BrandInfo.ProductDescription, ThemeManager.DimTag, 0));
            details.Controls.Add(MakeLabel("Developed by " + BrandInfo.Publisher, null, 14));
            details.Controls.Add(MakeLabel("Version " + BrandInfo.ProductVersion + "  (" + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription + ", " + (Environment.Is64BitProcess ? "64-bit" : "32-bit") + ")", null, 0));
            details.Controls.Add(MakeLabel(BrandInfo.Copyright, null, 0));
            // No office address is shown (HTREASURE decision); the company number identifies the company.
            details.Controls.Add(MakeLabel(BrandInfo.RegistrationLine, ThemeManager.DimTag, 14));
            details.Controls.Add(MakeLabel("Files are renamed only after you confirm, and every rename batch can be undone. "
                + "No telemetry is collected and no internet connection is needed.", ThemeManager.DimTag, 14));
            var website = new LinkLabel { Text = "www.htreasure.com", AutoSize = true, Margin = new Padding(0, 10, 0, 2) };
            website.LinkClicked += (s, e) => MainForm.OpenWebsite(this);
            details.Controls.Add(website);
            var licence = new LinkLabel { Text = "End-User Licence Agreement", AutoSize = true, Margin = new Padding(0, 4, 0, 2) };
            licence.LinkClicked += (s, e) => MainForm.ShowLicence(this);
            details.Controls.Add(licence);

            var icon = new BrandImageBox { Image = BrandWinForms.ProductImage, Size = new Size(64, 64), Margin = new Padding(0, 4, 20, 0) };
            var logo = new BrandImageBox { Image = BrandWinForms.GetLogo(ThemeManager.Current.IsDark ? LogoVariant.HorizontalColorDark : LogoVariant.HorizontalColor, 1024), ImageAlign = ContentAlignment.MiddleLeft, Size = new Size(200, 42), Anchor = AnchorStyles.Left, Margin = new Padding(0) };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Size = new Size(92, 30), Anchor = AnchorStyles.Right, Margin = new Padding(24, 0, 0, 0), Tag = ThemeManager.PrimaryTag };

            var bottom = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 20, 0, 0), Padding = new Padding(0, 14, 0, 0) };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.Controls.Add(logo, 0, 0);
            bottom.Controls.Add(ok, 1, 0);
            bottom.Paint += (s, e) => { using (var pen = new Pen(ThemeManager.Current.Border)) e.Graphics.DrawLine(pen, 0, 0, bottom.Width, 0); };

            var root = new TableLayoutPanel { ColumnCount = 2, RowCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(24, 22, 24, 18), Dock = DockStyle.Fill };
            root.Controls.Add(icon, 0, 0);
            root.Controls.Add(details, 1, 0);
            root.Controls.Add(bottom, 0, 1);
            root.SetColumnSpan(bottom, 2);
            Controls.Add(root);

            AcceptButton = ok;
            CancelButton = ok;
            ThemeManager.Apply(this);
        }

        static Label MakeLabel(string text, string tag, int top, Font font = null)
        {
            var label = new Label { Text = text, Tag = tag, AutoSize = true, Margin = new Padding(0, top, 0, 2), MaximumSize = new Size(380, 0), UseMnemonic = false };
            if (font != null) label.Font = font;
            return label;
        }
    }
}
