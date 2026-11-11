using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTBulkFileRenamer.Tests
{
    [TestClass]
    public class RenameHistoryTests
    {
        TestFolder folder;
        string savedDirectory;

        [TestInitialize]
        public void PointHistoryAtTempFolder()
        {
            folder = new TestFolder();
            savedDirectory = RenameHistory.DataDirectory;
            RenameHistory.DataDirectory = folder.Root;
        }

        [TestCleanup]
        public void Restore()
        {
            RenameHistory.DataDirectory = savedDirectory;
            folder.Dispose();
        }

        static string B64(string s) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(s)); }

        [TestMethod]
        public void UndoMarksItsBatchUndone()
        {
            var moves = new List<Tuple<string, string>> { Tuple.Create(@"C:\b\one.txt", @"C:\b\001_one.txt"), Tuple.Create(@"C:\b\two.txt", @"C:\b\002_two.txt") };
            string id = RenameHistory.NewBatchId();
            Assert.IsNull(RenameHistory.Append("RENAME", moves, id, null));
            Assert.IsNull(RenameHistory.Append("UNDO", moves.Select(m => Tuple.Create(m.Item2, m.Item1)).ToList(), RenameHistory.NewBatchId(), id));

            var batches = RenameHistory.Load();
            Assert.AreEqual(2, batches.Count);
            Assert.AreEqual(2, batches[0].Moves.Count);
            Assert.IsTrue(batches[0].Undone);
            Assert.AreEqual(@"C:\b\001_one.txt", batches[0].Moves[0].Item2);
        }

        [TestMethod]
        public void ReadsStage3LinesWithoutBatchIds()
        {
            File.WriteAllLines(RenameHistory.LogPath, new[]
            {
                "2026-11-01T10:00:00.0000000Z\tRENAME\t" + B64(@"C:\a\1.txt") + "\t" + B64(@"C:\a\x1.txt"),
                "2026-11-01T10:00:00.5000000Z\tRENAME\t" + B64(@"C:\a\2.txt") + "\t" + B64(@"C:\a\x2.txt"),
                "2026-11-01T10:00:00.9000000Z\tUNDO\t" + B64(@"C:\a\x1.txt") + "\t" + B64(@"C:\a\1.txt"),
                "2026-11-01T10:00:01.0000000Z\tUNDO\t" + B64(@"C:\a\x2.txt") + "\t" + B64(@"C:\a\2.txt"),
                "2026-11-01T11:00:00.0000000Z\tRENAME\t" + B64(@"C:\a\3.txt") + "\t" + B64(@"C:\a\x3.txt"),
                "not a history line",
            }, new UTF8Encoding(true));

            var shapes = RenameHistory.Load().Select(b => b.Operation + ":" + b.Moves.Count + ":" + b.Undone).ToArray();
            CollectionAssert.AreEqual(new[] { "RENAME:2:True", "UNDO:2:False", "RENAME:1:False" }, shapes);
        }

        [TestMethod]
        public void MissingLogMeansNoHistory()
        {
            Assert.AreEqual(0, RenameHistory.Load().Count);
        }
    }
}
