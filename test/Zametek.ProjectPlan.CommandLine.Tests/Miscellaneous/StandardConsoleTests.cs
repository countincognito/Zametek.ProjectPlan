using Shouldly;
using Xunit;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for the bytes zpp's own console writes: lines on stdout end with
    /// NewLineHelper.NewLine whatever the platform, a displayed block comes after
    /// a blank line, and stderr keeps the platform's line end. Windows' own line
    /// end is "\r\n", which is where these tests have teeth.
    /// </summary>
    public class StandardConsoleTests
    {
        private static readonly string s_TwoLinesFromWindows = @"first" + NewLineHelper.WindowsNewLine + @"second";

        [Fact]
        public async Task WriteLineAsync_Given_TextWithWindowsLineEnds_Then_EveryLineEndsWithNewLine()
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var console = new StandardConsole(output, error);

            await console.WriteLineAsync(s_TwoLinesFromWindows);

            output.ToString().ShouldBe(NewLineHelper.JoinLines(@"first", @"second") + NewLineHelper.NewLine);
            error.ToString().ShouldBeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DisplayAsync_Given_ABlock_Then_ItComesAfterABlankLineOnStdout(bool hasErrors)
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var console = new StandardConsole(output, error);

            await console.DisplayAsync(s_TwoLinesFromWindows, hasErrors);

            output.ToString().ShouldBe(NewLineHelper.JoinLines(string.Empty, @"first", @"second") + NewLineHelper.NewLine);
            error.ToString().ShouldBeEmpty();
        }

        [Fact]
        public async Task WriteErrorLineAsync_Given_Text_Then_ItEndsWithThePlatformsLineEndOnStderr()
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var console = new StandardConsole(output, error);

            await console.WriteErrorLineAsync(@"Disk full");

            error.ToString().ShouldBe(@"Disk full" + Environment.NewLine);
            output.ToString().ShouldBeEmpty();
        }
    }
}
