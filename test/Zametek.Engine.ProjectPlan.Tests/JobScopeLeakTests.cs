using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.Engine.ProjectPlan.Tests
{
    /// <summary>
    /// A finished job leaves nothing of itself behind. The view models are written for a desktop, so
    /// some of their work has to reach a UI thread; headless there is none, and none of what they
    /// handed to one would ever be run - it would sit in a queue, or on a parked thread, holding the
    /// job's project for the life of the process. That is what IUIDispatcher and IGraphDispatcher are
    /// for, and this is what says they work.
    /// </summary>
    public class JobScopeLeakTests
    {
        // Enough to tell a job that leaks from one that does not; each is a whole plan, compiled and
        // written out every way a job can write it.
        private const int c_Jobs = 3;

        // Generous, because this waits for work to finish rather than for a leak to appear: a job
        // that leaks still fails, it just takes this long to say so.
        private static readonly TimeSpan s_CollectionTimeout = TimeSpan.FromSeconds(30);

        public JobScopeLeakTests()
        {
            ProjectPlanEngine.Initialize();
        }

        [Fact]
        public async Task RunAsync_Given_FinishedJobs_Then_NothingTheyMadeIsStillAlive()
        {
            List<WeakReference> jobSettings = [];

            await using ServiceProvider services = new ServiceCollection()
                .AddProjectPlanEngine()
                // The job's own setting service stands in for everything else the job made: every
                // view model a job resolves holds it, so while any of them is still reachable, so is
                // this.
                .AddScoped<ISettingService>(_ =>
                {
                    SettingService settingService = new();
                    lock (jobSettings)
                    {
                        jobSettings.Add(new WeakReference(settingService));
                    }
                    return settingService;
                })
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            JobRunner runner = services.GetRequiredService<JobRunner>();

            byte[] input = await File.ReadAllBytesAsync(
                Path.Combine(AppContext.BaseDirectory, @"Assets", @"two-scenarios.zpp"));

            for (int job = 0; job < c_Jobs; job++)
            {
                var sink = new MemoryJobSink();
                using var stream = new MemoryStream(input, writable: false);

                // Every output, so that every view model a job can resolve is resolved.
                JobResult result = await runner.RunAsync(
                    new JobRequest
                    {
                        Input = stream,
                        SaveProject = true,
                        ExportFormat = ProjectScenarioExportFormat.Xlsx,
                        GanttChart = new ChartOutputRequest(ChartImageFormat.Png, 640, 480),
                        ArrowGraph = new GraphOutputRequest(GraphExportFormat.GraphML),
                        VertexGraph = new GraphOutputRequest(GraphExportFormat.GraphML),
                        ResourceChart = new ChartOutputRequest(ChartImageFormat.Png, 640, 480),
                        EarnedValueChart = new ChartOutputRequest(ChartImageFormat.Png, 640, 480),
                        ScenarioChart = new ChartOutputRequest(ChartImageFormat.Png, 640, 480),
                    },
                    sink);

                result.Status.ShouldBe(JobStatus.Succeeded);
            }

            WeakReference[] references;
            lock (jobSettings)
            {
                references = [.. jobSettings];
            }

            // One per job: without this the test would pass on a registration nothing ever used.
            references.Length.ShouldBe(c_Jobs);

            (await CollectedWithinAsync(references, s_CollectionTimeout)).ShouldBeTrue(
                @"a finished job is still holding what it made");
        }

        // Waits for the job's objects to go, rather than demanding they are gone the instant the job
        // ends: a graph refresh or a chart build can still be finishing on a pool thread as the job
        // returns, and while it is, it legitimately holds what it is working on.
        private static async Task<bool> CollectedWithinAsync(IReadOnlyList<WeakReference> references, TimeSpan timeout)
        {
            long deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;

            while (true)
            {
                await KeepThePoolBusyAsync();

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                if (references.All(x => !x.IsAlive))
                {
                    return true;
                }
                if (Environment.TickCount64 >= deadline)
                {
                    return false;
                }

                await Task.Delay(100);
            }
        }

        // A pool thread that has gone idle still holds on to some of what it last ran - measured, the
        // whole of a job's scope, until the thread runs something else - though nothing of ours asks
        // it to (no thread or async-local state here), and nothing the debugger's gcroot can name. A
        // host that keeps its threads busy loses it with the next work item, so it is no leak; a leak
        // is what outlasts that. So before each look every pool thread is given other work. What this
        // test is for survives it: work queued to a dispatcher that nothing pumps is held by the
        // dispatcher, and a thread parked waiting on one takes no other work.
        private static async Task KeepThePoolBusyAsync()
        {
            await Task.WhenAll(Enumerable
                .Range(0, Math.Max(Environment.ProcessorCount, ThreadPool.ThreadCount) * 16)
                .Select(_ => Task.Run(() => Thread.SpinWait(1000))));
        }
    }
}
