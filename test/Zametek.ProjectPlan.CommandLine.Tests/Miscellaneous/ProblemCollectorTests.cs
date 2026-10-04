using Shouldly;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for what is wrong with a request, found a problem at a time and kept until all are found: in the order they were
    /// found, and as the request as it was sent, or as what it says.
    /// </summary>
    public class ProblemCollectorTests
    {
        private static ProblemError Error(string code)
        {
            return new ProblemError { Pointer = @"#/options", Code = code, Detail = @"x" };
        }

        [Fact]
        public void New_Given_NothingFound_Then_NoErrorsAndNotMalformed()
        {
            var problems = new ProblemCollector();

            problems.HasErrors.ShouldBeFalse();
            problems.Errors.ShouldBeEmpty();
            problems.IsMalformed.ShouldBeFalse();
        }

        [Fact]
        public void AddInvalid_Given_AProblemWithWhatTheRequestSays_Then_KeptAndTheRequestIsNotMalformed()
        {
            var problems = new ProblemCollector();

            problems.AddInvalid(Error(@"a"));

            problems.HasErrors.ShouldBeTrue();
            problems.Errors.Select(x => x.Code).ShouldBe([@"a"]);
            problems.IsMalformed.ShouldBeFalse();
        }

        [Fact]
        public void AddMalformed_Given_AProblemWithTheRequestAsItWasSent_Then_KeptAndTheRequestIsMalformed()
        {
            var problems = new ProblemCollector();

            problems.AddMalformed(Error(@"a"));

            problems.HasErrors.ShouldBeTrue();
            problems.Errors.Select(x => x.Code).ShouldBe([@"a"]);
            problems.IsMalformed.ShouldBeTrue();
        }

        [Fact]
        public void Add_Given_ProblemsOfBothKinds_Then_AllKeptInTheOrderFoundAndMalformed()
        {
            var problems = new ProblemCollector();

            problems.AddInvalid(Error(@"a"));
            problems.AddMalformed(Error(@"b"));
            problems.AddInvalid(Error(@"c"));

            problems.Errors.Select(x => x.Code).ShouldBe([@"a", @"b", @"c"]);
            problems.IsMalformed.ShouldBeTrue();
        }

        [Fact]
        public void Add_Given_NoProblem_Then_Throws()
        {
            var problems = new ProblemCollector();

            Should.Throw<ArgumentNullException>(() => problems.AddInvalid(null!));
            Should.Throw<ArgumentNullException>(() => problems.AddMalformed(null!));
            problems.HasErrors.ShouldBeFalse();
        }
    }
}
