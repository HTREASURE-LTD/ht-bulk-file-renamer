using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Serialization;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTBulkFileRenamer.Tests
{
    [TestClass]
    public class PhotoDateTests
    {
        // Writes a small JPEG carrying an EXIF DateTimeOriginal tag, the way a camera would.
        static void SaveJpegTaken(string path, string exifDate)
        {
            using (var bitmap = new Bitmap(8, 8))
            {
                // PropertyItem has no public constructor; GDI+ only needs the fields set.
                var item = (PropertyItem)FormatterServices.GetUninitializedObject(typeof(PropertyItem));
                item.Id = 0x9003;
                item.Type = 2;   // ASCII
                item.Value = Encoding.ASCII.GetBytes(exifDate + "\0");
                item.Len = item.Value.Length;
                bitmap.SetPropertyItem(item);
                bitmap.Save(path, ImageFormat.Jpeg);
            }
        }

        [TestMethod]
        public void ReadsExifDateTaken()
        {
            using (var folder = new TestFolder())
            {
                string path = System.IO.Path.Combine(folder.Root, "photo.jpg");
                SaveJpegTaken(path, "2019:07:04 18:30:05");
                Assert.AreEqual(new DateTime(2019, 7, 4, 18, 30, 5), PhotoDate.Read(path));
                Assert.AreEqual(new DateTime(2019, 7, 4, 18, 30, 5), FileEntry.FromPath(path).Taken);
            }
        }

        [TestMethod]
        public void NoDateForPlainImagesTextAndOtherTypes()
        {
            using (var folder = new TestFolder())
            {
                string plain = System.IO.Path.Combine(folder.Root, "plain.jpg");
                using (var bitmap = new Bitmap(8, 8)) bitmap.Save(plain, ImageFormat.Jpeg);
                Assert.IsNull(PhotoDate.Read(plain));
                Assert.IsNull(PhotoDate.Read(folder.File("fake.jpg", "not an image")));
                Assert.IsNull(PhotoDate.Read(folder.File("notes.txt")));
            }
        }
    }
}
