using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using static Zametek.ViewModel.ProjectPlan.Tests.SequencerScopes;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A project file can give the nodes of its scenarios a cycle: a node that is its own parent, or two nodes that are the
    /// parent of each other. The open accepted each parent, and the path of a tracked node then went from parent to parent
    /// with no end, under the lock of the scenario manager (from the review of src, test and pkg). A node of a cycle now goes
    /// to the top of the tree, as an orphan does, and the path stops at a node that it saw.
    /// </summary>
    public class ProjectScenarioManagerNodeCycleTests
    {
        private static readonly Guid s_FolderOne = Guid.Parse(@"0f7b1c2d-3e4f-4a5b-8c6d-7e8f9a0b1c2d");
        private static readonly Guid s_FolderTwo = Guid.Parse(@"1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d");

        [Fact]
        public async Task ProcessProject_Given_ATrackedNodeThatIsItsOwnParent_Then_ItIsAtTheTopAndItsPathHasAnEnd()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            ProjectModel project = await CoreViewModelFixture.LoadProjectAsync(@"sample_v0_6_1.zpp");
            ProjectScenarioNodeModel scenario = project.Nodes.Single();
            project = project with { Nodes = [scenario with { ParentId = scenario.Id, IsTracked = true }] };

            harness.Manager.ProcessProject(project);
            Drain(main, pool);

            harness.Manager.GetNode(scenario.Id).ShouldNotBeNull().ParentId.ShouldBe(harness.Manager.Root.Id);
            (await BuildTrackedMetricsAsync(harness)).ShouldBe([scenario.Name]);
        }

        [Fact]
        public async Task ProcessProject_Given_TwoFoldersThatAreTheParentOfEachOther_Then_OneIsAtTheTopAndThePathHasAnEnd()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            ProjectModel project = await CoreViewModelFixture.LoadProjectAsync(@"sample_v0_6_1.zpp");
            ProjectScenarioNodeModel scenario = project.Nodes.Single();
            project = project with
            {
                Nodes =
                [
                    new ProjectScenarioNodeModel { Id = s_FolderOne, ParentId = s_FolderTwo, NodeType = ProjectScenarioNodeType.Folder, Name = @"One" },
                    new ProjectScenarioNodeModel { Id = s_FolderTwo, ParentId = s_FolderOne, NodeType = ProjectScenarioNodeType.Folder, Name = @"Two" },
                    scenario with { ParentId = s_FolderOne, IsTracked = true },
                ],
            };

            harness.Manager.ProcessProject(project);
            Drain(main, pool);

            // The first node of the cycle in the file goes to the top, and the second stays below it.
            harness.Manager.GetNode(s_FolderOne).ShouldNotBeNull().ParentId.ShouldBe(harness.Manager.Root.Id);
            harness.Manager.GetNode(s_FolderTwo).ShouldNotBeNull().ParentId.ShouldBe(s_FolderOne);
            (await BuildTrackedMetricsAsync(harness)).ShouldBe([$@"One > {scenario.Name}"]);
        }

        [Fact]
        public void Dispose_Given_ANodeThatIsAChildOfItself_Then_ItEnds()
        {
            // Dispose set its mark only at its end, and ClearChildren then disposed the same node again.
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            IManagedNodeViewModel node = harness.MakeNode();
            node.AddChildren([node]);

            Should.NotThrow(node.Dispose);
        }

        // The paths of the tracked metrics. The build runs on a different thread, so that a build with no end fails the test
        // and does not stop the run.
        private static async Task<List<string>> BuildTrackedMetricsAsync(Harness harness)
        {
            Task build = Task.Run(harness.Manager.BuildTrackedMetrics);
            (await Task.WhenAny(build, Task.Delay(TimeSpan.FromSeconds(30)))).ShouldBe(build, @"the build of the tracked metrics did not end");
            await build;

            return [.. harness.Manager.TrackedMetricsSet.TrackedMetrics.Select(x => x.Path)];
        }

        // A manager over a core, with the services a test never reaches stood in for.
        private sealed class Harness
            : IDisposable
        {
            private readonly System.Reactive.Subjects.BehaviorSubject<IComparer<IManagedNodeViewModel>> m_Comparer = new(
                ProjectScenarioNodeHelper.BuildSortComparer(SortMode.CreatedOn, Common.ProjectPlan.SortDirection.Ascending));

            private Harness(CoreViewModel core, ISettingService settings, ProjectScenarioManagerViewModel manager)
            {
                Core = core;
                Settings = settings;
                Manager = manager;
            }

            public CoreViewModel Core { get; }

            public ISettingService Settings { get; }

            public ProjectScenarioManagerViewModel Manager { get; }

            public static Harness Make()
            {
                (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
                var manager = new ProjectScenarioManagerViewModel(
                    core,
                    settings,
                    CoreViewModelFixture.CreateDialogService(),
                    new DateTimeCalculator(TimeProvider.System),
                    CoreViewModelFixture.CreateUIDispatcher());

                return new Harness(core, settings, manager);
            }

            public IManagedNodeViewModel MakeNode() => new ManagedNodeViewModel(Manager, Core, Settings, m_Comparer);

            public void Dispose()
            {
                Manager.Dispose();
                Core.Dispose();
                m_Comparer.Dispose();
            }
        }
    }
}
