using System.Drawing;
using System.Windows.Forms;

namespace HTBulkFileRenamer
{
    // Standard HTREASURE About box (follows the kit's BrandAboutForm): product icon, name, version,
    // copyright, publisher and company details, with the corporate logo.
    public class AboutForm : Form
    {
        public AboutForm()
        {
            Text = "About " + Brand.ProductName;
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Icon = Brand.ProductIcon;

            var details = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0), MaximumSize = new Size(380, 0) };
            details.Controls.Add(MakeLabel(Brand.ProductName, new Font("Segoe UI", 15F, FontStyle.Bold), Brand.DeepNavy, 0));
            details.Controls.Add(MakeLabel("Version " + Brand.ProductVersion, null, Brand.MutedText, 0));
            if (!string.IsNullOrWhiteSpace(Brand.ProductDescription))
                details.Controls.Add(MakeLabel(Brand.ProductDescription, null, Brand.DeepNavy, 10));
            details.Controls.Add(MakeLabel(Brand.Copyright, null, Brand.DeepNavy, 14));
            details.Controls.Add(MakeLabel("Publisher: " + Brand.Publisher, null, Brand.MutedText, 14));
            details.Controls.Add(MakeLabel(Brand.RegistrationLine, null, Brand.MutedText, 0));
            details.Controls.Add(MakeLabel(Brand.RegisteredAddress, null, Brand.MutedText, 0));

            var icon = new BrandImageBox { Image = Brand.ProductImage, Size = new Size(64, 64), Margin = new Padding(0, 4, 20, 0) };
            var logo = new BrandImageBox { Image = Brand.Logo, ImageAlign = ContentAlignment.MiddleLeft, Size = new Size(200, 42), Anchor = AnchorStyles.Left, Margin = new Padding(0) };
            var ok = new Button
            {
                Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(88, 30), Anchor = AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat, BackColor = Brand.TechnologyBlue, ForeColor = Color.White, Margin = new Padding(24, 0, 0, 0)
            };
            ok.FlatAppearance.BorderSize = 0;

            var bottom = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 22, 0, 0), Padding = new Padding(0, 16, 0, 0) };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.Controls.Add(logo, 0, 0);
            bottom.Controls.Add(ok, 1, 0);
            bottom.Paint += (s, e) => { using (var pen = new Pen(Brand.LineGrey)) e.Graphics.DrawLine(pen, 0, 0, bottom.Width, 0); };

            var root = new TableLayoutPanel { ColumnCount = 2, RowCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(24, 24, 24, 20), Dock = DockStyle.Fill };
            root.Controls.Add(icon, 0, 0);
            root.Controls.Add(details, 1, 0);
            root.Controls.Add(bottom, 0, 1);
            root.SetColumnSpan(bottom, 2);
            Controls.Add(root);

            AcceptButton = ok;
            CancelButton = ok;
        }

        static Label MakeLabel(string text, Font font, Color colour, int top)
        {
            var label = new Label { Text = text, AutoSize = true, ForeColor = colour, Margin = new Padding(0, top, 0, 2), MaximumSize = new Size(380, 0), UseMnemonic = false };
            if (font != null) label.Font = font;
            return label;
        }
    }
}
