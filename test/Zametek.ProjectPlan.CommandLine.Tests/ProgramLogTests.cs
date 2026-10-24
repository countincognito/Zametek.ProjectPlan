using Serilog;
using Shouldly;
using System.Text.RegularExpressions;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for the log of zpp itself, which is written to the console's stderr: a line of it is one line, whatever is in the
    /// values that it says, such as the title of a scenario in a project that somebody else made. In the same collection as
    /// ProgramExitCodeTests, because they swap that stderr.
    /// </summary>
    [Collection(ProgramExitCodeTests.CollectionName)]
    public class ProgramLogTests
    {
        // What the log writes on stderr while the action runs.
        private static string Capture(Action action)
        {
            ILogger previous = Log.Logger;
            TextWriter original = Console.Error;

            try
            {
                using var error = new StringWriter();
                Console.SetError(error);
                action();
                (Log.Logger as IDisposable)?.Dispose();
                return error.ToString();
            }
            finally
            {
                Console.SetError(original);
                Log.Logger = previous;
            }
        }

        [Fact]
        public void ConfigureSerilog_Given_AValueWithLineEnds_Then_OneLineThatKeepsThemEscaped()
        {
            string log = Capture(() =>
            {
                Program.ConfigureSerilog(verbose: true);
                Log.Information(@"Loading scenario {ScenarioTitle}", "Plan\r\n[12:00:00 ERR] forged\x1b[31m");
            });

            string escaped = Regex.Escape(@"Plan\r\n[12:00:00 ERR] forged" + @"\u" + "001b[31m");
            log.ShouldMatch($@"^\[\d\d:\d\d:\d\d INF\] Loading scenario {escaped}\r?\n$");
        }

        [Fact]
        public void ConfigureSerilog_Given_AWarningWithATextWithNoControlCharacter_Then_TheLineAsItWas()
        {
            string log = Capture(() =>
            {
                Program.ConfigureSerilog(verbose: false);
                Log.Warning(@"Compilation failed with {CompilationErrorCount} error(s) in {Title}", 2, "C:\\plans\\a b.zpp");
            });

            log.ShouldMatch(@"^\[\d\d:\d\d:\d\d WRN\] Compilation failed with 2 error\(s\) in C:\\plans\\a b\.zpp\r?\n$");
        }
    }
}
