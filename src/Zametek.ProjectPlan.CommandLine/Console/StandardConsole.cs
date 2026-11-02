using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // zpp's own console, on the writers it is given - stdout and stderr. Lines on stdout end with NewLineHelper.NewLine
    // on every platform, so that a run prints the same bytes on Windows as on Linux, and text that arrives with the
    // platform's line ends - the metrics tables and the compilation output are built that way - is converted on the way
    // through. stderr is left alone, as is CommandLineParser's help text, which zpp prints as the parser writes it. On a
    // terminal, a displayed block shows in green, or in red when it reports errors.
    internal class StandardConsole
        : IJobConsole
    {
        #region Fields

        private readonly TextWriter m_Output;
        private readonly TextWriter m_Error;

        #endregion

        #region Ctors

        public StandardConsole(
            TextWriter output,
            TextWriter error)
        {
            ArgumentNullException.ThrowIfNull(output);
            ArgumentNullException.ThrowIfNull(error);
            m_Output = output;
            m_Error = error;
        }

        #endregion

        #region IJobConsole Members

        public async Task WriteLineAsync(string text)
        {
            await m_Output.WriteAsync(NewLineHelper.NormalizeNewLines(text) + NewLineHelper.NewLine);
        }

        public async Task DisplayAsync(string content, bool hasErrors)
        {
            if (hasErrors)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
            }
            await m_Output.WriteAsync(NewLineHelper.NewLine);
            await WriteLineAsync(content);
            Console.ResetColor();
        }

        public async Task WriteErrorLineAsync(string text)
        {
            await m_Error.WriteLineAsync(text);
        }

        #endregion
    }
}
