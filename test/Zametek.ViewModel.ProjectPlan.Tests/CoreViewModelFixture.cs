using Microsoft.Extensions.Logging.Abstractions;
using ReactiveUI.Builder;
using System;
using System.Collections.Generic;
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

            coreViewModel.KillSubscriptions();
            coreViewModel.AutoCompile = false;
            return coreViewModel;
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
            public ProjectScenarioImportModel ImportProjectScenarioFile(string filename) =>
                throw new NotSupportedException();

            public Task<ProjectScenarioImportModel> ImportProjectScenarioFileAsync(string filename) =>
                throw new NotSupportedException();

            public ProjectScenarioImportModel ImportMicrosoftProjectFile(string filename) =>
                throw new NotSupportedException();

            public ProjectScenarioImportModel ImportProjectScenarioXlsxFile(string filename) =>
                throw new NotSupportedException();
        }

        private sealed class TestProjectScenarioFileExport
            : IProjectScenarioFileExport
        {
            public void ExportProjectScenarioFile(ProjectScenarioModel projectScenario, ResourceSeriesSetModel resourceSeriesSet, TrackingSeriesSetModel trackingSeriesSet, bool showDates, string filename) =>
                throw new NotSupportedException();

            public Task ExportProjectScenarioFileAsync(ProjectScenarioModel projectScenario, ResourceSeriesSetModel resourceSeriesSet, TrackingSeriesSetModel trackingSeriesSet, bool showDates, string filename) =>
                throw new NotSupportedException();

            public void ExportProjectScenarioXlsxFile(ProjectScenarioModel projectScenario, ResourceSeriesSetModel resourceSeriesSet, TrackingSeriesSetModel trackingSeriesSet, bool showDates, string filename) =>
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
