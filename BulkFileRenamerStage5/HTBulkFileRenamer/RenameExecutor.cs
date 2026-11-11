using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HTBulkFileRenamer
{
    // Moves files on disk for a batch that RenameEngine.Validate has accepted. No UI.
    public static class RenameExecutor
    {
        public const string TempPrefix = ".ht-renamer-";
        // ".ht-renamer-" + 32 hex digits + ".tmp"
        public const int TempNameLength = 48;

        // Stage all sources under temporary names to safely handle swaps and cycles, then commit.
        // On error, attempt to roll back both completed and staged moves.
        // report(stepsDone) is called between steps (of moves.Count * 2); cancel is honoured only while staging.
        public static bool Apply(IList<Tuple<string, string>> moves, Func<bool> cancelRequested, Action<int> report, out string error)
        {
            error = null;
            int completed = 0;
            var staged = new List<Tuple<string, string, string>>();
            try
            {
                foreach (var move in moves)
                {
                    report(completed);
                    if (cancelRequested()) throw new OperationCanceledException("Operation cancelled by user.");
                    string temp;
                    do { temp = Path.Combine(Path.GetDirectoryName(move.Item1), TempPrefix + Guid.NewGuid().ToString("N") + ".tmp"); }
                    while (File.Exists(temp) || Directory.Exists(temp));
                    File.Move(move.Item1, temp);
                    staged.Add(Tuple.Create(move.Item1, temp, move.Item2));
                    completed++;
                }
                foreach (var item in staged)
                {
                    // Do not interrupt the commit phase: finish moving staged files first.
                    File.Move(item.Item2, item.Item3);
                    completed++;
                    report(completed);
                }
                return true;
            }
            catch (Exception ex)
            {
                var failures = new List<string>();
                foreach (var item in staged.AsEnumerable().Reverse())
                {
                    try
                    {
                        if (File.Exists(item.Item3) && !File.Exists(item.Item2)) File.Move(item.Item3, item.Item2);
                    }
                    catch (Exception rollbackEx) { failures.Add(rollbackEx.Message); }
                }
                foreach (var item in staged.AsEnumerable().Reverse())
                {
                    try { if (File.Exists(item.Item2) && !File.Exists(item.Item1)) File.Move(item.Item2, item.Item1); }
                    catch (Exception rollbackEx) { failures.Add(rollbackEx.Message); }
                }
                error = ex.Message + (failures.Count > 0 ? "\nRollback incomplete. Check the folder for .ht-renamer temporary files.\n" + string.Join("; ", failures) : "\nRollback attempted; verify your files.");
                return false;
            }
        }
    }
}
