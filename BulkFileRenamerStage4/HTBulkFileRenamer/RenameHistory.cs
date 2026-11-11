using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace HTBulkFileRenamer
{
    public class HistoryBatch
    {
        public string Id, Operation, UndoOf;
        public DateTime TimeUtc;
        public bool Legacy, Undone;
        public readonly List<Tuple<string, string>> Moves = new List<Tuple<string, string>>();
    }

    // history.tsv line: timeUtc \t operation \t base64(from) \t base64(to) \t batchId \t undoOfBatchId
    // Stage 3 wrote only the first four columns.
    public static class RenameHistory
    {
        public static string DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HTSoftware", "BulkFileRenamer");

        public static string LogPath { get { return Path.Combine(DataDirectory, "history.tsv"); } }

        public static string NewBatchId() { return Guid.NewGuid().ToString("N"); }

        // Returns null on success, or a warning to show the user.
        public static string Append(string operation, IList<Tuple<string, string>> moves, string batchId, string undoOf)
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);
                string time = DateTime.UtcNow.ToString("o");
                using (var writer = new StreamWriter(LogPath, true, Encoding.UTF8))
                {
                    foreach (var move in moves)
                        writer.WriteLine(string.Join("\t", time, operation, Encode(move.Item1), Encode(move.Item2), batchId, undoOf ?? ""));
                }
                return null;
            }
            catch (Exception ex) { return "History could not be saved: " + ex.Message; }
        }

        public static List<HistoryBatch> Load()
        {
            var batches = new List<HistoryBatch>();
            if (!File.Exists(LogPath)) return batches;
            HistoryBatch current = null;
            DateTime lastTime = DateTime.MinValue;
            foreach (string line in File.ReadLines(LogPath, Encoding.UTF8))
            {
                var parts = line.Split('\t');
                if (parts.Length < 4) continue;
                DateTime time;
                if (!DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out time)) continue;
                string from, to;
                try { from = Decode(parts[2]); to = Decode(parts[3]); }
                catch (FormatException) { continue; }
                string id = parts.Length > 4 && parts[4].Length > 0 ? parts[4] : null;
                string undoOf = parts.Length > 5 && parts[5].Length > 0 ? parts[5] : null;
                // Stage 3 lines carry no batch id: group consecutive lines of one operation written within 2 seconds.
                bool sameBatch = current != null && (id != null
                    ? id == current.Id
                    : current.Legacy && current.Operation == parts[1] && (time - lastTime).TotalSeconds < 2);
                if (!sameBatch)
                {
                    current = new HistoryBatch { Id = id ?? "legacy-" + batches.Count, Legacy = id == null, Operation = parts[1], TimeUtc = time, UndoOf = undoOf };
                    batches.Add(current);
                }
                current.Moves.Add(Tuple.Create(from, to));
                lastTime = time;
            }
            MarkUndone(batches);
            return batches;
        }

        static void MarkUndone(List<HistoryBatch> batches)
        {
            var byId = new Dictionary<string, HistoryBatch>();
            for (int i = 0; i < batches.Count; i++)
            {
                var batch = batches[i];
                byId[batch.Id] = batch;
                if (batch.Operation != "UNDO") continue;
                if (batch.UndoOf == null && batch.Legacy)
                {
                    // Stage 3 undo lines don't name their batch; match the latest rename they exactly reverse.
                    for (int j = i - 1; j >= 0; j--)
                    {
                        var candidate = batches[j];
                        if (candidate.Operation == "RENAME" && !candidate.Undone && IsReverse(candidate, batch)) { batch.UndoOf = candidate.Id; break; }
                    }
                }
                HistoryBatch target;
                if (batch.UndoOf != null && byId.TryGetValue(batch.UndoOf, out target)) target.Undone = true;
            }
        }

        static bool IsReverse(HistoryBatch rename, HistoryBatch undo)
        {
            if (rename.Moves.Count != undo.Moves.Count) return false;
            var forward = new HashSet<string>(rename.Moves.Select(m => m.Item1 + "\0" + m.Item2), StringComparer.OrdinalIgnoreCase);
            return undo.Moves.All(m => forward.Contains(m.Item2 + "\0" + m.Item1));
        }

        static string Encode(string value) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(value)); }
        static string Decode(string value) { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
    }
}
