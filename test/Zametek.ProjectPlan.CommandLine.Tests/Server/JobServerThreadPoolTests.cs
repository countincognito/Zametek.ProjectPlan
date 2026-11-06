using Shouldly;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// The thread pool of zpp serve keeps threads for the requests while the jobs simulate. The simulator runs its iterations in
    /// Parallel.For on the threads of the pool, and it keeps each thread that it takes until its end. The pool then gave a
    /// request a thread only now and then: a request for the status of a job waited up to 19 seconds while a simulation ran.
    /// </summary>
    [Collection(ProgramExitCodeTests.CollectionName)]
    public class JobServerThreadPoolTests
        : IClassFixture<EngineFixture>
    {
        private readonly EngineFixture m_Engine;

        public JobServerThreadPoolTests(EngineFixture engine)
        {
            m_Engine = engine;
        }

        // A thread for each processor, as the pool keeps without a server, and one for each thread that the simulations of
        // the jobs can take at one time: the processors / 1 + the processors / 2 + ..., and at least one each.
        private static int ThreadsFor(int maxJobs)
        {
            int processors = Environment.ProcessorCount;
            int threads = processors;

            for (int i = 1; i <= maxJobs; i++)
            {
                threads += Math.Max(1, processors / i);
            }

            return threads;
        }

        [Fact]
        public async Task Build_Given_TheLimitOfTheJobs_Then_ThePoolKeepsAThreadForEachProcessorAndForEachThreadOfTheSimulations()
        {
            await using RunningServer server = await RunningServer.StartAsync(
                m_Engine.JobRunner,
                new ServeSettings { Limits = new ServeLimits { MaxJobs = 3 } });

            ThreadPool.GetMinThreads(out int workers, out _);

            workers.ShouldBeGreaterThanOrEqualTo(ThreadsFor(3));
        }

        [Fact]
        public void KeepThreadsForRequests_Given_ALowerLimitLater_Then_DoesNotLowerTheMinimum()
        {
            // A second server in the same process, or a test, must not take threads away from the first.
            int first = JobServer.KeepThreadsForRequests(2);

            int second = JobServer.KeepThreadsForRequests(1);

            first.ShouldBeGreaterThanOrEqualTo(ThreadsFor(2));
            second.ShouldBeGreaterThanOrEqualTo(first);
        }
    }
}
