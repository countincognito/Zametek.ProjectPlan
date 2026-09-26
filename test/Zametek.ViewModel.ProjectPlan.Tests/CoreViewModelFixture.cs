using Microsoft.Extensions.Logging.Abstractions;
using ReactiveUI.Builder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Builds a CoreViewModel with no user interface attached, in the same way the
    /// command line tool does: the real services throughout, and inert stand-ins only
    /// for the file and user interface seams a test never reaches.
    /// </summary>
    /// <remarks>
    /// The reactive subscriptions are killed immediately, so a test drives the core
    /// explicitly (RunCompile, the settings setters) rather than waiting on the
    /// pipeline. That keeps these tests about what the core does with the plan, not
    /// about when the pipeline decides to do it.
    /// </remarks>
    public static class CoreViewModelFixture
    {
        private static int s_ReactiveUIInitialized;

        /// <summary>
        /// Initialises the core (non user interface) ReactiveUI services, which the view
        /// models need in order to be constructed at all. The desktop application gets
        /// this from Avalonia and the command line tool does it directly; a test host has
        /// neither, so it is done here. Process-global, so it must happen exactly once
        /// however many fixtures a run builds.
        /// </summary>
        private static void EnsureReactiveUIInitialized()
        {
            if (Interlocked.Exchange(ref s_ReactiveUIInitialized, 1) == 0)
            {
                RxAppBuilder.CreateReactiveUIBuilder()
                    .WithCoreServices()
                    .BuildApp();
            }
        }

        public static CoreViewModel Create(
            int compilationTimeoutMilliseconds = AppSettingsModel.DefaultCompilationTimeoutMilliseconds)
        {
            (CoreViewModel coreViewModel, _) = Build(compilationTimeoutMilliseconds);

            coreViewModel.KillSubscriptions();
            coreViewModel.AutoCompile = false;
            return coreViewModel;
        }

        /// <summary>
        /// Builds the core and the settings managers that sit on top of it with their
        /// reactive subscriptions <em>intact</em>, for the tests that are about when the
        /// pipeline delivers rather than what the core computes.
        /// </summary>
        /// <remarks>
        /// The opposite of <see cref="Create"/>, and deliberately so. Killing the
        /// subscriptions makes a test independent of the scheduler, which is what nearly
        /// every test here wants; but a whole class of defect lives in the deferral
        /// itself, and none of it is reachable once the subscriptions are gone. Pair this
        /// with a <c>MainThreadSequencerScope</c> so the deferred deliveries queue up and
        /// the test says when they run.
        /// <para>
        /// Auto compilation is left off so a test is not racing background compiles it
        /// did not ask for; a scenario load still compiles, because
        /// <c>ProcessProjectScenario</c> runs one explicitly.
        /// </para>
        /// </remarks>
        public static SubscribedHarness CreateWithSubscriptions(
            int compilationTimeoutMilliseconds = AppSettingsModel.DefaultCompilationTimeoutMilliseconds)
        {
            (CoreViewModel coreViewModel, ISettingService settingService) = Build(compilationTimeoutMilliseconds);

            coreViewModel.AutoCompile = false;

            var dialogService = new TestDialogService();

            var resourceSettingsManagerViewModel = new ResourceSettingsManagerViewModel(
                coreViewModel,
                settingService,
                dialogService);

            var workStreamSettingsManagerViewModel = new WorkStreamSettingsManagerViewModel(
                coreViewModel,
                resourceSettingsManagerViewModel,
                settingService,
                dialogService);

            return new SubscribedHarness(
                coreViewModel,
                settingService,
                resourceSettingsManagerViewModel,
                workStreamSettingsManagerViewModel);
        }

        private static (CoreViewModel CoreViewModel, ISettingService SettingService) Build(
            int compilationTimeoutMilliseconds)
        {
            EnsureReactiveUIInitialized();

            var mapper = new ProjectPlanMapper();
            var settingService = new TestSettingService
            {
                CompilationTimeoutMilliseconds = compilationTimeoutMilliseconds,
            };
            var dateTimeCalculator = new DateTimeCalculator(TimeProvider.System);

            var coreViewModel = new CoreViewModel(
                new TestProjectScenarioFileImport(),
                new TestProjectScenarioFileExport(),
                settingService,
                dateTimeCalculator,
                mapper,
                new GraphCompilationService(mapper, settingService),
                new ResourceSchedulingService(mapper),
                new MetricCalculationService(mapper, dateTimeCalculator),
                new TestDataGridScrollManager(),
                NullLogger<CoreViewModel>.Instance);

            return (coreViewModel, settingService);
        }

        /// <summary>
        /// The core plus the settings managers built over it, all with live
        /// subscriptions. Disposing it tears them down in the order the application
        /// would.
        /// </summary>
        public sealed class SubscribedHarness
            : IDisposable
        {
            public SubscribedHarness(
                CoreViewModel coreViewModel,
                ISettingService settingService,
                ResourceSettingsManagerViewModel resourceSettingsManagerViewModel,
                WorkStreamSettingsManagerViewModel workStreamSettingsManagerViewModel)
            {
                Core = coreViewModel;
                SettingService = settingService;
                ResourceSettingsManager = resourceSettingsManagerViewModel;
                WorkStreamSettingsManager = workStreamSettingsManagerViewModel;
            }

            public CoreViewModel Core { get; }

            public ISettingService SettingService { get; }

            public ResourceSettingsManagerViewModel ResourceSettingsManager { get; }

            public WorkStreamSettingsManagerViewModel WorkStreamSettingsManager { get; }

            public void Dispose()
            {
                WorkStreamSettingsManager.Dispose();
                ResourceSettingsManager.Dispose();
                Core.Dispose();
            }
        }

        /// <summary>
        /// The graph settings the application starts with, which every scenario needs:
        /// a plan whose settings carry no activity severities cannot have its risk
        /// metrics built at all.
        /// </summary>
        public static GraphSettingsModel DefaultGraphSettings => new TestSettingService().DefaultGraphSettings;

        /// <summary>
        /// The settings the application starts with, for a test that needs a setting
        /// service but no core.
        /// </summary>
        public static ISettingService CreateSettingService() => new TestSettingService();

        /// <summary>
        /// Reads the current scenario out of a project file in TestFiles, through the same
        /// reader the application uses. A real plan carries the shape a hand-built one does
        /// not - dozens of activities, a deep dependency chain, a mixture of activities that
        /// target specific resources and activities that take whatever is free - which is
        /// what makes a scheduling result worth comparing against another.
        /// </summary>
        public static async Task<ProjectScenarioModel> LoadProjectScenarioAsync(string testFileName)
        {
            var projectFileOpen = new ProjectFileOpen(new DateTimeCalculator(TimeProvider.System));
            ProjectModel projectModel;

            await using (FileStream stream = File.OpenRead(Path.Combine(@"TestFiles", testFileName)))
            {
                projectModel = await projectFileOpen.OpenProjectFileAsync(stream);
            }

            return projectModel.Files.Single(x => x.NodeId == projectModel.Current).Scenario;
        }

        /// <summary>
        /// A plan of chained activities, each targeting one of two resources in turn, so
        /// that a compilation has both a critical path to calculate and a schedule to
        /// resolve rather than being trivially satisfiable.
        /// </summary>
        public static ProjectScenarioModel CreateProjectScenario(int activityCount)
        {
            List<ResourceModel> resources =
                [.. Enumerable.Range(1, 2).Select(id => new ResourceModel
                {
                    Id = id,
                    Name = $@"Resource {id}",
                    DisplayOrder = id,
                    IsExplicitTarget = false,
                    ColorFormat = ColorHelper.Random(),
                })];

            List<DependentActivityModel> activities =
                [.. Enumerable.Range(1, activityCount).Select(id => new DependentActivityModel
                {
                    Activity = new ActivityModel
                    {
                        Id = id,
                        DisplayOrder = id,
                        Name = $@"Activity {id}",
                        Duration = 1 + (id % 5),
                        TargetResources = [1 + (id % 2)],
                        ColorFormat = ColorHelper.Random(),
                    },
                    // A chain, so the critical path has to be walked rather than every
                    // activity starting at once.
                    Dependencies = id > 1 ? [id - 1] : [],
                })];

            return new ProjectScenarioModel
            {
                ProjectStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                Today = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                DependentActivities = activities,
                ResourceSettings = new ResourceSettingsModel
                {
                    Resources = resources,
                    DefaultUnitCost = 1.0,
                    DefaultUnitBilling = 1.0,
                    AreDisabled = false,
                },
                WorkStreamSettings = new WorkStreamSettingsModel(),
                HolidaySettings = new HolidaySettingsModel(),
                GraphSettings = new TestSettingService().DefaultGraphSettings,
                DisplaySettings = new ProjectScenarioDisplaySettingsModel(),
            };
        }

        #region Stand-ins

        private sealed class TestSettingService
            : SettingServiceBase
        {
            private string m_ProjectDirectory = string.Empty;

            public TestSettingService()
                : base(string.Empty)
            {
            }

            public override string ProjectDirectory
            {
                get => m_ProjectDirectory;
                protected set => m_ProjectDirectory = value;
            }

            public override string DockLayout { get; set; } = string.Empty;

            public override IList<DataGridModel> GetDataGridLayout() => [];

            public override void SetDataGridLayout(IList<DataGridModel> models)
            {
            }

            public override bool DefaultShowDates { get; set; }

            public override bool DefaultUseClassicDates { get; set; }

            public override NonWorkingDayMode DefaultNonWorkingDayMode { get; set; }

            public override bool DefaultHideCost { get; set; }

            public override bool DefaultHideBilling { get; set; }

            public override string SelectedTheme { get; set; } = string.Empty;

            public override int CompilationTimeoutMilliseconds { get; set; }

            public override int MaxRecentProjectFilePaths => 0;

            public override IReadOnlyList<string> RecentProjectFilePaths => [];

            public override void RecordRecentProjectFilePath(string filename)
            {
            }

            public override void RemoveRecentProjectFilePath(string filename)
            {
            }

            public override void ClearRecentProjectFilePaths()
            {
            }
        }

        private sealed class TestProjectScenarioFileImport
            : IProjectScenarioFileImport
        {
            public ProjectScenarioImportModel ImportProjectScenarioFile(Stream stream, ProjectScenarioImportFormat format) =>
                throw new NotSupportedException();
        }

        private sealed class TestProjectScenarioFileExport
            : IProjectScenarioFileExport
        {
            public void ExportProjectScenarioFile(ProjectScenarioModel projectScenario, ResourceSeriesSetModel resourceSeriesSet, TrackingSeriesSetModel trackingSeriesSet, bool showDates, Stream stream, ProjectScenarioExportFormat format) =>
                throw new NotSupportedException();
        }

        /// <summary>
        /// Never reached: a test that provokes a dialog has gone wrong, and throwing
        /// says so rather than letting the run continue past it.
        /// </summary>
        private sealed class TestDialogService
            : IDialogService
        {
            public object Parent { set { } }

            public Task ShowNotificationAsync(string title, string header, string message) =>
                throw new NotSupportedException(message);

            public Task ShowErrorAsync(string title, string header, string message) =>
                throw new NotSupportedException(message);

            public Task ShowWarningAsync(string title, string header, string message) =>
                throw new NotSupportedException(message);

            public Task ShowInfoAsync(string title, string header, string message, bool showMainPageLink = false) =>
                throw new NotSupportedException(message);

            public Task ShowInfoAsync(string title, string header, string message, double height, double width, bool showMainPageLink = false) =>
                throw new NotSupportedException(message);

            public Task<bool> ShowContextAsync(string title, string header, string message, object context) =>
                throw new NotSupportedException(message);

            public Task<bool> ShowContextAsync(string title, string header, string message, object context, double height, double width) =>
                throw new NotSupportedException(message);

            public Task<bool> ShowConfirmationAsync(string title, string header, string message) =>
                throw new NotSupportedException(message);

            public Task<string?> ShowOpenFileDialogAsync(string initialDirectory, IList<IFileFilter> fileFilters) =>
                throw new NotSupportedException();

            public Task<string?> ShowSaveFileDialogAsync(string initialFilename, string initialDirectory, IList<IFileFilter> fileFilters) =>
                throw new NotSupportedException();
        }

        private sealed class TestDataGridScrollManager
            : IDataGridScrollManager
        {
            public object? GetScrollItem(string name) => null;

            public void SetScrollItem(string name, object? item)
            {
            }

            public void ClearScrollItems()
            {
            }

            public void Dispose()
            {
            }
        }

        #endregion
    }
}
