namespace Zametek.ProjectPlan.CommandLine
{
    // zpp's own writes to stdout. Their lines end with "\n" on every platform, so that a run prints the same bytes on
    // Windows as on Linux, where Console.Out would otherwise end them with the platform's line end. Text that arrives
    // with the platform's line ends - the metrics tables and the compilation output are built that way - is converted on
    // the way through. CommandLineParser's help text is left as it writes it, and stderr is left alone.
    internal static class StandardOutput
    {
        private const string c_LineEnd = "\n";

        public static void WriteLine()
        {
            Console.Out.Write(c_LineEnd);
        }

        public static void WriteLine(string text)
        {
            Console.Out.Write(ToLineEnd(text) + c_LineEnd);
        }

        public static async Task WriteLineAsync(string text)
        {
            await Console.Out.WriteAsync(ToLineEnd(text) + c_LineEnd);
        }

        private static string ToLineEnd(string text)
        {
            return text.Replace("\r\n", c_LineEnd, StringComparison.Ordinal);
        }
    }
}
