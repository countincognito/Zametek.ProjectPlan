using Shouldly;
using Xunit;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// The list of the jobs that the server runs now: a job is in it from its beginning to its end, and it says how far the job has got.
    /// </summary>
    public class RunningJobsTests
    {
        private const string c_Id = "4bf92f3577b34da6a3ce929d0e0e4736";

        private sealed class ManualTimeProvider
            : TimeProvider
        {
            private long m_Timestamp;

            public DateTimeOffset Now { get; set; } = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);

            public override long TimestampFrequency => 1000;

            public override long GetTimestamp() => Volatile.Read(ref m_Timestamp);

            public override DateTimeOffset GetUtcNow() => Now;

            public void Advance(TimeSpan time)
            {
                Interlocked.Add(ref m_Timestamp, (long)time.TotalMilliseconds);
            }
        }

        [Fact]
        public void Find_Given_AJobThatRuns_Then_ItsStatusIsRunningWithNoProgressYet()
        {
            var time = new ManualTimeProvider();
            var jobs = new RunningJobs(time);

            using RunningJobs.Registration job = jobs.Begin(c_Id);
            time.Advance(TimeSpan.FromSeconds(3));

            JobStatusResponse status = jobs.Find(c_Id).ShouldNotBeNull();
            status.Id.ShouldBe(c_Id);
            status.State.ShouldBe(JobState.Running);
            status.StartedAt.ShouldBe(time.Now);
            status.Elapsed.ShouldBe(TimeSpan.FromSeconds(3));
            status.Progress.ShouldBeNull();
            jobs.Count.ShouldBe(1);
        }

        [Fact]
        public void Find_Given_AJobThatReports_Then_ItsStatusHasTheLastReport()
        {
            var jobs = new RunningJobs(new ManualTimeProvider());
            using RunningJobs.Registration job = jobs.Begin(c_Id);

            job.Report(new JobProgress(100, 5000));
            job.Report(new JobProgress(2500, 5000));

            jobs.Find(c_Id).ShouldNotBeNull().Progress.ShouldBe(new JobProgressResponse(2500, 5000));
        }

        [Fact]
        public void Find_Given_AJobThatHasEnded_Then_ThereIsNoStatus()
        {
            var jobs = new RunningJobs(new ManualTimeProvider());
            RunningJobs.Registration job = jobs.Begin(c_Id);

            job.Dispose();

            jobs.Find(c_Id).ShouldBeNull();
            jobs.Count.ShouldBe(0);
        }

        [Fact]
        public void Find_Given_AnIdNoJobHas_Then_ThereIsNoStatus()
        {
            var jobs = new RunningJobs(new ManualTimeProvider());
            using RunningJobs.Registration job = jobs.Begin(c_Id);

            jobs.Find("00000000000000000000000000000001").ShouldBeNull();
        }

        [Fact]
        public void Begin_Given_AnIdThatAnotherJobHas_Then_TheFirstKeepsItAndTheSecondIsNotListed()
        {
            var jobs = new RunningJobs(new ManualTimeProvider());
            using RunningJobs.Registration first = jobs.Begin(c_Id);
            RunningJobs.Registration second = jobs.Begin(c_Id);

            first.Report(new JobProgress(1, 10));
            second.Report(new JobProgress(9, 10));

            jobs.Find(c_Id).ShouldNotBeNull().Progress.ShouldBe(new JobProgressResponse(1, 10));
            jobs.Count.ShouldBe(1);

            // The end of the one that was not listed does not take the other away.
            second.Dispose();
            jobs.Find(c_Id).ShouldNotBeNull();
        }

        [Fact]
        public void Dispose_Given_TheFirstJobOfAnIdThatEnds_Then_TheIdIsFreeForTheNext()
        {
            var jobs = new RunningJobs(new ManualTimeProvider());
            RunningJobs.Registration first = jobs.Begin(c_Id);
            first.Dispose();

            using RunningJobs.Registration next = jobs.Begin(c_Id);
            next.Report(new JobProgress(3, 4));
            first.Dispose();

            jobs.Find(c_Id).ShouldNotBeNull().Progress.ShouldBe(new JobProgressResponse(3, 4));
        }

        [Fact]
        public async Task Report_Given_ManyThreads_Then_TheStatusIsAlwaysOneWholeReport()
        {
            var jobs = new RunningJobs(new ManualTimeProvider());
            using RunningJobs.Registration job = jobs.Begin(c_Id);

            Task writers = Task.WhenAll(Enumerable.Range(1, 4).Select(i => Task.Run(() =>
            {
                for (int n = 1; n <= 20_000; n++)
                {
                    job.Report(new JobProgress(n, n + i));
                }
            })));

            // Done and Total are always from the same report, because the report is one object.
            int reads = 0;
            while (!writers.IsCompleted)
            {
                JobProgressResponse? progress = jobs.Find(c_Id)!.Progress;

                if (progress is not null)
                {
                    (progress.Total - progress.Done).ShouldBeInRange(1, 4);
                }

                reads++;
            }

            await writers;
            reads.ShouldBeGreaterThan(0);
        }
    }
}
