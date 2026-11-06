namespace Zametek.ViewModel.ProjectPlan
{
    // The line end the application writes into its text - saved project files, graph labels and chart annotations,
    // and everything zpp prints - on every platform, so that the same input produces the same bytes on Windows as on
    // Linux. Environment.NewLine, which StringBuilder.AppendLine, TextWriter.WriteLine and most libraries use, is
    // "\r\n" on Windows, so text built that way passes through NormalizeNewLines. Shared by the view models, the views
    // and the command line; the graph library keeps its own copy, so that it stays standalone.
    public static class NewLineHelper
    {
        public const char LineFeed = '\n';
        public const char CarriageReturn = '\r';

        // The line end the application writes.
        public const string NewLine = "\n";

        // The line end Windows uses, and so what Environment.NewLine is there.
        public const string WindowsNewLine = "\r\n";

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
    }
}
