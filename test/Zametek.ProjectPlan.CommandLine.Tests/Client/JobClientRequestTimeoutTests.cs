using Shouldly;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for the time limit that zpp --server gives a request when --server-timeout does not: the time the limits of the
    /// server let a job wait and run - the jobs in front of it in the queue, as many at a time as the server runs, and then
    /// the job itself, each for the time of a job and of one compilation, which a job that is out of time ends first - and
    /// a margin for the upload and the download, of a tenth and never less than 30 seconds.
    /// </summary>
    public class JobClientRequestTimeoutTests
    {
        private static LimitsResponse Limits(
            int maxJobs,
            int maxQueue,
            int jobTimeoutSeconds,
            int maxCompileTimeoutSeconds)
        {
            return new LimitsResponse(maxJobs, maxQueue, 50, 5000, 5000, 100_000, TimeSpan.FromSeconds(jobTimeoutSeconds), TimeSpan.FromSeconds(maxCompileTimeoutSeconds));
        }

        [Theory]
        // The limits of a server with four processors: two rounds of the queue and the job, each of 180 seconds, and a tenth.
        [InlineData(4, 8, 120, 60, 594_000)]
        // No queue: the job alone. A tenth of it is less than 30 seconds.
        [InlineData(1, 0, 120, 60, 210_000)]
        // Five in the queue, two at a time, is three rounds before the job.
        [InlineData(2, 5, 60, 60, 528_000)]
        [InlineData(1, 2, 600, 60, 2_178_000)]
        [InlineData(3, 3, 300, 5, 671_000)]
        [InlineData(8, 16, 1, 1, 36_000)]
        // An answer that gives no time for a compilation.
        [InlineData(4, 8, 120, 0, 396_000)]
        // A server that gives a job no time: the margin alone.
        [InlineData(1, 0, 0, 0, 30_000)]
        public void GetRequestTimeout_Given_TheLimitsOfAServer_Then_TheTimeOfTheQueueAndTheJobAndAMargin(int maxJobs, int maxQueue, int jobTimeoutSeconds, int maxCompileTimeoutSeconds, long expectedMilliseconds)
        {
            JobClient.GetRequestTimeout(Limits(maxJobs, maxQueue, jobTimeoutSeconds, maxCompileTimeoutSeconds))
                .ShouldBe(TimeSpan.FromMilliseconds(expectedMilliseconds));
        }

        [Theory]
        [InlineData(0, 8, 120, 60)]
        [InlineData(-1, 8, 120, 60)]
        [InlineData(4, -1, 120, 60)]
        [InlineData(4, 8, -1, 60)]
        [InlineData(4, 8, 120, -1)]
        public void GetRequestTimeout_Given_LimitsThatNoServerHas_Then_NoTimeLimit(int maxJobs, int maxQueue, int jobTimeoutSeconds, int maxCompileTimeoutSeconds)
        {
            JobClient.GetRequestTimeout(Limits(maxJobs, maxQueue, jobTimeoutSeconds, maxCompileTimeoutSeconds)).ShouldBeNull();
        }

        [Fact]
        public void GetRequestTimeout_Given_LimitsThatGiveMoreTimeThanATimerTakes_Then_TheLongestTimeATimerTakes()
        {
            // A day for a job, and a million jobs in the queue.
            JobClient.GetRequestTimeout(Limits(1, 1_000_000, 86_400, 60))
                .ShouldBe(TimeSpan.FromMilliseconds(uint.MaxValue - 1));
        }
    }
}
