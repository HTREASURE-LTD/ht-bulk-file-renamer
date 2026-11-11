using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HTBulkFileRenamer
{
    public class FileEntry
    {
        public string Path;
        public bool Included = true;
        public DateTime Modified, Created;

        public static FileEntry FromPath(string path)
        {
            var info = new FileInfo(path);
            return new FileEntry { Path = path, Modified = info.LastWriteTime, Created = info.CreationTime };
        }
    }

    public class RenameOptions
    {
        public string Prefix = "", Suffix = "", Find = "", Replace = "";
        public bool MatchCase = true, UseRegex = false, Numbering = true;
        public long Start = 1;
        public int Digits = 3, Case = 0;
    }

    // Pure naming and validation rules; no UI and no file moves.
    public static class RenameEngine
    {
        static readonly Regex TokenPattern = new Regex(@"\{(n|date|created|parent)(?::([^{}]*))?\}", RegexOptions.IgnoreCase);

        public const string TokenHelp = "Tokens for prefix and suffix:\n{n}  number (replaces the automatic 001_ position)\n{date}  modified date\n{created}  created date\n{parent}  folder name\nDates take a format, e.g. {date:yyyyMMdd} or {date:yyyy-MM-dd_HHmm}.";

        public static bool UsesNumberToken(RenameOptions o)
        {
            return HasNumberToken(o.Prefix) || HasNumberToken(o.Suffix);
        }

        static bool HasNumberToken(string text)
        {
            return !string.IsNullOrEmpty(text) && TokenPattern.Matches(text).Cast<Match>()
                .Any(m => m.Groups[1].Value.Equals("n", StringComparison.OrdinalIgnoreCase));
        }

        // Excluded files keep their name; included files are numbered in list order.
        public static List<Tuple<string, string>> PlanMoves(IList<FileEntry> files, RenameOptions o)
        {
            var moves = new List<Tuple<string, string>>(files.Count);
            long number = o.Start;
            foreach (var file in files)
                moves.Add(Tuple.Create(file.Path, file.Included ? NewPath(file, number++, o) : file.Path));
            return moves;
        }

        public static string NewPath(FileEntry file, long number, RenameOptions o)
        {
            string name = Path.GetFileNameWithoutExtension(file.Path);
            string extension = Path.GetExtension(file.Path);
            name = FindReplace(name, o);
            switch (o.Case)
            {
                case 1: name = name.ToLowerInvariant(); break;
                case 2: name = name.ToUpperInvariant(); break;
                case 3: name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower()); break;
            }
            string formatted = number.ToString("D" + o.Digits, CultureInfo.InvariantCulture);
            string automatic = o.Numbering && !UsesNumberToken(o) ? formatted + "_" : "";
            string prefix = ExpandTokens(o.Prefix, file, formatted), suffix = ExpandTokens(o.Suffix, file, formatted);
            return Path.Combine(Path.GetDirectoryName(file.Path), prefix + automatic + name + suffix + extension);
        }

        public static string FindReplace(string name, RenameOptions o)
        {
            if (string.IsNullOrEmpty(o.Find)) return name;
            if (o.UseRegex)
            {
                // Timeout guards against patterns with catastrophic backtracking freezing the preview.
                var options = o.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
                return Regex.Replace(name, o.Find, o.Replace ?? "", options, TimeSpan.FromSeconds(1));
            }
            if (o.MatchCase) return name.Replace(o.Find, o.Replace ?? "");
            var result = new StringBuilder();
            int position = 0, hit;
            while ((hit = name.IndexOf(o.Find, position, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                result.Append(name, position, hit - position).Append(o.Replace);
                position = hit + o.Find.Length;
            }
            return result.Append(name, position, name.Length - position).ToString();
        }

        public static string ExpandTokens(string text, FileEntry file, string number)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text ?? "";
            return TokenPattern.Replace(text, m =>
            {
                string format = m.Groups[2].Success && m.Groups[2].Value.Length > 0 ? m.Groups[2].Value : "yyyy-MM-dd";
                switch (m.Groups[1].Value.ToLowerInvariant())
                {
                    case "n": return number;
                    case "date": return file.Modified.ToString(format, CultureInfo.InvariantCulture);
                    case "created": return file.Created.ToString(format, CultureInfo.InvariantCulture);
                    default: return Path.GetFileName(Path.GetDirectoryName(file.Path)) ?? "";
                }
            });
        }

        public static bool IsInvalidWindowsName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.EndsWith(".") || name.EndsWith(" ")) return true;
            string stem = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            var reserved = new[] { "CON", "PRN", "AUX", "NUL" };
            return reserved.Contains(stem) || (stem.Length == 4 &&
                ((stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] >= '1' && stem[3] <= '9'));
        }

        public static string Validate(List<Tuple<string, string>> moves)
        {
            if (moves.Any(m => !File.Exists(m.Item1))) return "One or more source files no longer exist.";
            // A path separator in prefix/suffix/replace would otherwise move files into another folder.
            if (moves.Any(m => !string.Equals(Path.GetDirectoryName(m.Item1), Path.GetDirectoryName(m.Item2), StringComparison.OrdinalIgnoreCase)))
                return @"Prefix, suffix, tokens and replacement text cannot produce \ or /.";
            if (moves.Any(m => string.IsNullOrWhiteSpace(Path.GetFileName(m.Item2)))) return "A proposed filename is empty.";
            if (moves.Any(m => Path.GetFileName(m.Item2).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)) return "A proposed filename contains invalid characters.";
            if (moves.Any(m => IsInvalidWindowsName(Path.GetFileName(m.Item2)))) return "A filename is reserved, ends with a dot/space, or is invalid on Windows.";
            if (moves.Any(m => Path.GetFileName(m.Item2).Length > 240)) return "A proposed filename is too long (limit 240 characters for this tool).";
            if (moves.Any(m => m.Item2.Length >= 260)) return "A proposed path exceeds 259 characters (legacy Windows compatibility limit).";
            if (moves.Any(m => Directory.Exists(m.Item2))) return "A proposed destination is already a directory.";
            if (moves.Select(m => m.Item2).Distinct(StringComparer.OrdinalIgnoreCase).Count() != moves.Count) return "Two files would receive the same destination filename.";
            var sources = new HashSet<string>(moves.Select(m => m.Item1), StringComparer.OrdinalIgnoreCase);
            if (moves.Any(m => File.Exists(m.Item2) && !sources.Contains(m.Item2))) return "A destination filename is already in use by another file.";
            return null;
        }
    }
}
