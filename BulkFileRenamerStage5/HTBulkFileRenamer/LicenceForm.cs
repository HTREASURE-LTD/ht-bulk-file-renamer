using System.Drawing;
using System.Windows.Forms;

namespace HTBulkFileRenamer
{
    // Shows the End-User Licence Agreement embedded in the exe (the repository's LICENSE file).
    public class LicenceForm : Form
    {
        public LicenceForm()
        {
            Text = "Licence Agreement — " + Brand.ProductName;
            Icon = Brand.ProductIcon;
            Font = new Font("Segoe UI", 9F);
            Size = new Size(720, 600);
            MinimumSize = new Size(480, 360);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;

            var text = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true,
                Dock = DockStyle.Fill, Text = Brand.LicenceText.Replace("\r\n", "\n").Replace("\n", "\r\n"),
                BorderStyle = BorderStyle.FixedSingle, TabStop = false
            };
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 12, 12, 0) };
            body.Controls.Add(text);

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Size = new Size(92, 30), Tag = ThemeManager.PrimaryTag, Margin = new Padding(6, 1, 0, 1) };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(10, 8, 12, 5), FlowDirection = FlowDirection.RightToLeft };
            buttons.Controls.Add(ok);

            Controls.Add(body);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = ok;
            ThemeManager.Apply(this);
            // Leave the caret at the top rather than with all the text selected.
            Shown += (s, e) => { text.SelectionStart = 0; text.SelectionLength = 0; ok.Focus(); };
        }
    }
}
