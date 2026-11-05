using Dock.Model.Core;
using Microsoft.Extensions.Logging.Abstractions;
using ReactiveUI;
using Shouldly;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// An import that fails keeps the project that is open. An import takes the place of the scenario on show, and so the
    /// other scenarios and their edits that are not saved are not part of it. Before this, each failure other than a workbook
    /// with problems reset the project: a file that could not be read, a change that failed part way, and a compilation that
    /// used all of its budget. An import that fails part way now goes back to the scenario that it replaced, as a load does.
    /// </summary>
    public class MainViewModelImportFailureTests
    {
        private const int c_FirstActivityCount = 4;
        private const int c_SecondActivityCount = 3;
        private const int c_ThirdActivityCount = 2;
        private const int c_ImportedActivityCount = 5;

        [Fact]
        public async Task ImportProjectScenarioFileAsync_Given_AnImportThatWorks_Then_ItTakesThePlaceOfTheScenarioOnShowOnly()
        {
            // This also passes on the code before the change. It shows that the setup is right.
            using Harness harness = await Harness.MakeAsync();

            await harness.ImportAsync();

            harness.Dialogs.Errors.ShouldBeEmpty();
            harness.ShouldKeepTheOtherScenarios();
            harness.Settings.ScenarioId.ShouldBe(harness.FirstId);
            harness.Node(harness.FirstId).IsLoaded.ShouldBeTrue();
            harness.Core.RawActivities.Count.ShouldBe(c_ImportedActivityCount);
            harness.Core.IsProjectScenarioUpdated.ShouldBeTrue();
            harness.Saved(harness.FirstId).DependentActivities.Count.ShouldBe(c_ImportedActivityCount);
            harness.ShouldBeIdle();
        }

        [Fact]
        public async Task ImportProjectScenarioFileAsync_Given_ACompilationThatOutlastsTheWatchdog_Then_TheImportStaysAndTheProjectStaysOpen()
        {
            using Harness harness = await Harness.MakeGatedAsync();
            var core = (GatedCoreViewModel)harness.Core;
            core.StallNextCompilation();

            await harness.ImportAsync();

            harness.Dialogs.Errors.ShouldHaveSingleItem().Title.ShouldBe(Resource.ProjectPlan.Titles.Title_Error);
            harness.ShouldKeepTheOtherScenarios();
            harness.Settings.ScenarioId.ShouldBe(harness.FirstId);
            harness.Settings.ScenarioTitle.ShouldBe(@"First");
            harness.Node(harness.FirstId).IsLoaded.ShouldBeTrue();
            core.RawActivities.Count.ShouldBe(c_ImportedActivityCount);
            core.IsProjectScenarioUpdated.ShouldBeTrue(@"an import is an edit");
            core.HasStaleOutputs.ShouldBeTrue(@"the import has no outputs");
            harness.Saved(harness.FirstId).DependentActivities.Count.ShouldBe(c_ImportedActivityCount);
            harness.ShouldBeIdle();
        }

        [Fact]
        public async Task ImportProjectScenarioFileAsync_Given_ACompilationAfterTheImportThatOutlastsTheWatchdog_Then_TheImportStaysAndTheProjectStaysOpen()
        {
            using Harness harness = await Harness.MakeGatedAsync();
            var core = (GatedCoreViewModel)harness.Core;
            core.AutoCompile = true;

            // The import compiles while it holds its bulk update. With automatic compilation on, the command compiles again
            // after the import, and that compilation is the one that stalls.
            void StallAfterTheImport(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(CoreViewModel.IsBulkUpdating)
                    && !core.IsBulkUpdating)
                {
                    core.StallNextCompilation();
                }
            }

            core.PropertyChanged += StallAfterTheImport;

            try
            {
                await harness.ImportAsync();
            }
            finally
            {
                core.PropertyChanged -= StallAfterTheImport;
            }

            harness.Dialogs.Errors.ShouldHaveSingleItem().Title.ShouldBe(Resource.ProjectPlan.Titles.Title_Error);
            harness.ShouldKeepTheOtherScenarios();
            harness.Settings.ScenarioId.ShouldBe(harness.FirstId);
            core.RawActivities.Count.ShouldBe(c_ImportedActivityCount);
            core.HasStaleOutputs.ShouldBeTrue();
            core.AutoCompile.ShouldBeFalse(@"the next edit must not walk back into the same hang");
            harness.ShouldBeIdle();
        }

        // The first change fails in the reset. The second fails after the import has put its activities in.
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public async Task ImportProjectScenarioFileAsync_Given_AChangeThatFails_Then_TheScenarioBeforeIsBackAndTheProjectStaysOpen(int failingChange)
        {
            using Harness harness = await Harness.MakeAsync();

            using (new RoutingModeFault(harness.Core, failingChange))
            {
                await harness.ImportAsync();
            }

            harness.Dialogs.Errors.ShouldHaveSingleItem().Message.ShouldBe(RoutingModeFault.Message);
            harness.ShouldKeepTheOtherScenarios();
            harness.Settings.ScenarioId.ShouldBe(harness.FirstId);
            harness.Settings.ScenarioTitle.ShouldBe(@"First");
            harness.Node(harness.FirstId).IsLoaded.ShouldBeTrue();
            harness.Core.RawActivities.Count.ShouldBe(c_FirstActivityCount);

            ProjectScenarioModel first = harness.Saved(harness.FirstId);
            first.DependentActivities.Count.ShouldBe(c_FirstActivityCount);
            first.DisplaySettings.ArrowGraphEdgeRoutingMode.ShouldBe(EdgeRoutingMode.Spline);
            harness.ShouldBeIdle();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ImportProjectScenarioFileAsync_Given_AFailure_Then_TheEditsAndTheMarkOfTheScenarioBeforeStay(bool hasEdits)
        {
            using Harness harness = await Harness.MakeAsync();
            int activityCount = c_FirstActivityCount;

            if (hasEdits)
            {
                harness.Core.AddManagedActivity(++activityCount);
                harness.Core.IsProjectScenarioUpdated = true;
            }

            using (new RoutingModeFault(harness.Core, 1))
            {
                await harness.ImportAsync();
            }

            harness.Dialogs.Errors.ShouldHaveSingleItem();
            harness.ShouldKeepTheOtherScenarios();
            harness.Core.RawActivities.Count.ShouldBe(activityCount);
            harness.Core.IsProjectScenarioUpdated.ShouldBe(hasEdits, @"the mark says whether the scenario on show has edits that are not saved");
            harness.Saved(harness.FirstId).DependentActivities.Count.ShouldBe(activityCount);
        }

        [Fact]
        public async Task ImportProjectScenarioFileAsync_Given_AChangeThatFailsAndAWayBackThatFails_Then_NoScenarioIsLoadedAndASaveKeepsTheEdits()
        {
            using Harness harness = await Harness.MakeAsync();
            harness.Core.AddManagedActivity(c_FirstActivityCount + 1);
            harness.Core.IsProjectScenarioUpdated = true;

            using (new RoutingModeFault(harness.Core, 1, 2))
            {
                await harness.ImportAsync();
            }

            harness.Dialogs.Errors.ShouldHaveSingleItem().Message.ShouldBe(RoutingModeFault.Message);
            harness.ShouldKeepTheOtherScenarios();
            harness.Settings.ScenarioId.ShouldNotBe(harness.FirstId);
            harness.Node(harness.FirstId).IsLoaded.ShouldBeFalse();
            harness.Node(harness.SecondId).IsLoaded.ShouldBeFalse();
            harness.Node(harness.ThirdId).IsLoaded.ShouldBeFalse();
            harness.Saved(harness.FirstId).DependentActivities.Count.ShouldBe(c_FirstActivityCount + 1, @"the edit is in the file of the scenario before the import");
            harness.ShouldBeIdle();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ImportProjectScenarioFileAsync_Given_AFileThatCannotBeRead_Then_NothingChangesAndTheProjectStaysOpen(bool isGone)
        {
            using Harness harness = await Harness.MakeAsync();

            if (isGone)
            {
                harness.Dialogs.OpenFilename = harness.MissingFile;
            }
            else
            {
                harness.Importer.Failure = new InvalidDataException(@"The file is damaged.");
            }

            await harness.ImportAsync();

            (_, _, string message) = harness.Dialogs.Errors.ShouldHaveSingleItem();

            if (!isGone)
            {
                message.ShouldBe(@"The file is damaged.");
            }

            harness.ShouldKeepTheOtherScenarios();
            harness.Settings.ScenarioId.ShouldBe(harness.FirstId);
            harness.Node(harness.FirstId).IsLoaded.ShouldBeTrue();
            harness.Core.RawActivities.Count.ShouldBe(c_FirstActivityCount);
            harness.Core.IsProjectScenarioUpdated.ShouldBeFalse();
            harness.ShouldBeIdle();
        }

        [Fact]
        public async Task ImportProjectScenarioFileAsync_Given_AFailureAndThenAnImport_Then_TheImportWorks()
        {
            using Harness harness = await Harness.MakeAsync();

            using (new RoutingModeFault(harness.Core, 1))
            {
                await harness.ImportAsync();
            }

            await harness.ImportAsync();

            harness.Dialogs.Errors.ShouldHaveSingleItem();
            harness.ShouldKeepTheOtherScenarios();
            harness.Settings.ScenarioId.ShouldBe(harness.FirstId);
            harness.Node(harness.FirstId).IsLoaded.ShouldBeTrue();
            harness.Core.RawActivities.Count.ShouldBe(c_ImportedActivityCount);
            harness.Saved(harness.FirstId).DependentActivities.Count.ShouldBe(c_ImportedActivityCount);
        }

        private static ProjectScenarioModel Scenario(int activityCount, EdgeRoutingMode routingMode) =>
            CoreViewModelFixture.CreateProjectScenario(activityCount) with
            {
                DisplaySettings = new ProjectScenarioDisplaySettingsModel { ArrowGraphEdgeRoutingMode = routingMode },
            };

        // The import has a mode of routing that the first scenario does not have. Thus, it changes the mode after the reset.
        private static ProjectScenarioImportModel Import()
        {
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(c_ImportedActivityCount);

            return new ProjectScenarioImportModel
            {
                ProjectStart = scenario.ProjectStart,
                Today = scenario.Today,
                DependentActivities = scenario.DependentActivities,
                ResourceSettings = scenario.ResourceSettings,
                DisplaySettings = new ProjectScenarioDisplaySettingsModel { ArrowGraphEdgeRoutingMode = EdgeRoutingMode.Rectilinear },
            };
        }

        // Gives the import above, whatever the file holds, or throws the failure that a test gives it.
        private sealed class ScriptedProjectScenarioFileImport
            : IProjectScenarioFileImport
        {
            public Exception? Failure { get; set; }

            public ProjectScenarioImportModel ImportProjectScenarioFile(Stream stream, ProjectScenarioImportFormat format) =>
                Failure is null ? Import() : throw Failure;
        }

        // A project of three scenarios, with the first on show. The second has an edit that is not saved: the edit was made
        // while the second was on show, and then the first was loaded again.
        private sealed class Harness
            : IDisposable
        {
            private readonly string m_Directory;

            private Harness(CoreViewModel core, ISettingService settings, ScriptedProjectScenarioFileImport importer)
            {
                Core = core;
                Settings = settings;
                Importer = importer;
                Manager = new ProjectScenarioManagerViewModel(
                    core,
                    settings,
                    Dialogs,
                    new DateTimeCalculator(TimeProvider.System),
                    CoreViewModelFixture.CreateUIDispatcher());
                Main = new MainViewModel(
                    DoNothingProxy.Of<IFactory>(),
                    DoNothingProxy.Of<IDockSerializer>(),
                    DoNothingProxy.Of<IDataGridLayoutManager>(),
                    Manager,
                    core,
                    DoNothingProxy.Of<IProjectFileOpen>(),
                    DoNothingProxy.Of<IProjectFileSave>(),
                    settings,
                    Dialogs,
                    DoNothingProxy.Of<IServiceProvider>(),
                    NullLogger<MainViewModel>.Instance);

                Guid rootId = Guid.NewGuid();
                DateTimeOffset now = DateTimeOffset.UtcNow;

                ProjectScenarioNodeModel NodeModel(Guid id, string name) =>
                    new()
                    {
                        Id = id,
                        ParentId = rootId,
                        NodeType = ProjectScenarioNodeType.File,
                        Name = name,
                        CreatedOn = now,
                        ModifiedOn = now,
                    };

                Manager.ProcessProject(new ProjectModel
                {
                    Id = Guid.NewGuid(),
                    Root = rootId,
                    Current = FirstId,
                    Nodes =
                    [
                        NodeModel(FirstId, @"First"),
                        NodeModel(SecondId, @"Second"),
                        NodeModel(ThirdId, @"Third"),
                    ],
                    Files =
                    [
                        new ProjectScenarioFileModel { NodeId = FirstId, Scenario = Scenario(c_FirstActivityCount, EdgeRoutingMode.Spline) },
                        new ProjectScenarioFileModel { NodeId = SecondId, Scenario = Scenario(c_SecondActivityCount, EdgeRoutingMode.Rectilinear) },
                        new ProjectScenarioFileModel { NodeId = ThirdId, Scenario = Scenario(c_ThirdActivityCount, EdgeRoutingMode.Spline) },
                    ],
                    Tags = [],
                });

                m_Directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"zpp-import-failure-{Guid.NewGuid():N}")).FullName;
                string filename = Path.Combine(m_Directory, @"plan.xlsx");
                File.WriteAllText(filename, @"The scripted import does not read this file.");
                Dialogs.OpenFilename = filename;
                MissingFile = Path.Combine(m_Directory, @"gone.xlsx");
            }

            public Guid FirstId { get; } = Guid.NewGuid();

            public Guid SecondId { get; } = Guid.NewGuid();

            public Guid ThirdId { get; } = Guid.NewGuid();

            public CoreViewModel Core { get; }

            public ISettingService Settings { get; }

            public ScriptedDialogService Dialogs { get; } = new();

            public ScriptedProjectScenarioFileImport Importer { get; }

            public ProjectScenarioManagerViewModel Manager { get; }

            public MainViewModel Main { get; }

            public string MissingFile { get; }

            public static async Task<Harness> MakeAsync()
            {
                var importer = new ScriptedProjectScenarioFileImport();
                (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService(importer);
                return await new Harness(core, settings, importer).EditTheSecondAsync();
            }

            public static async Task<Harness> MakeGatedAsync()
            {
                var importer = new ScriptedProjectScenarioFileImport();
                (GatedCoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateGatedWithSettingService(
                    compilationTimeoutMilliseconds: 1_000,
                    projectScenarioFileImport: importer);
                return await new Harness(core, settings, importer).EditTheSecondAsync();
            }

            public IManagedNodeViewModel Node(Guid id) => Manager.GetNode(id).ShouldNotBeNull();

            // What a save writes for this scenario.
            public ProjectScenarioModel Saved(Guid id) =>
                Manager.BuildProject().Files.Single(x => x.NodeId == id).Scenario;

            public async Task LoadAsync(Guid id) =>
                await ((ReactiveCommand<IManagedNodeViewModel, RxVoid>)Manager.LoadProjectScenarioFileCommand).Execute(Node(id));

            // The command reports a failure itself, and so this ends when the import has ended, whichever way it went.
            public async Task ImportAsync() => await Main.ImportProjectScenarioFileAsync();

            // A reset leaves one new scenario, and it drops the edit of the second scenario.
            public void ShouldKeepTheOtherScenarios()
            {
                Manager.GetNode(SecondId).ShouldNotBeNull(@"the project was reset");
                Manager.GetNode(ThirdId).ShouldNotBeNull(@"the project was reset");
                Saved(SecondId).DependentActivities.Count.ShouldBe(c_SecondActivityCount + 1, @"the edit that is not saved is gone");
                Saved(ThirdId).DependentActivities.Count.ShouldBe(c_ThirdActivityCount);
                Manager.IsProjectUpdated.ShouldBeTrue(@"the project must still say that it has changes");
            }

            public void ShouldBeIdle()
            {
                Core.IsBusy.ShouldBeFalse();
                Core.IsBulkUpdating.ShouldBeFalse();
                Manager.IsBusy.ShouldBeFalse();
            }

            public void Dispose()
            {
                Manager.Dispose();
                Core.Dispose();

                try
                {
                    Directory.Delete(m_Directory, recursive: true);
                }
                catch (IOException)
                {
                    // Best-effort cleanup only.
                }
            }

            // The edit stays in the file of the second scenario, and the project says that it has changes.
            private async Task<Harness> EditTheSecondAsync()
            {
                await LoadAsync(SecondId);
                Core.AddManagedActivity(c_SecondActivityCount + 1);
                Core.IsProjectScenarioUpdated = true;
                await LoadAsync(FirstId);
                return this;
            }
        }
    }
}
