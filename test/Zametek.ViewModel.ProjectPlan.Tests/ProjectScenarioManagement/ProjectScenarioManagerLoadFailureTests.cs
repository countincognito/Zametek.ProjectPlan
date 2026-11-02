using ReactiveUI;
using Shouldly;
using System;
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
    /// A load of a scenario that fails must change nothing. The application goes back to the scenario that it had, with the
    /// edits that it had, and a save writes every scenario as it was. Before this, the load left a part of a scenario in the
    /// core, and the next save wrote that over a whole one: 46 activities and 15 resources became none, with no mark on the
    /// project to say that something had changed.
    /// </summary>
    public class ProjectScenarioManagerLoadFailureTests
    {
        private const int c_FirstActivityCount = 4;
        private const int c_SecondActivityCount = 3;

        [Fact]
        public async Task Load_Given_AScenarioThatFails_Then_TheCoreHasTheScenarioBeforeAndTheErrorIsShown()
        {
            using Harness harness = Harness.Make();

            await harness.LoadAsync(harness.BrokenId);

            harness.Dialogs.Errors.ShouldHaveSingleItem();
            harness.Settings.ScenarioId.ShouldBe(harness.FirstId);
            harness.Settings.ScenarioTitle.ShouldBe(@"First");
            harness.Core.RawActivities.Count.ShouldBe(c_FirstActivityCount);
            harness.Node(harness.FirstId).IsLoaded.ShouldBeTrue();
            harness.Node(harness.BrokenId).IsLoaded.ShouldBeFalse();
            harness.Core.IsBusy.ShouldBeFalse();
            harness.Manager.IsBusy.ShouldBeFalse();
        }

        [Fact]
        public async Task Load_Given_AScenarioThatFails_Then_ASaveWritesEachScenarioAsItWas()
        {
            using Harness harness = Harness.Make();

            await harness.LoadAsync(harness.BrokenId);

            harness.ShouldSaveEachScenarioAsItWas();
            harness.Manager.BuildProject().Current.ShouldBe(harness.FirstId);
        }

        // The first change fails in the reset, when the id was still the id of the scenario that was open and a save
        // emptied that scenario. The second fails after the reset, when a save emptied the scenario being loaded.
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public async Task Load_Given_AChangeThatFails_Then_ASaveWritesEachScenarioAsItWas(int failingChange)
        {
            using Harness harness = Harness.Make();

            using (new RoutingModeFault(harness.Core, failingChange))
            {
                await harness.LoadAsync(harness.SecondId);
            }

            harness.Dialogs.Errors.ShouldHaveSingleItem().Message.ShouldBe(RoutingModeFault.Message);
            harness.Settings.ScenarioId.ShouldBe(harness.FirstId);
            harness.Core.RawActivities.Count.ShouldBe(c_FirstActivityCount);
            harness.Node(harness.FirstId).IsLoaded.ShouldBeTrue();
            harness.Node(harness.SecondId).IsLoaded.ShouldBeFalse();
            harness.ShouldSaveEachScenarioAsItWas();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public async Task Load_Given_AChangeThatFailsAndThenALoad_Then_TheLoadWorks(int failingChange)
        {
            using Harness harness = Harness.Make();

            using (new RoutingModeFault(harness.Core, failingChange))
            {
                await harness.LoadAsync(harness.SecondId);
            }

            await harness.LoadAsync(harness.SecondId);

            harness.Settings.ScenarioId.ShouldBe(harness.SecondId);
            harness.Core.RawActivities.Count.ShouldBe(c_SecondActivityCount);
            harness.Node(harness.SecondId).IsLoaded.ShouldBeTrue();
            harness.Node(harness.FirstId).IsLoaded.ShouldBeFalse();
            harness.ShouldSaveEachScenarioAsItWas();
        }

        [Fact]
        public async Task Load_Given_EditsThatAreNotSavedAndAFailure_Then_TheEditsAndTheMarkStay()
        {
            using Harness harness = Harness.Make();
            harness.Core.AddManagedActivity(c_FirstActivityCount + 1);
            harness.Core.IsProjectScenarioUpdated = true;

            await harness.LoadAsync(harness.BrokenId);

            harness.Core.RawActivities.Count.ShouldBe(c_FirstActivityCount + 1);
            harness.Core.IsProjectScenarioUpdated.ShouldBeTrue(@"without the mark, a close would drop the edits and ask nothing");
            harness.Saved(harness.FirstId).DependentActivities.Count.ShouldBe(c_FirstActivityCount + 1);
        }

        [Fact]
        public async Task Load_Given_NoEditsAndAFailure_Then_NothingIsMarked()
        {
            using Harness harness = Harness.Make();

            await harness.LoadAsync(harness.BrokenId);

            harness.Core.IsProjectScenarioUpdated.ShouldBeFalse();
            harness.Manager.IsProjectUpdated.ShouldBeFalse();
        }

        [Fact]
        public async Task Load_Given_AChangeThatFailsAndAWayBackThatFails_Then_NoScenarioIsLoadedAndASaveWritesEachAsItWas()
        {
            using Harness harness = Harness.Make();

            using (new RoutingModeFault(harness.Core, 1, 2))
            {
                await harness.LoadAsync(harness.SecondId);
            }

            harness.Dialogs.Errors.ShouldHaveSingleItem();
            harness.Settings.ScenarioId.ShouldNotBe(harness.FirstId);
            harness.Settings.ScenarioId.ShouldNotBe(harness.SecondId);
            harness.Node(harness.FirstId).IsLoaded.ShouldBeFalse();
            harness.Node(harness.SecondId).IsLoaded.ShouldBeFalse();
            harness.ShouldSaveEachScenarioAsItWas();
        }

        [Fact]
        public async Task Load_Given_AChangeThatFailsAndAWayBackThatFailsAndThenALoad_Then_TheLoadWorks()
        {
            using Harness harness = Harness.Make();

            using (new RoutingModeFault(harness.Core, 1, 2))
            {
                await harness.LoadAsync(harness.SecondId);
            }

            await harness.LoadAsync(harness.SecondId);

            harness.Settings.ScenarioId.ShouldBe(harness.SecondId);
            harness.Core.RawActivities.Count.ShouldBe(c_SecondActivityCount);
            harness.Node(harness.SecondId).IsLoaded.ShouldBeTrue();
            harness.ShouldSaveEachScenarioAsItWas();
        }

        [Fact]
        public async Task Load_Given_ACompilationThatOutlastsTheWatchdog_Then_TheScenarioIsLoadedAndTheErrorIsShown()
        {
            using Harness harness = Harness.MakeGated();
            var core = (GatedCoreViewModel)harness.Core;
            core.StallNextCompilation();

            await harness.LoadAsync(harness.SecondId);

            harness.Dialogs.Errors.ShouldHaveSingleItem();
            harness.Settings.ScenarioId.ShouldBe(harness.SecondId);
            harness.Node(harness.SecondId).IsLoaded.ShouldBeTrue();
            harness.Node(harness.FirstId).IsLoaded.ShouldBeFalse();
            harness.Core.RawActivities.Count.ShouldBe(c_SecondActivityCount);
            harness.Core.HasStaleOutputs.ShouldBeTrue();
            harness.Manager.IsProjectUpdated.ShouldBeTrue();
            harness.ShouldSaveEachScenarioAsItWas();
        }

        [Fact]
        public async Task Load_Given_AFailureAndACompilationThatOutlastsTheWatchdogOnTheWayBack_Then_TheScenarioBeforeIsBack()
        {
            using Harness harness = Harness.MakeGated();
            var core = (GatedCoreViewModel)harness.Core;
            core.AddManagedActivity(c_FirstActivityCount + 1);
            core.IsProjectScenarioUpdated = true;

            // The load fails before it compiles, and so the compilation that this stalls is the one on the way back.
            core.StallNextCompilation();

            using (new RoutingModeFault(core, 1))
            {
                await harness.LoadAsync(harness.SecondId);
            }

            harness.Dialogs.Errors.ShouldHaveSingleItem().Message.ShouldBe(RoutingModeFault.Message);
            harness.Settings.ScenarioId.ShouldBe(harness.FirstId);
            harness.Node(harness.FirstId).IsLoaded.ShouldBeTrue();
            core.RawActivities.Count.ShouldBe(c_FirstActivityCount + 1);
            core.IsProjectScenarioUpdated.ShouldBeTrue(@"the edits are not saved yet");
        }

        [Fact]
        public async Task Load_Given_AScenarioThatFails_Then_TheTitleAndTheTrackedMetricsAreRevised()
        {
            using Harness harness = Harness.Make();
            harness.Manager.BuildTrackedMetrics();
            int titleRevision = harness.Manager.TitleRevision;

            await harness.LoadAsync(harness.BrokenId);

            harness.Manager.TitleRevision.ShouldNotBe(titleRevision);
            harness.Manager.IsReadyToReviseTrackedMetrics.ShouldBe(ReadyToRevise.Yes);
        }

        [Fact]
        public async Task Paste_Given_TheCutOfTheScenarioThatIsLoadedAndALoadThatFails_Then_TheScenarioStaysLoadedAndWhole()
        {
            using Harness harness = Harness.Make();
            harness.Manager.SelectedNodes.Add(harness.Node(harness.FirstId));
            await harness.ExecuteAsync(harness.Manager.CutProjectScenarioNodeCommand);

            using (new RoutingModeFault(harness.Core, 1))
            {
                await harness.ExecuteAsync(harness.Manager.PasteProjectScenarioNodeCommand);
            }

            harness.Dialogs.Errors.ShouldHaveSingleItem().Message.ShouldBe(RoutingModeFault.Message);
            harness.Settings.ScenarioId.ShouldBe(harness.FirstId);
            harness.Node(harness.FirstId).IsLoaded.ShouldBeTrue();
            harness.Core.RawActivities.Count.ShouldBe(c_FirstActivityCount);
            harness.ShouldSaveEachScenarioAsItWas();
        }

        [Fact]
        public async Task Remove_Given_TheScenarioThatIsLoadedAndALoadThatFails_Then_ASaveWritesTheOthersAsTheyWere()
        {
            using Harness harness = Harness.Make();
            harness.Manager.SelectedNodes.Add(harness.Node(harness.FirstId));

            // The most recent scenario that remains is the one that a load cannot finish.
            await harness.ExecuteAsync(harness.Manager.RemoveProjectScenarioNodeCommand);

            harness.Dialogs.Errors.ShouldHaveSingleItem();
            harness.Manager.GetNode(harness.FirstId).ShouldBeNull();
            harness.Settings.ScenarioId.ShouldNotBe(harness.BrokenId);
            harness.Saved(harness.SecondId).DependentActivities.Count.ShouldBe(c_SecondActivityCount);
            harness.Saved(harness.BrokenId).ShouldBeSameAs(harness.Broken);
        }

        // A project of three scenarios, with the first open.
        private sealed class Harness
            : IDisposable
        {
            private readonly ProjectScenarioModel m_First = CoreViewModelFixture.CreateProjectScenario(c_FirstActivityCount) with
            {
                DisplaySettings = new ProjectScenarioDisplaySettingsModel { ArrowGraphEdgeRoutingMode = EdgeRoutingMode.Spline },
            };

            private readonly ProjectScenarioModel m_Second = CoreViewModelFixture.CreateProjectScenario(c_SecondActivityCount) with
            {
                DisplaySettings = new ProjectScenarioDisplaySettingsModel { ArrowGraphEdgeRoutingMode = EdgeRoutingMode.Rectilinear },
            };

            private Harness(CoreViewModel core, ISettingService settings)
            {
                Core = core;
                Settings = settings;
                Manager = new ProjectScenarioManagerViewModel(
                    core,
                    settings,
                    Dialogs,
                    new DateTimeCalculator(TimeProvider.System),
                    CoreViewModelFixture.CreateUIDispatcher());

                Guid rootId = Guid.NewGuid();
                DateTimeOffset now = DateTimeOffset.UtcNow;

                ProjectScenarioNodeModel Node(Guid id, string name, DateTimeOffset modifiedOn) =>
                    new()
                    {
                        Id = id,
                        ParentId = rootId,
                        NodeType = ProjectScenarioNodeType.File,
                        Name = name,
                        CreatedOn = now,
                        ModifiedOn = modifiedOn,
                    };

                Manager.ProcessProject(new ProjectModel
                {
                    Id = Guid.NewGuid(),
                    Root = rootId,
                    Current = FirstId,
                    Nodes =
                    [
                        Node(FirstId, @"First", now),
                        Node(SecondId, @"Second", now),
                        Node(BrokenId, @"Broken", now.AddDays(1)),
                    ],
                    Files =
                    [
                        new ProjectScenarioFileModel { NodeId = FirstId, Scenario = m_First },
                        new ProjectScenarioFileModel { NodeId = SecondId, Scenario = m_Second },
                        new ProjectScenarioFileModel { NodeId = BrokenId, Scenario = Broken },
                    ],
                    Tags = [],
                });
            }

            public Guid FirstId { get; } = Guid.NewGuid();

            public Guid SecondId { get; } = Guid.NewGuid();

            public Guid BrokenId { get; } = Guid.NewGuid();

            // A load fails on this at the activities, when the settings of the scenario are in place.
            public ProjectScenarioModel Broken { get; } = CoreViewModelFixture.CreateProjectScenario(2) with { DependentActivities = null! };

            public CoreViewModel Core { get; }

            public ISettingService Settings { get; }

            public ScriptedDialogService Dialogs { get; } = new();

            public ProjectScenarioManagerViewModel Manager { get; }

            public static Harness Make()
            {
                (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
                return new Harness(core, settings);
            }

            public static Harness MakeGated()
            {
                (GatedCoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateGatedWithSettingService(compilationTimeoutMilliseconds: 1_000);
                return new Harness(core, settings);
            }

            public IManagedNodeViewModel Node(Guid id) => Manager.GetNode(id).ShouldNotBeNull();

            // What a save writes for this scenario.
            public ProjectScenarioModel Saved(Guid id) =>
                Manager.BuildProject().Files.Single(x => x.NodeId == id).Scenario;

            public void ShouldSaveEachScenarioAsItWas()
            {
                ProjectScenarioModel first = Saved(FirstId);
                first.DependentActivities.Count.ShouldBe(c_FirstActivityCount);
                first.ResourceSettings.Resources.Count.ShouldBe(2);
                first.DisplaySettings.ArrowGraphEdgeRoutingMode.ShouldBe(EdgeRoutingMode.Spline);

                ProjectScenarioModel second = Saved(SecondId);
                second.DependentActivities.Count.ShouldBe(c_SecondActivityCount);
                second.ResourceSettings.Resources.Count.ShouldBe(2);
                second.DisplaySettings.ArrowGraphEdgeRoutingMode.ShouldBe(EdgeRoutingMode.Rectilinear);

                Saved(BrokenId).ShouldBeSameAs(Broken);
            }

            // The command reports a failure itself, so this ends when the load has ended, whichever way it went.
            public async Task LoadAsync(Guid id) =>
                await ((ReactiveCommand<IManagedNodeViewModel, RxVoid>)Manager.LoadProjectScenarioFileCommand).Execute(Node(id));

            public async Task ExecuteAsync(System.Windows.Input.ICommand command) =>
                await ((ReactiveCommand<RxVoid, RxVoid>)command).Execute();

            public void Dispose()
            {
                Manager.Dispose();
                Core.Dispose();
            }
        }
    }
}
