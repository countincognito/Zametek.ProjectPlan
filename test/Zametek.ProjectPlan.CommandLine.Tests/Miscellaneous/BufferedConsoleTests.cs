using Shouldly;
using Xunit;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for a job's console on zpp serve: it keeps what zpp's own console
    /// would print, except that stderr's lines end with NewLineHelper.NewLine
    /// too, as stdout's do, whatever the platform.
    /// </summary>
    public class BufferedConsoleTests
    {
        private static readonly string s_TwoLinesFromWindows = @"first" + NewLineHelper.WindowsNewLine + @"second";

        [Fact]
        public async Task WriteLineAsync_Given_TextWithWindowsLineEnds_Then_KeepsItOnStdoutWithEveryLineEndingInNewLine()
        {
            var console = new BufferedConsole();

            await console.WriteLineAsync(s_TwoLinesFromWindows);

            console.Output.ShouldBe(NewLineHelper.JoinLines(@"first", @"second") + NewLineHelper.NewLine);
            console.Error.ShouldBeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DisplayAsync_Given_ABlock_Then_KeepsItOnStdoutAfterABlankLine(bool hasErrors)
        {
            var console = new BufferedConsole();

            await console.DisplayAsync(s_TwoLinesFromWindows, hasErrors);

            console.Output.ShouldBe(NewLineHelper.JoinLines(string.Empty, @"first", @"second") + NewLineHelper.NewLine);
            console.Error.ShouldBeEmpty();
        }

        [Fact]
        public async Task WriteErrorLineAsync_Given_TextWithWindowsLineEnds_Then_KeepsItOnStderrWithEveryLineEndingInNewLine()
        {
            var console = new BufferedConsole();

            await console.WriteErrorLineAsync(s_TwoLinesFromWindows);

            console.Error.ShouldBe(NewLineHelper.JoinLines(@"first", @"second") + NewLineHelper.NewLine);
            console.Output.ShouldBeEmpty();
        }

        [Fact]
        public async Task Output_Given_SeveralWrites_Then_InTheOrderTheyCame()
        {
            var console = new BufferedConsole();

            await console.WriteLineAsync(@"one");
            await console.WriteErrorLineAsync(@"two");
            await console.DisplayAsync(@"three", hasErrors: false);

            console.Output.ShouldBe(NewLineHelper.JoinLines(@"one", string.Empty, @"three") + NewLineHelper.NewLine);
            console.Error.ShouldBe(@"two" + NewLineHelper.NewLine);
        }
    }
}
