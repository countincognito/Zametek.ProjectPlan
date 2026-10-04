using Shouldly;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for how long zpp --server waits before it tries a busy server again: what the server says, or zpp's own wait
    /// when it says nothing; never less than a second; more each time, up to 30 seconds or what the server says if that is
    /// more; and a little over, at random.
    /// </summary>
    public class JobClientBackOffTests
    {
        [Theory]
        [InlineData(null, 0, 5_000)]
        [InlineData(null, 1, 10_000)]
        [InlineData(null, 2, 20_000)]
        [InlineData(null, 3, 30_000)]
        [InlineData(null, 10, 30_000)]
        [InlineData(null, 50, 30_000)]
        [InlineData(1_000, 0, 1_000)]
        [InlineData(1_000, 1, 2_000)]
        [InlineData(1_000, 2, 4_000)]
        [InlineData(1_000, 4, 16_000)]
        [InlineData(1_000, 5, 30_000)]
        [InlineData(2_500, 3, 20_000)]
        [InlineData(10_000, 0, 10_000)]
        [InlineData(10_000, 1, 20_000)]
        [InlineData(10_000, 2, 30_000)]
        public void GetBackOff_Given_NoJitter_Then_WhatTheServerSaysDoubledEachTryUpToThirtySeconds(int? saidMilliseconds, int tries, int expectedMilliseconds)
        {
            TimeSpan? said = saidMilliseconds is int milliseconds ? TimeSpan.FromMilliseconds(milliseconds) : null;

            JobClient.GetBackOff(said, tries, 0).TotalMilliseconds.ShouldBe(expectedMilliseconds);
        }

        [Theory]
        [InlineData(60_000, 0)]
        [InlineData(60_000, 1)]
        [InlineData(60_000, 10)]
        [InlineData(3_600_000, 3)]
        public void GetBackOff_Given_AServerThatSaysMoreThanThirtySeconds_Then_NeverLessAndNeverMoreThanItSays(double saidMilliseconds, int tries)
        {
            JobClient.GetBackOff(TimeSpan.FromMilliseconds(saidMilliseconds), tries, 0).TotalMilliseconds.ShouldBe(saidMilliseconds);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(500)]
        [InlineData(999)]
        public void GetBackOff_Given_AServerThatSaysToComeBackAtOnce_Then_AtLeastASecond(double saidMilliseconds)
        {
            JobClient.GetBackOff(TimeSpan.FromMilliseconds(saidMilliseconds), 0, 0).ShouldBe(TimeSpan.FromSeconds(1));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-60_000)]
        public void GetBackOff_Given_ATimeThatHasAlreadyPassed_Then_ZppsOwnWait(double saidMilliseconds)
        {
            // A Retry-After that is a date in the past - because the clocks differ - says nothing that can be waited for.
            JobClient.GetBackOff(TimeSpan.FromMilliseconds(saidMilliseconds), 0, 0).ShouldBe(TimeSpan.FromSeconds(5));
        }

        [Theory]
        [InlineData(0, 5_000)]
        [InlineData(0.5, 5_625)]
        [InlineData(1, 6_250)]
        public void GetBackOff_Given_Jitter_Then_UpToAQuarterMore(double jitter, double expectedMilliseconds)
        {
            JobClient.GetBackOff(null, 0, jitter).TotalMilliseconds.ShouldBe(expectedMilliseconds);
        }

        [Fact]
        public void GetBackOff_Given_JitterAtTheCeiling_Then_UpToAQuarterOverIt()
        {
            JobClient.GetBackOff(null, 20, 1).TotalMilliseconds.ShouldBe(37_500);
        }

        [Fact]
        public void GetBackOff_Given_AServerThatSaysAVeryLongTime_Then_DoesNotOverflow()
        {
            // As long as a Retry-After can say: a date in the year 9999.
            TimeSpan said = new DateTimeOffset(9999, 12, 31, 0, 0, 0, TimeSpan.Zero) - DateTimeOffset.UtcNow;

            JobClient.GetBackOff(said, 10, 1).ShouldBe(said + TimeSpan.FromTicks((long)(said.Ticks * 0.25)));
            JobClient.GetBackOff(said, 0, 0).ShouldBe(said);
        }
    }
}
