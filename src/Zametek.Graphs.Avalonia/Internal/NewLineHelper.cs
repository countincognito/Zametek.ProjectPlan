namespace Zametek.Graphs.Avalonia
{
    // The line end the graph library writes into its exports on every platform, so that the same diagram exports to the
    // same bytes on Windows as on Linux, and the line ends it recognises when it splits a label into lines. A focused
    // copy of the application's NewLineHelper, kept local so that the library stays standalone, and internal so that it
    // never clashes with the application's public NewLineHelper in consumers that import both namespaces.
    internal static class NewLineHelper
    {
        // The line end the library writes.
        public const string NewLine = "\n";

        // The line end Windows uses, and so what Environment.NewLine is there.
        public const string WindowsNewLine = "\r\n";

        // A carriage return on its own, the line end of classic Mac OS text.
        public const string ClassicMacNewLine = "\r";

        // Every line end a label may arrive with, the two-character one first so that it is never split in half.
        private static readonly string[] s_LineEnds = [WindowsNewLine, ClassicMacNewLine, NewLine];

        public static string JoinLines(params IEnumerable<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);
            return string.Join(NewLine, lines);
        }

        // Converts the Windows line ends in text built with Environment.NewLine to NewLine. Text that already uses
        // NewLine comes back unchanged.
        public static string NormalizeNewLines(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            return text.Replace(WindowsNewLine, NewLine, StringComparison.Ordinal);
        }

        // Splits text into its lines at any line end, keeping empty lines.
        public static string[] SplitLines(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            return text.Split(s_LineEnds, StringSplitOptions.None);
        }
    }
}
