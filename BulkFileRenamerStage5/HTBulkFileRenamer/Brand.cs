using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace HTBulkFileRenamer
{
    // HTREASURE LTD company identity and product assets, taken from HTREASURE-Branding/brand.json (v1.0.0).
    // Colours and layout come from the HTREASURE design template (ThemeManager, shared with HT ISO Ripper).
    // Product details come from the .csproj.
    public static class Brand
    {
        public const string CompanyName = "HTREASURE LTD";
        public const string Publisher = CompanyName;
        public const string Attribution = "Developed by HTREASURE LTD";
        public const string RegistrationLine = "Company No. 17178941 · Registered in England and Wales";
        public const string RegisteredAddress = "34a Wagg Street, Congleton, England, CW12 4BA";

        static readonly Assembly App = typeof(Brand).Assembly;

        public static string ProductName
        {
            get { var a = App.GetCustomAttribute<AssemblyProductAttribute>(); return a != null ? a.Product : "HT Bulk File Renamer"; }
        }
        public static string ProductDescription
        {
            get { var a = App.GetCustomAttribute<AssemblyDescriptionAttribute>(); return a != null ? a.Description : ""; }
        }
        public static string ProductVersion
        {
            get
            {
                var a = App.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                return a != null && a.InformationalVersion.Length > 0 ? a.InformationalVersion.Split('+')[0] : App.GetName().Version.ToString(3);
            }
        }
        // The year is stamped at build time (<Copyright> in the .csproj), so it never goes stale.
        public static int CopyrightYear
        {
            get
            {
                var a = App.GetCustomAttribute<AssemblyCopyrightAttribute>();
                var match = Regex.Match(a != null ? a.Copyright : "", @"\b(19|20)\d{2}\b");
                return match.Success ? int.Parse(match.Value) : DateTime.Now.Year;
            }
        }
        public static string Copyright { get { return "Copyright © " + CopyrightYear + " " + CompanyName + ". All rights reserved."; } }
        public static string ShortCopyright { get { return "© " + CopyrightYear + " " + CompanyName; } }

        static Icon productIcon;
        static Image productImage, logo, logoDark;
        public static Icon ProductIcon { get { return productIcon ?? (productIcon = new Icon(Open("HT-bulk-file-renamer.ico"))); } }
        public static Image ProductImage { get { return productImage ?? (productImage = LoadImage("HT-bulk-file-renamer-256.png")); } }
        // The kit's "color-dark" logo is for dark backgrounds.
        public static Image LogoFor(bool darkBackground)
        {
            return darkBackground
                ? logoDark ?? (logoDark = LoadImage("HTREASURE-logo-horizontal-color-dark-1024w.png"))
                : logo ?? (logo = LoadImage("HTREASURE-logo-horizontal-color-1024w.png"));
        }

        // End-User Licence Agreement: the repository's LICENSE file, embedded at build time.
        static string licenceText;
        public static string LicenceText
        {
            get
            {
                if (licenceText == null)
                    using (var reader = new StreamReader(Open("LICENSE.txt"), System.Text.Encoding.UTF8))
                        licenceText = reader.ReadToEnd();
                return licenceText;
            }
        }

        static Stream Open(string name)
        {
            var stream = App.GetManifestResourceStream("Branding." + name);
            if (stream == null) throw new FileNotFoundException("Missing embedded brand asset: " + name);
            return stream;
        }
        static Image LoadImage(string name)
        {
            using (var stream = Open(name))
            using (var decoded = Image.FromStream(stream))
                return new Bitmap(decoded);   // detach from the stream
        }

        // Draws an image into a rectangle with high-quality resampling, keeping its aspect ratio.
        public static void DrawImageFit(Graphics g, Image image, Rectangle bounds, ContentAlignment align)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            float scale = Math.Min((float)bounds.Width / image.Width, (float)bounds.Height / image.Height);
            float width = image.Width * scale, height = image.Height * scale;
            float x = align == ContentAlignment.MiddleLeft ? bounds.X : bounds.X + (bounds.Width - width) / 2;
            float y = bounds.Y + (bounds.Height - height) / 2;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(image, x, y, width, height);
        }
    }

    // Shows a large source image scaled smoothly to the control, so icons and logos stay sharp.
    public class BrandImageBox : Control
    {
        Image image;

        public BrandImageBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;
            ImageAlign = ContentAlignment.MiddleCenter;
        }

        [DefaultValue(null)]
        public Image Image { get { return image; } set { image = value; Invalidate(); } }

        public ContentAlignment ImageAlign { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (image != null) Brand.DrawImageFit(e.Graphics, image, ClientRectangle, ImageAlign);
        }
    }
}
