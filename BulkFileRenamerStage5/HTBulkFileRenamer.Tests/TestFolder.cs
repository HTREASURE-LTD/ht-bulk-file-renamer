using System;
using System.IO;
using System.Linq;

namespace HTBulkFileRenamer.Tests
{
    // A disposable folder of real files under %TEMP%, deleted after each test.
    sealed class TestFolder : IDisposable
    {
        public readonly string Root = Path.Combine(Path.GetTempPath(), "htrenamer-tests", Guid.NewGuid().ToString("N").Substring(0, 8));

        public TestFolder() { Directory.CreateDirectory(Root); }

        public string File(string name, string content = "x")
        {
            string path = Path.Combine(Root, name);
            System.IO.File.WriteAllText(path, content);
            return path;
        }

        public string[] Names()
        {
            return Directory.GetFiles(Root).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
