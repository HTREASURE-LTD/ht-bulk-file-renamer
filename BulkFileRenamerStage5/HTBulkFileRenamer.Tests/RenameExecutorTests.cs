using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTBulkFileRenamer.Tests
{
    [TestClass]
    public class RenameExecutorTests
    {
        static bool Apply(List<Tuple<string, string>> moves, out string error, Func<bool> cancel = null)
        {
            return RenameExecutor.Apply(moves, cancel ?? (() => false), done => { }, out error);
        }

        static Tuple<string, string> Move(TestFolder folder, string from, string to)
        {
            return Tuple.Create(Path.Combine(folder.Root, from), Path.Combine(folder.Root, to));
        }

        [TestMethod]
        public void RenamesFiles()
        {
            using (var folder = new TestFolder())
            {
                folder.File("a.txt", "A"); folder.File("b.txt", "B");
                string error;
                Assert.IsTrue(Apply(new List<Tuple<string, string>> { Move(folder, "a.txt", "001_a.txt"), Move(folder, "b.txt", "002_b.txt") }, out error), error);
                CollectionAssert.AreEqual(new[] { "001_a.txt", "002_b.txt" }, folder.Names());
                Assert.AreEqual("A", File.ReadAllText(Path.Combine(folder.Root, "001_a.txt")));
            }
        }

        [TestMethod]
        public void SwapsAndCyclesWithoutLosingContent()
        {
            using (var folder = new TestFolder())
            {
                folder.File("1.txt", "one"); folder.File("2.txt", "two"); folder.File("3.txt", "three");
                string error;
                Assert.IsTrue(Apply(new List<Tuple<string, string>> { Move(folder, "1.txt", "2.txt"), Move(folder, "2.txt", "3.txt"), Move(folder, "3.txt", "1.txt") }, out error), error);
                Assert.AreEqual("three", File.ReadAllText(Path.Combine(folder.Root, "1.txt")));
                Assert.AreEqual("one", File.ReadAllText(Path.Combine(folder.Root, "2.txt")));
                Assert.AreEqual("two", File.ReadAllText(Path.Combine(folder.Root, "3.txt")));
            }
        }

        [TestMethod]
        public void CaseOnlyRename()
        {
            using (var folder = new TestFolder())
            {
                folder.File("photo.JPG");
                string error;
                Assert.IsTrue(Apply(new List<Tuple<string, string>> { Move(folder, "photo.JPG", "photo.jpg") }, out error), error);
                CollectionAssert.AreEqual(new[] { "photo.jpg" }, folder.Names());
            }
        }

        [TestMethod]
        public void LockedFileRollsBackWholeBatch()
        {
            using (var folder = new TestFolder())
            {
                folder.File("a.txt"); folder.File("b.txt"); folder.File("c.txt");
                // A file open without delete sharing cannot be moved, so the batch fails part-way.
                using (new FileStream(Path.Combine(folder.Root, "c.txt"), FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    string error;
                    Assert.IsFalse(Apply(new List<Tuple<string, string>> { Move(folder, "a.txt", "x_a.txt"), Move(folder, "b.txt", "x_b.txt"), Move(folder, "c.txt", "x_c.txt") }, out error));
                    StringAssert.Contains(error, "Rollback attempted");
                }
                CollectionAssert.AreEqual(new[] { "a.txt", "b.txt", "c.txt" }, folder.Names(), "originals restored, no temporary files left");
            }
        }

        [TestMethod]
        public void CancelDuringStagingRestoresEverything()
        {
            using (var folder = new TestFolder())
            {
                folder.File("a.txt"); folder.File("b.txt");
                int checks = 0;
                string error;
                Assert.IsFalse(Apply(new List<Tuple<string, string>> { Move(folder, "a.txt", "x_a.txt"), Move(folder, "b.txt", "x_b.txt") }, out error, () => ++checks == 2));
                StringAssert.Contains(error, "cancelled");
                CollectionAssert.AreEqual(new[] { "a.txt", "b.txt" }, folder.Names());
            }
        }

        [TestMethod]
        public void ReportsEveryStep()
        {
            using (var folder = new TestFolder())
            {
                folder.File("a.txt"); folder.File("b.txt");
                var reported = new List<int>();
                string error;
                RenameExecutor.Apply(new List<Tuple<string, string>> { Move(folder, "a.txt", "c.txt"), Move(folder, "b.txt", "d.txt") }, () => false, reported.Add, out error);
                CollectionAssert.AreEqual(new[] { 0, 1, 3, 4 }, reported);
            }
        }
    }
}
