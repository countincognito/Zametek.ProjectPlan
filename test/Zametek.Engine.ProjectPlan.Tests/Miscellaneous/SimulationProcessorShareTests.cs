using Shouldly;
using Xunit;

namespace Zametek.Engine.ProjectPlan.Tests
{
    /// <summary>
    /// The share of the processors that a simulation takes when other simulations run at the same time.
    /// </summary>
    public class SimulationProcessorShareTests
    {
        [Fact]
        public void Begin_Given_ASimulationThatIsAlone_Then_TakesAllTheProcessors()
        {
            var share = new SimulationProcessorShare(8);

            using SimulationProcessorShare.Lease lease = share.Begin();

            lease.Threads.ShouldBe(8);
            share.Running.ShouldBe(1);
        }

        [Fact]
        public void Begin_Given_SimulationsThatBeginOneAfterTheOther_Then_EachTakesTheProcessorsDividedByTheNumberThatRunNow()
        {
            var share = new SimulationProcessorShare(8);

            using SimulationProcessorShare.Lease first = share.Begin();
            using SimulationProcessorShare.Lease second = share.Begin();
            using SimulationProcessorShare.Lease third = share.Begin();
            using SimulationProcessorShare.Lease fourth = share.Begin();

            // 8, 8 / 2, 8 / 3 and 8 / 4, the third rounded down.
            new[] { first.Threads, second.Threads, third.Threads, fourth.Threads }.ShouldBe([8, 4, 2, 2]);
            share.Running.ShouldBe(4);
        }

        [Fact]
        public void Begin_Given_MoreSimulationsThanProcessors_Then_EachHasAtLeastOneThread()
        {
            var share = new SimulationProcessorShare(2);
            var leases = new List<SimulationProcessorShare.Lease>();

            for (int i = 0; i < 10; i++)
            {
                leases.Add(share.Begin());
            }

            leases.Select(x => x.Threads).ShouldBe([2, 1, 1, 1, 1, 1, 1, 1, 1, 1]);
            leases.ForEach(x => x.Dispose());
        }

        [Fact]
        public void Dispose_Given_ASimulationThatEnds_Then_ItsPlaceIsFreeForTheNext()
        {
            var share = new SimulationProcessorShare(8);
            SimulationProcessorShare.Lease first = share.Begin();
            using SimulationProcessorShare.Lease second = share.Begin();

            first.Dispose();

            share.Running.ShouldBe(1);

            // The next one has what the one that is left leaves: with two running, half.
            using SimulationProcessorShare.Lease third = share.Begin();
            third.Threads.ShouldBe(4);
        }

        [Fact]
        public void Dispose_Given_TheSameLeaseTwice_Then_FreesItsPlaceOnce()
        {
            var share = new SimulationProcessorShare(8);
            using SimulationProcessorShare.Lease other = share.Begin();
            SimulationProcessorShare.Lease lease = share.Begin();

            lease.Dispose();
            lease.Dispose();

            share.Running.ShouldBe(1);
        }

        [Fact]
        public void Begin_Given_SimulationsThatBeginTogether_Then_TheCountIsExactAndEachShareIsInRange()
        {
            var share = new SimulationProcessorShare(16);
            const int count = 64;
            var leases = new SimulationProcessorShare.Lease[count];

            Parallel.For(0, count, i => leases[i] = share.Begin());

            share.Running.ShouldBe(count);
            leases.ShouldAllBe(x => x.Threads >= 1 && x.Threads <= 16);
            leases.Count(x => x.Threads == 16).ShouldBe(1); // The first, and only the first, has them all.

            foreach (SimulationProcessorShare.Lease lease in leases)
            {
                lease.Dispose();
            }

            share.Running.ShouldBe(0);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Ctor_Given_NoProcessor_Then_Throws(int processors)
        {
            Should.Throw<ArgumentOutOfRangeException>(() => new SimulationProcessorShare(processors));
        }

        [Fact]
        public void Ctor_Given_NothingElse_Then_ShareTheProcessorsOfTheMachine()
        {
            new SimulationProcessorShare().Processors.ShouldBe(Environment.ProcessorCount);
        }
    }
}
