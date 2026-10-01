using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // zpp's own writes to stdout. Their lines end with NewLineHelper.NewLine on every platform, so that a run prints the
    // same bytes on Windows as on Linux, where Console.Out would otherwise end them with the platform's line end. Text
    // that arrives with the platform's line ends - the metrics tables and the compilation output are built that way - is
    // converted on the way through. CommandLineParser's help text is left as it writes it, and stderr is left alone.
    internal static class StandardOutput
    {
        public static void WriteLine()
        {
            Console.Out.Write(NewLineHelper.NewLine);
        }

        public static void WriteLine(string text)
        {
            Console.Out.Write(NewLineHelper.NormalizeNewLines(text) + NewLineHelper.NewLine);
        }

        public static async Task WriteLineAsync(string text)
        {
            await Console.Out.WriteAsync(NewLineHelper.NormalizeNewLines(text) + NewLineHelper.NewLine);
        }
    }
}
