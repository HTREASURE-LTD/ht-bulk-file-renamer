using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace HTBulkFileRenamer
{
    public class FileEntry
    {
        public string Path;
        public bool Included = true;
        public DateTime Modified, Created;
        DateTime? taken;
        bool takenRead;

        public static FileEntry FromPath(string path)
        {
            var info = new FileInfo(path);
            return new FileEntry { Path = path, Modified = info.LastWriteTime, Created = info.CreationTime };
        }

        // Photo "date taken" from EXIF; read on first use (only when {taken} is used) and cached. Null if none.
        public DateTime? Taken
        {
            get { if (!takenRead) { taken = PhotoDate.Read(Path); takenRead = true; } return taken; }
            set { taken = value; takenRead = true; }
        }
    }

    public static class PhotoDate
    {
        static readonly string[] Extensions = { ".jpg", ".jpeg", ".jpe", ".jfif", ".tif", ".tiff" };
        // EXIF DateTimeOriginal, then DateTimeDigitized.
        static readonly int[] TagIds = { 0x9003, 0x9004 };

        public static DateTime? Read(string path)
        {
            if (!Extensions.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) return null;
            try
            {
                using (var stream = File.OpenRead(path))
                using (var image = Image.FromStream(stream, false, false))   // headers only; pixels are not decoded
                {
                    foreach (int id in TagIds)
                    {
                        if (!image.PropertyIdList.Contains(id)) continue;
                        string text = Encoding.ASCII.GetString(image.GetPropertyItem(id).Value).TrimEnd('\0', ' ');
                        DateTime value;
                        if (DateTime.TryParseExact(text, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out value)) return value;
                    }
                }
            }
            catch (Exception) { /* unreadable, or not really an image: treat as no photo date */ }
            return null;
        }
    }

    public class RenameOptions
    {
        public string Prefix = "", Suffix = "", Find = "", Replace = "", NewExtension = "";
        public bool MatchCase = true, UseRegex = false, Numbering = true;
        public long Start = 1;
        // ExtensionMode: 0 keep, 1 lowercase, 2 UPPERCASE, 3 replace with NewExtension (empty removes it).
        public int Digits = 3, Case = 0, ExtensionMode = 0;
    }

    // Pure naming and validation rules; no UI and no file moves.
    public static class RenameEngine
    {
        static readonly Regex TokenPattern = new Regex(@"\{(n|date|created|taken|parent)(?::([^{}]*))?\}", RegexOptions.IgnoreCase);

        public const string TokenHelp = "Tokens for prefix and suffix:\n{n}  number (replaces the automatic 001_ position)\n{date}  modified date\n{created}  created date\n{taken}  photo date taken (JPEG/TIFF EXIF; modified date if a file has none)\n{parent}  folder name\nDates take a format, e.g. {taken:yyyyMMdd} or {date:yyyy-MM-dd_HHmm}.";

        // Paths of 260+ characters work only when Windows long path support is on (the app's manifest opts in).
        public static bool LongPathsEnabled = DetectLongPaths();
        const int MaxLongPath = 32000;

        static bool DetectLongPaths()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\FileSystem"))
                    return key != null && Equals(key.GetValue("LongPathsEnabled"), 1);
            }
            catch (Exception) { return false; }   // no permission to read: assume the legacy limit
        }

        public static bool UsesNumberToken(RenameOptions o)
        {
            return UsesToken(o, "n");
        }

        public static bool UsesToken(RenameOptions o, string token)
        {
            return HasToken(o.Prefix, token) || HasToken(o.Suffix, token);
        }

        static bool HasToken(string text, string token)
        {
            return !string.IsNullOrEmpty(text) && TokenPattern.Matches(text).Cast<Match>()
                .Any(m => m.Groups[1].Value.Equals(token, StringComparison.OrdinalIgnoreCase));
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
            string extension = ChangeExtension(Path.GetExtension(file.Path), o);
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

        public static string ChangeExtension(string extension, RenameOptions o)
        {
            switch (o.ExtensionMode)
            {
                case 1: return extension.ToLowerInvariant();
                case 2: return extension.ToUpperInvariant();
                case 3:
                    string replacement = (o.NewExtension ?? "").Trim();
                    return replacement.Length == 0 ? "" : replacement.StartsWith(".") ? replacement : "." + replacement;
                default: return extension;
            }
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
                    case "taken": return (file.Taken ?? file.Modified).ToString(format, CultureInfo.InvariantCulture);
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
            if (LongPathsEnabled)
            {
                if (moves.Any(m => m.Item2.Length >= MaxLongPath)) return "A proposed path is longer than Windows allows.";
            }
            else
            {
                if (moves.Any(m => m.Item2.Length >= 260)) return "A proposed path is 260 characters or longer. Turn on Windows long path support to allow this.";
                // Each file is first moved to a temporary name in its folder; that path must fit too.
                if (moves.Any(m => m.Item1 != m.Item2 && Path.GetDirectoryName(m.Item1).Length + 1 + RenameExecutor.TempNameLength >= 260))
                    return "A folder path is too long to rename in safely (over about 210 characters). Turn on Windows long path support, or use a shorter folder.";
            }
            if (moves.Any(m => Directory.Exists(m.Item2))) return "A proposed destination is already a directory.";
            if (moves.Select(m => m.Item2).Distinct(StringComparer.OrdinalIgnoreCase).Count() != moves.Count) return "Two files would receive the same destination filename.";
            var sources = new HashSet<string>(moves.Select(m => m.Item1), StringComparer.OrdinalIgnoreCase);
            if (moves.Any(m => File.Exists(m.Item2) && !sources.Contains(m.Item2))) return "A destination filename is already in use by another file.";
            return null;
        }

        // RFC 4180 CSV: fields with commas, quotes or line breaks are quoted, quotes doubled.
        public static string ToCsv(IEnumerable<string[]> rows)
        {
            var csv = new StringBuilder();
            foreach (var row in rows)
                csv.Append(string.Join(",", row.Select(field =>
                {
                    field = field ?? "";
                    return field.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + field.Replace("\"", "\"\"") + "\"" : field;
                }))).Append("\r\n");
            return csv.ToString();
        }
    }
}
