using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Zametek.ProjectPlan.Engine.Tests
{
    // Turns an output into something two runs of the same job can be compared by, leaving out only what is
    // known to differ between runs and is no fault of the job's.
    internal static class OutputComparison
    {
        public static string Comparable(JobOutput output, byte[] content)
        {
            if (output == JobOutput.ScenarioExport)
            {
                return WorkbookEntries(content);
            }

            string text = Encoding.UTF8.GetString(content);
            return text.Contains(@"<svg", StringComparison.Ordinal)
                ? WithClipPathsRenumbered(content)
                : Convert.ToBase64String(content);
        }

        // Skia names each clip path in an SVG after a counter that lives as long as
        // the process, so the same chart gets different ids depending on how much
        // was drawn before it, by any job. Numbering them in the order they appear
        // leaves everything else to compare.
        public static string WithClipPathsRenumbered(byte[] svg)
        {
            var numbers = new Dictionary<string, int>();
            return Regex.Replace(
                Encoding.UTF8.GetString(svg),
                @"(?<=id=""|url\(#)cl_[0-9a-f]+",
                match =>
                {
                    if (!numbers.TryGetValue(match.Value, out int number))
                    {
                        number = numbers.Count;
                        numbers.Add(match.Value, number);
                    }
                    return $@"cl_{number}";
                });
        }

        // A workbook's entries, all but its core properties, which NPOI stamps with
        // the time the workbook was created.
        private static string WorkbookEntries(byte[] workbook)
        {
            using var archive = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
            var entries = new StringBuilder();

            foreach (ZipArchiveEntry entry in archive.Entries.Where(x => x.FullName != @"docProps/core.xml"))
            {
                using Stream stream = entry.Open();
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                entries.Append(entry.FullName).Append(':').AppendLine(Convert.ToBase64String(copy.ToArray()));
            }

            return entries.ToString();
        }
    }
}
