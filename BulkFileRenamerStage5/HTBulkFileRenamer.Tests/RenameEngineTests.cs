using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTBulkFileRenamer.Tests
{
    [TestClass]
    public class RenameEngineTests
    {
        static RenameOptions Plain() { return new RenameOptions { Numbering = false }; }

        static FileEntry Entry(string name, DateTime? modified = null, DateTime? taken = null)
        {
            var entry = new FileEntry { Path = Path.Combine(@"C:\Photos", name), Modified = modified ?? new DateTime(2024, 3, 15, 10, 30, 0), Created = new DateTime(2024, 1, 2) };
            entry.Taken = taken;   // set, so tests never read the (non-existent) file
            return entry;
        }

        static string NewName(FileEntry file, RenameOptions o, long number = 1)
        {
            return Path.GetFileName(RenameEngine.NewPath(file, number, o));
        }

        [TestMethod]
        public void FindReplace_MatchCaseOnlyReplacesExactCase()
        {
            var o = Plain(); o.Find = "beach"; o.Replace = "Sea";
            Assert.AreEqual("IMG Beach.jpg", NewName(Entry("IMG Beach.jpg"), o));
            Assert.AreEqual("img Sea.jpg", NewName(Entry("img beach.jpg"), o));
        }

        [TestMethod]
        public void FindReplace_IgnoreCaseReplacesEveryMatch()
        {
            var o = Plain(); o.MatchCase = false; o.Find = "AA"; o.Replace = "b";
            Assert.AreEqual("bb.txt", NewName(Entry("aAaA.txt"), o));
        }

        [TestMethod]
        public void FindReplace_RegexWithGroups()
        {
            var o = Plain(); o.UseRegex = true; o.MatchCase = false; o.Find = @"^img_(\d+) (\w+)$"; o.Replace = "$2-$1";
            Assert.AreEqual("Beach-0001.jpg", NewName(Entry("IMG_0001 Beach.jpg"), o));
        }

        [TestMethod]
        public void FindReplace_InvalidRegexThrows()
        {
            var o = Plain(); o.UseRegex = true; o.Find = "([";
            Assert.ThrowsException<ArgumentException>(() => NewName(Entry("a.txt"), o));
        }

        [TestMethod]
        public void Numbering_AutomaticPrefixAndNumberToken()
        {
            var o = new RenameOptions();
            Assert.AreEqual("007_a.txt", NewName(Entry("a.txt"), o, 7));
            o.Suffix = "_{n}";
            Assert.AreEqual("a_007.txt", NewName(Entry("a.txt"), o, 7));
        }

        [TestMethod]
        public void Tokens_DatesAndParent()
        {
            var o = Plain(); o.Prefix = "{date:yyyyMMdd}_{created}_{parent}-";
            Assert.AreEqual("20240315_2024-01-02_Photos-a.txt", NewName(Entry("a.txt"), o));
        }

        [TestMethod]
        public void TakenToken_UsesPhotoDateOrFallsBackToModified()
        {
            var o = Plain(); o.Prefix = "{taken:yyyy-MM-dd}_";
            Assert.AreEqual("2019-07-04_a.jpg", NewName(Entry("a.jpg", taken: new DateTime(2019, 7, 4)), o));
            Assert.AreEqual("2024-03-15_b.jpg", NewName(Entry("b.jpg", modified: new DateTime(2024, 3, 15)), o));
            Assert.IsTrue(RenameEngine.UsesToken(o, "taken"));
            Assert.IsFalse(RenameEngine.UsesToken(o, "n"));
        }

        [TestMethod]
        public void Extension_KeepLowerUpperReplaceRemove()
        {
            var o = Plain(); o.Find = "jpg"; o.Replace = "png"; o.Case = 2;
            Assert.AreEqual("PHOTO.Jpg", NewName(Entry("photo.Jpg"), o), "find/replace and case never touch the extension");
            o = Plain(); o.ExtensionMode = 1;
            Assert.AreEqual("photo.jpg", NewName(Entry("photo.JPG"), o));
            o.ExtensionMode = 2;
            Assert.AreEqual("photo.JPG", NewName(Entry("photo.jpg"), o));
            o.ExtensionMode = 3; o.NewExtension = "jpeg";
            Assert.AreEqual("photo.jpeg", NewName(Entry("photo.jpg"), o));
            o.NewExtension = ".txt";
            Assert.AreEqual("notes.txt", NewName(Entry("notes"), o), "adds an extension to a file without one");
            o.NewExtension = "  ";
            Assert.AreEqual("photo", NewName(Entry("photo.jpg"), o), "empty removes the extension");
        }

        [TestMethod]
        public void PlanMoves_ExcludedFilesKeepNamesAndDontUseNumbers()
        {
            var files = new List<FileEntry> { Entry("a.txt"), Entry("b.txt"), Entry("c.txt") };
            files[0].Included = false;
            var names = RenameEngine.PlanMoves(files, new RenameOptions()).Select(m => Path.GetFileName(m.Item2));
            CollectionAssert.AreEqual(new[] { "a.txt", "001_b.txt", "002_c.txt" }, names.ToArray());
        }

        [TestMethod]
        public void Csv_QuotesOnlyWhenNeeded()
        {
            string csv = RenameEngine.ToCsv(new[] { new[] { "a", "b,c", "say \"hi\"", null } });
            Assert.AreEqual("a,\"b,c\",\"say \"\"hi\"\"\",\r\n", csv);
        }

        [TestMethod]
        public void Validate_AcceptsSwapAndCaseOnlyRename()
        {
            using (var folder = new TestFolder())
            {
                string a = folder.File("a.txt"), b = folder.File("b.txt");
                Assert.IsNull(RenameEngine.Validate(new List<Tuple<string, string>> { Tuple.Create(a, b), Tuple.Create(b, a) }));
                Assert.IsNull(RenameEngine.Validate(new List<Tuple<string, string>> { Tuple.Create(a, Path.Combine(folder.Root, "A.TXT")) }));
            }
        }

        [TestMethod]
        public void Validate_RejectsUnsafeNames()
        {
            using (var folder = new TestFolder())
            {
                string a = folder.File("a.txt"), b = folder.File("b.txt");
                Func<string, string> check = name => RenameEngine.Validate(new List<Tuple<string, string>> { Tuple.Create(a, Path.Combine(folder.Root, name)) });
                StringAssert.Contains(check(@"..\a.txt"), @"cannot produce \ or /");
                StringAssert.Contains(check("CON.txt"), "reserved");
                StringAssert.Contains(check("a."), "reserved");
                StringAssert.Contains(check("a*b.txt"), "invalid characters");
                StringAssert.Contains(check("b.txt"), "already in use");
                var both = new List<Tuple<string, string>> { Tuple.Create(a, Path.Combine(folder.Root, "x.txt")), Tuple.Create(b, Path.Combine(folder.Root, "X.txt")) };
                StringAssert.Contains(RenameEngine.Validate(both), "same destination");
            }
        }

        [TestMethod]
        public void Validate_LongPathsDependOnWindowsSetting()
        {
            bool saved = RenameEngine.LongPathsEnabled;
            try
            {
                using (var folder = new TestFolder())
                {
                    string a = folder.File("a.txt");
                    string longName = new string('n', 240 - 4) + ".txt";
                    // Pad the folder so the destination passes 260 characters.
                    var moves = new List<Tuple<string, string>> { Tuple.Create(a, Path.Combine(folder.Root, longName)) };
                    Assume(moves[0].Item2.Length >= 260);

                    RenameEngine.LongPathsEnabled = false;
                    StringAssert.Contains(RenameEngine.Validate(moves), "long path support");
                    RenameEngine.LongPathsEnabled = true;
                    Assert.IsNull(RenameEngine.Validate(moves));
                }
            }
            finally { RenameEngine.LongPathsEnabled = saved; }
        }

        static void Assume(bool condition)
        {
            if (!condition) Assert.Inconclusive("Temp folder path is too short for this test.");
        }
    }
}
