using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The panel that runs a simulation of the schedule: what a run finds, what it leaves alone, and when its
    /// result stops belonging to the plan.
    /// </summary>
    /// <remarks>
    /// The core is never started, so a test drives it explicitly (a scenario load compiles, the setters say what
    /// changed). The sample plan of the application has no estimates, so a copy of it is given an optimistic
    /// duration of 0.75 and a pessimistic duration of 1.6 times its duration.
    /// </remarks>
    public class ScheduleSimulationManagerViewModelTests
    {
        private const int c_Iterations = 200;

        private static async Task<ProjectScenarioModel> LoadSampleWithEstimatesAsync()
        {
            ProjectScenarioModel scenario = await CoreViewModelFixture.LoadProjectScenarioAsync(@"sample_v0_6_1.zpp");

            return scenario with
            {
                DependentActivities =
                [
                    .. scenario.DependentActivities.Select(x => x with
                    {
                        Activity = x.Activity with
                        {
                            MostLikelyDuration = x.Activity.Duration,
                            OptimisticDuration = (int)Math.Round(x.Activity.Duration * 0.75, MidpointRounding.AwayFromZero),
                            PessimisticDuration = (int)Math.Round(x.Activity.Duration * 1.6, MidpointRounding.AwayFromZero),
                        },
                    }),
                ],
            };
        }

        // A panel over a core that holds the sample plan, compiled.
        private sealed class Harness
            : IDisposable
        {
            public CoreViewModel Core { get; }

            public RecordingDialogService Dialogs { get; }

            public ScheduleSimulationManagerViewModel Panel { get; }

            private Harness(CoreViewModel core, RecordingDialogService dialogs, ScheduleSimulationManagerViewModel panel)
            {
                Core = core;
                Dialogs = dialogs;
                Panel = panel;
            }

            public static async Task<Harness> MakeAsync(ProjectScenarioModel? scenario = null, IUIDispatcher? dispatcher = null)
            {
                CoreViewModel core = CoreViewModelFixture.Create();
                core.GraphSettings = CoreViewModelFixture.DefaultGraphSettings;
                core.ProcessProjectScenario(scenario ?? await LoadSampleWithEstimatesAsync(), Guid.NewGuid(), @"Test");
                core.RunCompile();

                var dialogs = new RecordingDialogService();
                var panel = new ScheduleSimulationManagerViewModel(
                    core,
                    CoreViewModelFixture.CreateSettingService(),
                    dialogs,
                    new DateTimeCalculator(TimeProvider.System),
                    new ScottPlotImageExporter(),
                    dispatcher ?? CoreViewModelFixture.CreateUIDispatcher(),
                    NullLogger<ScheduleSimulationManagerViewModel>.Instance);

                return new Harness(core, dialogs, panel);
            }

            public void Dispose()
            {
                Panel.Dispose();
                Core.Dispose();
            }
        }

        // A dialog service that remembers the errors it was asked to show, and answers a save dialog with a file.
        private sealed class RecordingDialogService
            : IDialogService
        {
            public List<string> Errors { get; } = [];

            public List<string> Infos { get; } = [];

            public string? SaveFilename { get; set; }

            public object Parent { set { } }

            public Task ShowNotificationAsync(string title, string header, string message) => Task.CompletedTask;

            public Task ShowErrorAsync(string title, string header, string message)
            {
                Errors.Add(message);
                return Task.CompletedTask;
            }

            public Task ShowWarningAsync(string title, string header, string message) => Task.CompletedTask;

            public Task ShowInfoAsync(string title, string header, string message, bool showMainPageLink = false)
            {
                Infos.Add(message);
                return Task.CompletedTask;
            }

            public Task ShowInfoAsync(string title, string header, string message, double height, double width, bool showMainPageLink = false) => Task.CompletedTask;

            public Task<bool> ShowContextAsync(string title, string header, string message, object context) => Task.FromResult(false);

            public Task<bool> ShowContextAsync(string title, string header, string message, object context, double height, double width) => Task.FromResult(false);

            public Task<bool> ShowConfirmationAsync(string title, string header, string message) => Task.FromResult(false);

            public Task<string?> ShowOpenFileDialogAsync(string initialDirectory, IList<IFileFilter> fileFilters) => Task.FromResult<string?>(null);

            public Task<string?> ShowSaveFileDialogAsync(string initialFilename, string initialDirectory, IList<IFileFilter> fileFilters) =>
                Task.FromResult(SaveFilename);
        }

        [Fact]
        public async Task RunSimulationAsync_Given_ACompiledPlan_Then_HasAResultThatTheTablesAndTheChartShow()
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            panel.Iterations = c_Iterations;
            panel.UseRandomSeed = false;
            panel.Seed = 7;

            await panel.RunSimulationAsync();

            harness.Dialogs.Errors.ShouldBeEmpty();
            panel.IsRunning.ShouldBeFalse();
            panel.HasResult.ShouldBeTrue();
            panel.Progress.ShouldBe(1.0);
            panel.Summary.ShouldNotBeNull();
            panel.Summary.IterationCount.ShouldBe(c_Iterations);
            panel.Summary.ValidIterationCount.ShouldBe(c_Iterations);
            panel.Summary.Mean.ShouldBeGreaterThan(0.0);
            panel.Statistics.ShouldNotBeEmpty();
            panel.Statistics.Select(x => x.Name).ShouldContain(Resource.ProjectPlan.Labels.Label_SimulationMean);
            panel.Paths.ShouldNotBeEmpty();
            panel.Activities.ShouldNotBeEmpty();
            panel.SimulationChartPlotModel.GetPlottables().ShouldNotBeEmpty();
            panel.StatusText.ShouldStartWith(Resource.ProjectPlan.Labels.Label_SimulationDone.Split('{')[0]);
        }

        // A dispatcher that holds what it is given until it is told to run it, as the thread of the UI does.
        private sealed class QueueingUIDispatcher
            : IUIDispatcher
        {
            private readonly Queue<Action> m_Actions = new();
            private readonly object m_Lock = new();

            public int Pending
            {
                get
                {
                    lock (m_Lock)
                    {
                        return m_Actions.Count;
                    }
                }
            }

            public Task InvokeAsync(Action action)
            {
                action();
                return Task.CompletedTask;
            }

            public void Defer(Action action)
            {
                lock (m_Lock)
                {
                    m_Actions.Enqueue(action);
                }
            }

            public void RunAll()
            {
                while (true)
                {
                    Action action;
                    lock (m_Lock)
                    {
                        if (m_Actions.Count == 0)
                        {
                            return;
                        }
                        action = m_Actions.Dequeue();
                    }
                    action();
                }
            }
        }

        [Fact]
        public async Task RunSimulationAsync_Given_AUIThread_Then_EverythingThatIsBoundIsChangedThereAndInOrder()
        {
            // The progress comes from the threads of the simulation, and a control can be changed only on the thread
            // of the UI. What a run changes for the UI is handed to that thread, and the last word stays the last.
            var dispatcher = new QueueingUIDispatcher();
            using Harness harness = await Harness.MakeAsync(dispatcher: dispatcher);
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            panel.Iterations = c_Iterations;
            panel.UseRandomSeed = false;
            panel.Seed = 6;
            string before = panel.StatusText;
            IReadOnlyList<SimulationStatisticModel> statisticsBefore = panel.Statistics;
            var changed = new List<string>();
            panel.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

            await panel.RunSimulationAsync();

            // The run is over, and none of what the UI shows has changed.
            panel.HasResult.ShouldBeTrue();
            dispatcher.Pending.ShouldBeGreaterThan(0);
            panel.StatusText.ShouldBe(before);
            panel.Statistics.ShouldBeSameAs(statisticsBefore);
            changed.ShouldNotContain(nameof(ScheduleSimulationManagerViewModel.StatusText));
            changed.ShouldNotContain(nameof(ScheduleSimulationManagerViewModel.Progress));
            changed.ShouldNotContain(nameof(ScheduleSimulationManagerViewModel.Statistics));

            dispatcher.RunAll();

            panel.Progress.ShouldBe(1.0);
            panel.StatusText.ShouldStartWith(Resource.ProjectPlan.Labels.Label_SimulationDone.Split('{')[0]);
            panel.Statistics.ShouldNotBeEmpty();
        }

        [Fact]
        public async Task RunSimulationAsync_Given_TheSameSeedTwice_Then_GivesTheSameResult()
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            panel.Iterations = c_Iterations;
            panel.UseRandomSeed = false;
            panel.Seed = 11;

            await panel.RunSimulationAsync();
            SimulationSummaryModel first = panel.Summary!;
            await panel.RunSimulationAsync();
            SimulationSummaryModel second = panel.Summary!;

            second.Mean.ShouldBe(first.Mean);
            second.StandardDeviation.ShouldBe(first.StandardDeviation);
            second.Percentiles.Select(x => x.FinishTime).ShouldBe(first.Percentiles.Select(x => x.FinishTime));
            second.Paths.Select(x => x.Label).ShouldBe(first.Paths.Select(x => x.Label));
        }

        [Fact]
        public async Task RunSimulationAsync_Given_ARandomSeed_Then_ShowsTheSeedItUsedAndThatSeedRepeatsTheResult()
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            panel.Iterations = c_Iterations;
            panel.UseRandomSeed = true;
            panel.Seed = 0;

            await panel.RunSimulationAsync();
            ulong seedOfFirstRun = panel.Seed;
            SimulationSummaryModel first = panel.Summary!;

            await panel.RunSimulationAsync();
            panel.Seed.ShouldNotBe(seedOfFirstRun);

            panel.UseRandomSeed = false;
            panel.Seed = seedOfFirstRun;
            await panel.RunSimulationAsync();

            panel.Summary!.Mean.ShouldBe(first.Mean);
            panel.Summary.Percentiles.Select(x => x.FinishTime).ShouldBe(first.Percentiles.Select(x => x.FinishTime));
        }

        [Fact]
        public async Task RunSimulationAsync_Given_ATarget_Then_TheStatisticsHaveTheChanceOfMeetingIt()
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            panel.Iterations = c_Iterations;
            panel.UseRandomSeed = false;
            panel.Seed = 3;
            panel.TargetFinishTime = 10_000;

            await panel.RunSimulationAsync();

            panel.Summary!.TargetProbability.ShouldBe(1.0);
            panel.Statistics.ShouldContain(x => x.Name.StartsWith(Resource.ProjectPlan.Labels.Label_SimulationTargetProbability));
        }

        [Fact]
        public async Task RunSimulationAsync_Given_TheModifiedBetaDistribution_Then_UsesItsShapeValue()
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            panel.Iterations = c_Iterations;
            panel.UseRandomSeed = false;
            panel.Seed = 5;

            panel.Distribution = DurationDistribution.BetaPert;
            await panel.RunSimulationAsync();
            double betaPert = panel.Summary!.StandardDeviation;

            panel.Distribution = DurationDistribution.ModifiedBetaPert;
            panel.IsModifiedBetaDistribution.ShouldBeTrue();
            panel.ModifiedBetaShape = 40.0;
            await panel.RunSimulationAsync();
            double tight = panel.Summary!.StandardDeviation;

            panel.ModifiedBetaShape = 1.0;
            await panel.RunSimulationAsync();
            double loose = panel.Summary!.StandardDeviation;

            // A larger shape value draws closer to the most likely duration.
            tight.ShouldBeLessThan(loose);
            betaPert.ShouldBeGreaterThan(0.0);
        }

        [Fact]
        public async Task RunSimulationAsync_Given_APlanThatCompilesAgain_Then_TheResultIsStaleUntilItRunsAgain()
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            panel.Iterations = c_Iterations;
            panel.UseRandomSeed = false;
            panel.Seed = 1;
            panel.IsResultStale.ShouldBeFalse();

            await panel.RunSimulationAsync();
            panel.IsResultStale.ShouldBeFalse();

            // The plan has changed and the outputs no longer match it.
            harness.Core.HasStaleOutputs = true;
            panel.IsResultStale.ShouldBeTrue();

            // The outputs match the plan again, and so the result is not stale.
            harness.Core.HasStaleOutputs = false;
            panel.IsResultStale.ShouldBeFalse();

            // A plan that is loaded builds its outputs again, and a new revision of them says that the result is
            // of the plan that was there before.
            harness.Core.ProcessProjectScenario(await LoadSampleWithEstimatesAsync(), Guid.NewGuid(), @"Test");
            panel.IsResultStale.ShouldBeTrue();

            await panel.RunSimulationAsync();
            panel.IsResultStale.ShouldBeFalse();
        }

        [Fact]
        public async Task RunSimulationAsync_Given_AnEditAndACompile_Then_TheNextRunUsesTheEditedPlan()
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            panel.Iterations = c_Iterations;
            panel.UseRandomSeed = false;
            panel.Seed = 9;

            await panel.RunSimulationAsync();
            double before = panel.Summary!.Mean;
            int plannedBefore = panel.Summary.PlannedFinishTime;

            // Lengthen the first activity a great deal, and compile.
            IManagedActivityViewModel first = harness.Core.RawActivities.First();
            harness.Core.SetActivityDuration(first.Id, first.MostLikelyDuration + 500);
            harness.Core.RunCompile();

            await panel.RunSimulationAsync();

            panel.Summary!.PlannedFinishTime.ShouldBeGreaterThan(plannedBefore);
            panel.Summary.Mean.ShouldBeGreaterThan(before);
        }

        [Fact]
        public async Task CancelSimulation_Given_ARunInProgress_Then_StopsItAndKeepsTheEarlierResult()
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            panel.Iterations = c_Iterations;
            panel.UseRandomSeed = false;
            panel.Seed = 2;
            await panel.RunSimulationAsync();
            SimulationSummaryModel? earlier = panel.Summary;

            // A run that would take minutes.
            panel.Iterations = GraphLimits.MaximumSimulationIterations;
            Task run = panel.RunSimulationAsync();
            panel.IsRunning.ShouldBeTrue();
            panel.CancelSimulation();
            await run;

            panel.IsRunning.ShouldBeFalse();
            panel.StatusText.ShouldBe(Resource.ProjectPlan.Labels.Label_SimulationCancelled);
            panel.Summary.ShouldBeSameAs(earlier);
            harness.Dialogs.Errors.ShouldBeEmpty();
        }

        [Fact]
        public async Task RunSimulationAsync_Given_ARunInProgress_Then_AnotherRunDoesNothing()
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            panel.Iterations = GraphLimits.MaximumSimulationIterations;

            Task first = panel.RunSimulationAsync();
            Task second = panel.RunSimulationAsync();
            await second;
            panel.IsRunning.ShouldBeTrue();

            panel.CancelSimulation();
            await first;
            panel.IsRunning.ShouldBeFalse();
            harness.Dialogs.Errors.ShouldBeEmpty();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(GraphLimits.MaximumSimulationIterations + 1)]
        public async Task RunSimulationAsync_Given_AnIterationCountOutOfRange_Then_ReportsItAndRunsNothing(int iterations)
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            panel.Iterations = iterations;

            await panel.RunSimulationAsync();

            harness.Dialogs.Errors.Count.ShouldBe(1);
            panel.HasResult.ShouldBeFalse();
            panel.IsRunning.ShouldBeFalse();
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        public async Task RunSimulationAsync_Given_AShapeValueThatIsNotAboveZero_Then_ReportsItAndRunsNothing(double shape)
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            panel.Iterations = c_Iterations;
            panel.Distribution = DurationDistribution.ModifiedBetaPert;
            panel.ModifiedBetaShape = shape;

            await panel.RunSimulationAsync();

            harness.Dialogs.Errors.Count.ShouldBe(1);
            panel.HasResult.ShouldBeFalse();
        }

        [Fact]
        public async Task RunSimulationAsync_Given_APlanWithCompilationErrors_Then_ReportsItAndTheCommandCannotRun()
        {
            // Two activities that depend on each other.
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(2) with
            {
                DependentActivities =
                [
                    new DependentActivityModel { Activity = new ActivityModel { Id = 1, DisplayOrder = 1, Name = "A", Duration = 3 }, Dependencies = [2] },
                    new DependentActivityModel { Activity = new ActivityModel { Id = 2, DisplayOrder = 2, Name = "B", Duration = 4 }, Dependencies = [1] },
                ],
            };
            using Harness harness = await Harness.MakeAsync(scenario);
            harness.Core.HasCompilationErrors.ShouldBeTrue();

            await harness.Panel.RunSimulationAsync();

            harness.Dialogs.Errors.ShouldContain(Resource.ProjectPlan.Messages.Message_SimulationCompilationErrors);
            harness.Panel.HasResult.ShouldBeFalse();
            harness.Panel.RunSimulationCommand.CanExecute(null).ShouldBeFalse();
        }

        [Fact]
        public async Task RunSimulationAsync_Given_APlanWithNoActivities_Then_ReportsIt()
        {
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(1) with { DependentActivities = [] };
            using Harness harness = await Harness.MakeAsync(scenario);

            await harness.Panel.RunSimulationAsync();

            harness.Dialogs.Errors.ShouldContain(Resource.ProjectPlan.Messages.Message_SimulationNoActivities);
            harness.Panel.HasResult.ShouldBeFalse();
            harness.Panel.IsRunning.ShouldBeFalse();
        }

        [Fact]
        public async Task RunSimulationCommand_Given_ACompiledPlan_Then_CanRunAndRunsTheSimulation()
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            panel.Iterations = c_Iterations;
            panel.CancelSimulationCommand.CanExecute(null).ShouldBeFalse();

            panel.RunSimulationCommand.CanExecute(null).ShouldBeTrue();
            panel.RunSimulationCommand.Execute(null);

            // The command runs the task and gives no handle to it, so the test waits for the result.
            DateTime deadline = DateTime.UtcNow.AddSeconds(60);
            while (!panel.HasResult && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20, CancellationToken.None);
            }

            panel.HasResult.ShouldBeTrue();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(57)]
        [InlineData(224)]
        public async Task TargetFinishDate_Given_ATargetInDays_Then_IsTheDateThoseDaysFinishOnAndTheOtherWayRound(int days)
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            var changed = new List<string>();
            panel.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

            panel.TargetFinishTime = days;
            DateTimeOffset? date = panel.TargetFinishDate;

            date.ShouldNotBeNull();
            changed.ShouldContain(nameof(ScheduleSimulationManagerViewModel.TargetFinishDate));

            // The date is the one that the statistics show for the same number of days.
            panel.TargetFinishTime = null;
            panel.TargetFinishDate.ShouldBeNull();
            panel.TargetFinishDate = date;
            panel.TargetFinishTime.ShouldBe(days);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(20)]
        [InlineData(100)]
        public async Task TargetFinishDate_Given_ThePlanSkipsTheWeekends_Then_TheDatesAndTheDaysStillMatch(int days)
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            harness.Core.DisplaySettingsViewModel.NonWorkingDayMode = NonWorkingDayMode.Weekends;

            panel.TargetFinishTime = days;
            DateTimeOffset? date = panel.TargetFinishDate;
            date.ShouldNotBeNull();
            panel.TargetFinishTime = null;
            panel.TargetFinishDate = date;

            // A day that finishes on a working day has its own date, and so it comes back as itself.
            panel.TargetFinishTime.ShouldBe(days);
        }

        [Fact]
        public async Task TargetFinishDate_Given_ADateBeforeTheStartOfThePlan_Then_IsTheFirstDay()
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;

            panel.TargetFinishDate = harness.Core.ProjectStart.AddYears(-3);

            panel.TargetFinishTime.ShouldBe(0);
        }

        [Fact]
        public async Task ShowDates_Given_TheDisplaySetting_Then_FollowsIt()
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            harness.Core.DisplaySettingsViewModel.ShowDates = false;
            panel.ShowDates.ShouldBeFalse();

            harness.Core.DisplaySettingsViewModel.ShowDates = true;

            panel.ShowDates.ShouldBeTrue();
        }

        [Fact]
        public async Task RunSimulationAsync_Given_DatesOnDisplay_Then_TheStatisticsShowTheDatesAsWell()
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            harness.Core.DisplaySettingsViewModel.ShowDates = true;
            panel.Iterations = c_Iterations;
            panel.UseRandomSeed = false;
            panel.Seed = 8;
            panel.TargetFinishDate = harness.Core.ProjectStart.AddDays(300);

            await panel.RunSimulationAsync();

            SimulationStatisticModel mean = panel.Statistics.Single(x => x.Name == Resource.ProjectPlan.Labels.Label_SimulationPlannedFinish);
            mean.Value.ShouldContain("(");
            panel.Summary!.TargetFinishTime.ShouldNotBeNull();
        }

        [Fact]
        public async Task ExportCsvFilesCommand_Given_AResult_Then_WritesFiveFilesNextToTheNameThatTheUserChose()
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            panel.Iterations = c_Iterations;
            panel.UseRandomSeed = false;
            panel.Seed = 4;
            panel.ExportCsvFilesCommand.CanExecute(null).ShouldBeFalse();
            await panel.RunSimulationAsync();
            panel.ExportCsvFilesCommand.CanExecute(null).ShouldBeTrue();

            string directory = Path.Combine(Path.GetTempPath(), $@"simulation-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                harness.Dialogs.SaveFilename = Path.Combine(directory, @"plan.csv");

                panel.ExportCsvFilesCommand.Execute(null);

                DateTime deadline = DateTime.UtcNow.AddSeconds(60);
                while (harness.Dialogs.Infos.Count == 0 && harness.Dialogs.Errors.Count == 0 && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(20, CancellationToken.None);
                }

                harness.Dialogs.Errors.ShouldBeEmpty();
                Directory.GetFiles(directory).Select(Path.GetFileName).Order().ShouldBe(
                [
                    "plan-activities.csv",
                    "plan-histogram.csv",
                    "plan-iterations.csv",
                    "plan-paths.csv",
                    "plan-statistics.csv",
                ]);
                File.ReadAllLines(Path.Combine(directory, @"plan-statistics.csv"))[0].ShouldContain("Statistic,Value");
                File.ReadAllLines(Path.Combine(directory, @"plan-iterations.csv")).Length.ShouldBe(c_Iterations + 1);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Theory]
        [InlineData(SimulationCsvFile.Statistics, "Statistic,Value")]
        [InlineData(SimulationCsvFile.Histogram, "FirstFinishTime,LastFinishTime,Count,Share,CumulativeShare")]
        [InlineData(SimulationCsvFile.Paths, "Rank,Path,HasResourceLinks,Iterations,Share")]
        [InlineData(SimulationCsvFile.Activities, "ActivityId,Name,CriticalIterations,CriticalityIndex")]
        [InlineData(SimulationCsvFile.Iterations, "Iteration,FinishTime,CriticalPaths")]
        public async Task WriteSimulationCsvAsync_Given_AResult_Then_WritesTheFileWithTheByteOrderMarkTheDialogGives(SimulationCsvFile file, string header)
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            panel.Iterations = c_Iterations;
            panel.UseRandomSeed = false;
            panel.Seed = 4;
            await panel.RunSimulationAsync();

            using var stream = new MemoryStream();
            await panel.WriteSimulationCsvAsync(stream, file);

            byte[] bytes = stream.ToArray();
            bytes.Take(3).ShouldBe([(byte)0xEF, (byte)0xBB, (byte)0xBF]);
            System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).Split('\n')[0].ShouldStartWith(header);
        }

        [Fact]
        public async Task WriteSimulationCsvAsync_Given_ARun_Then_TheSameBytesAsTheFilesOfTheExport()
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            panel.Iterations = c_Iterations;
            panel.UseRandomSeed = false;
            panel.Seed = 4;
            await panel.RunSimulationAsync();

            string directory = Path.Combine(Path.GetTempPath(), $@"simulation-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                harness.Dialogs.SaveFilename = Path.Combine(directory, @"plan.csv");
                panel.ExportCsvFilesCommand.Execute(null);

                DateTime deadline = DateTime.UtcNow.AddSeconds(60);
                while (harness.Dialogs.Infos.Count == 0 && harness.Dialogs.Errors.Count == 0 && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(20, CancellationToken.None);
                }

                foreach ((SimulationCsvFile file, string suffix) in new[]
                {
                    (SimulationCsvFile.Statistics, Resource.ProjectPlan.Suffixes.Suffix_SimulationStatistics),
                    (SimulationCsvFile.Histogram, Resource.ProjectPlan.Suffixes.Suffix_SimulationHistogram),
                    (SimulationCsvFile.Paths, Resource.ProjectPlan.Suffixes.Suffix_SimulationPaths),
                    (SimulationCsvFile.Activities, Resource.ProjectPlan.Suffixes.Suffix_SimulationActivities),
                    (SimulationCsvFile.Iterations, Resource.ProjectPlan.Suffixes.Suffix_SimulationIterations),
                })
                {
                    using var stream = new MemoryStream();
                    await panel.WriteSimulationCsvAsync(stream, file);

                    stream.ToArray().ShouldBe(File.ReadAllBytes(Path.Combine(directory, $@"plan{suffix}.csv")), file.ToString());
                }
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public async Task WriteSimulationCsvAsync_Given_NoResult_Then_Throws()
        {
            using Harness harness = await Harness.MakeAsync();
            using var stream = new MemoryStream();

            InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(() => harness.Panel.WriteSimulationCsvAsync(stream, SimulationCsvFile.Statistics));

            ex.Message.ShouldBe(Resource.ProjectPlan.Messages.Message_SimulationNoResult);
            stream.Length.ShouldBe(0);
        }

        [Theory]
        [InlineData(SimulationCsvFile.None)]
        [InlineData(SimulationCsvFile.Statistics | SimulationCsvFile.Paths)]
        [InlineData(SimulationCsvFile.All)]
        [InlineData((SimulationCsvFile)64)]
        public async Task WriteSimulationCsvAsync_Given_NotExactlyOneFile_Then_Throws(SimulationCsvFile file)
        {
            using Harness harness = await Harness.MakeAsync();
            harness.Panel.Iterations = c_Iterations;
            await harness.Panel.RunSimulationAsync();
            using var stream = new MemoryStream();

            await Should.ThrowAsync<ArgumentOutOfRangeException>(() => harness.Panel.WriteSimulationCsvAsync(stream, file));

            stream.Length.ShouldBe(0);
        }

        [Fact]
        public async Task GetFinishDate_Given_ADayCount_Then_TheDateTheDisplayShowsForIt()
        {
            using Harness harness = await Harness.MakeAsync();
            harness.Core.DisplaySettingsViewModel.ShowDates = true;
            ScheduleSimulationManagerViewModel panel = harness.Panel;

            // The date a target of that many days is shown as, and the days that a date gives back.
            DateOnly date = panel.GetFinishDate(40);
            panel.TargetFinishDate = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

            // Days that a calendar with weekends skipped gives the same date are the same target, so it is the first of them.
            int days = panel.TargetFinishTime.ShouldNotBeNull();
            days.ShouldBeLessThanOrEqualTo(40);
            panel.GetFinishDate(days).ShouldBe(date);
            panel.GetFinishDate(0).ShouldBeLessThanOrEqualTo(date);
        }

        [Fact]
        public async Task WriteSimulationChartImageAsync_Given_AResult_Then_WritesAnImage()
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            panel.Iterations = c_Iterations;
            panel.UseRandomSeed = false;
            await panel.RunSimulationAsync();

            using var stream = new MemoryStream();
            await panel.WriteSimulationChartImageAsync(stream, ChartImageFormat.Png, 800, 500);

            stream.Length.ShouldBeGreaterThan(1000);
            stream.ToArray()[1].ShouldBe((byte)'P');
        }

        [Fact]
        public async Task RunSimulationAsync_Given_ATokenThatIsAlreadyCancelled_Then_DoesNotRunAndSaysSo()
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            panel.Iterations = GraphLimits.MaximumSimulationIterations;
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            await panel.RunSimulationAsync(cancellation.Token);

            panel.IsRunning.ShouldBeFalse();
            panel.HasResult.ShouldBeFalse();
            panel.Summary.ShouldBeNull();
            panel.StatusText.ShouldBe(Resource.ProjectPlan.Labels.Label_SimulationCancelled);
            harness.Dialogs.Errors.ShouldBeEmpty();
        }

        [Fact]
        public async Task RunSimulationAsync_Given_ATokenThatIsCancelledWhileItRuns_Then_StopsBeforeItsLastIteration()
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            panel.Iterations = GraphLimits.MaximumSimulationIterations;
            using var cancellation = new CancellationTokenSource();

            Task run = panel.RunSimulationAsync(cancellation.Token);
            await cancellation.CancelAsync();
            await run;

            panel.IsRunning.ShouldBeFalse();
            panel.Summary.ShouldBeNull();
            panel.StatusText.ShouldBe(Resource.ProjectPlan.Labels.Label_SimulationCancelled);
        }

        [Fact]
        public async Task Dispose_Given_ARunInProgress_Then_CancelsIt()
        {
            using Harness harness = await Harness.MakeAsync();
            ScheduleSimulationManagerViewModel panel = harness.Panel;
            panel.Iterations = GraphLimits.MaximumSimulationIterations;
            Task run = panel.RunSimulationAsync();

            panel.KillSubscriptions();
            await run;

            panel.IsRunning.ShouldBeFalse();
            panel.StatusText.ShouldBe(Resource.ProjectPlan.Labels.Label_SimulationCancelled);
        }
    }
}
