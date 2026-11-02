using Shouldly;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for NestingCounter, which the core and the project scenario manager count their nested sections with. A
    /// section that starts inside another is not the outermost, and only the outermost one changes a signal.
    /// </summary>
    public class NestingCounterTests
    {
        [Fact]
        public void IsActive_Given_ANewCounter_Then_ItIsFalse()
        {
            var counter = new NestingCounter();

            counter.IsActive.ShouldBeFalse();
        }

        [Fact]
        public void Begin_Given_NoSection_Then_ItIsTheOutermostAndTheCounterIsActive()
        {
            var counter = new NestingCounter();

            counter.Begin().ShouldBeTrue();

            counter.IsActive.ShouldBeTrue();
        }

        [Fact]
        public void Begin_Given_ASectionThatIsOpen_Then_ItIsNotTheOutermost()
        {
            var counter = new NestingCounter();
            counter.Begin();

            counter.Begin().ShouldBeFalse();
            counter.Begin().ShouldBeFalse();

            counter.IsActive.ShouldBeTrue();
        }

        [Fact]
        public void End_Given_OneSection_Then_ItIsTheOutermostAndTheCounterIsNotActive()
        {
            var counter = new NestingCounter();
            counter.Begin();

            counter.End().ShouldBeTrue();

            counter.IsActive.ShouldBeFalse();
        }

        [Fact]
        public void End_Given_NestedSections_Then_OnlyTheLastEndIsTheOutermost()
        {
            var counter = new NestingCounter();
            counter.Begin();
            counter.Begin();
            counter.Begin();

            counter.End().ShouldBeFalse();
            counter.End().ShouldBeFalse();
            counter.End().ShouldBeTrue();
        }

        [Fact]
        public void IsActive_Given_NestedSections_Then_ItIsTrueUntilTheLastEnds()
        {
            var counter = new NestingCounter();
            counter.Begin();
            counter.Begin();

            counter.End();
            counter.IsActive.ShouldBeTrue();

            counter.End();
            counter.IsActive.ShouldBeFalse();
        }

        [Fact]
        public void End_Given_NoSection_Then_ItChangesNothingAndIsNotTheOutermost()
        {
            var counter = new NestingCounter();

            counter.End().ShouldBeFalse();

            counter.IsActive.ShouldBeFalse();
        }

        [Fact]
        public void Begin_Given_MoreEndsThanBegins_Then_TheNextBeginIsStillTheOutermost()
        {
            var counter = new NestingCounter();
            counter.Begin();
            counter.End();

            // Three calls with no section to end must not take the count below zero.
            counter.End().ShouldBeFalse();
            counter.End().ShouldBeFalse();
            counter.End().ShouldBeFalse();

            counter.Begin().ShouldBeTrue();
            counter.IsActive.ShouldBeTrue();
            counter.End().ShouldBeTrue();
            counter.IsActive.ShouldBeFalse();
        }

        [Fact]
        public void Begin_Given_AnotherSectionAfterTheFirstEnded_Then_ItIsTheOutermostAgain()
        {
            var counter = new NestingCounter();

            counter.Begin().ShouldBeTrue();
            counter.End().ShouldBeTrue();
            counter.Begin().ShouldBeTrue();
            counter.End().ShouldBeTrue();
        }

        [Fact]
        public void Counters_Given_TwoOfThem_Then_EachCountsItsOwnSections()
        {
            var first = new NestingCounter();
            var second = new NestingCounter();

            first.Begin().ShouldBeTrue();
            second.Begin().ShouldBeTrue();

            first.End().ShouldBeTrue();
            first.IsActive.ShouldBeFalse();
            second.IsActive.ShouldBeTrue();
        }

        [Fact]
        public async Task BeginAndEnd_Given_ManyThreads_Then_TheOutermostChangesPairUpAndTheCounterEndsAtZero()
        {
            var counter = new NestingCounter();
            int outermostBegins = 0;
            int outermostEnds = 0;
            using var start = new ManualResetEventSlim();

            Task[] workers = [.. Enumerable.Range(0, 8).Select(_ => Task.Factory.StartNew(
                () =>
                {
                    start.Wait();

                    for (int i = 0; i < 20_000; i++)
                    {
                        // Each thread nests two sections, and the threads interleave in any order.
                        if (counter.Begin())
                        {
                            Interlocked.Increment(ref outermostBegins);
                        }

                        if (counter.Begin())
                        {
                            Interlocked.Increment(ref outermostBegins);
                        }

                        if (counter.End())
                        {
                            Interlocked.Increment(ref outermostEnds);
                        }

                        if (counter.End())
                        {
                            Interlocked.Increment(ref outermostEnds);
                        }
                    }
                },
                TaskCreationOptions.LongRunning))];

            start.Set();
            await Task.WhenAll(workers);

            // The count went up from zero as often as it went down to zero, and it is back at zero: the next section is
            // the outermost one.
            outermostBegins.ShouldBeGreaterThan(0);
            outermostEnds.ShouldBe(outermostBegins);
            counter.IsActive.ShouldBeFalse();
            counter.Begin().ShouldBeTrue();
        }
    }
}
