using Shouldly;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// zpp's exit codes are part of its contract - scripts and CI gates branch
    /// on them - so each keeps its value. ProgramExitCodeTests pins the codes a
    /// run of Main can be made to end with; the timeout's cannot be provoked
    /// there without a race, so this is where its value is pinned. ProgramCancellationTests pins the run that ends with the code
    /// of a stop (Ctrl+C), and this is where its value is pinned.
    /// </summary>
    public class ExitCodeTests
    {
        [Theory]
        [InlineData(ExitCode.Success, 0)]
        [InlineData(ExitCode.Failure, 1)]
        [InlineData(ExitCode.UsageError, 2)]
        [InlineData(ExitCode.CompilationErrors, 3)]
        [InlineData(ExitCode.CompilationTimeout, 4)]
        [InlineData(ExitCode.ServerFailure, 5)]
        [InlineData(ExitCode.Cancelled, 130)] // 128 + SIGINT (2): what a shell gives a program that Ctrl+C stopped.
        public void ExitCode_Given_ACode_Then_ItKeepsItsValue(ExitCode exitCode, int value)
        {
            ((int)exitCode).ShouldBe(value);
        }
    }
}
