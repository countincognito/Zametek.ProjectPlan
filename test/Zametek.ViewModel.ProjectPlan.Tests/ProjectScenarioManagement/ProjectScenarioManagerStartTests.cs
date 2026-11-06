using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using static Zametek.ViewModel.ProjectPlan.Tests.SequencerScopes;
using SortDirection = Zametek.Common.ProjectPlan.SortDirection;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A project scenario manager makes no reactive pipeline until it is started (see IStartSubscriptions), and the nodes
    /// it makes are started with it - the root it begins with, the root a reset replaces it with, and the scenarios a
    /// project is opened with - and not otherwise.
    /// </summary>
    /// <remarks>
    /// The pumps stand in for the thread pool and the main thread and queue what the manager hands them, so each test says
    /// when a delivery runs, and the count of what is waiting says whether anything was handed over at all.
    /// </remarks>
    public class ProjectScenarioManagerStartTests
    {
        [Fact]
        public void Construction_Given_AManager_Then_NothingIsHandedToAnotherThread()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();

            using Harness harness = Harness.Make();

            main.Pump.PendingCount.ShouldBe(0);
            pool.Pump.PendingCount.ShouldBe(0);
        }

        [Fact]
        public void StartSubscriptions_Given_AManagerNotYetStarted_Then_HandsItsPipelinesTheirFirstValues()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();

            harness.Manager.StartSubscriptions();

            pool.Pump.PendingCount.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void StartSubscriptions_Given_AKilledManager_Then_StartsNothing()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Manager.KillSubscriptions();

            harness.Manager.StartSubscriptions();

            main.Pump.PendingCount.ShouldBe(0);
            pool.Pump.PendingCount.ShouldBe(0);
        }

        [Fact]
        public void ResetProject_Given_AStartedManager_Then_TheRootItMakesIsStarted()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Manager.StartSubscriptions();
            Drain(main, pool);

            harness.Manager.ResetProject();
            Drain(main, pool);
            int before = harness.Manager.Root.Children.Count;
            harness.Manager.Root.AddChildren([harness.MakeNode()]);
            Drain(main, pool);

            // The root's view of its children is filled by a pipeline of its own.
            harness.Manager.Root.Children.Count.ShouldBe(before + 1);
        }

        [Fact]
        public void ResetProject_Given_AManagerNotStarted_Then_TheRootItMakesIsNot()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();

            harness.Manager.ResetProject();
            harness.Manager.Root.AddChildren([harness.MakeNode()]);
            Drain(main, pool);

            // Nothing fills the root's view of its children, which stays as it was made: empty.
            harness.Manager.Root.Children.Count.ShouldBe(0);
        }

        [Fact]
        public void StartSubscriptions_Given_ARootMadeBeforeIt_Then_StartsItWithIt()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Manager.Root.AddChildren([harness.MakeNode()]);
            Drain(main, pool);
            harness.Manager.Root.Children.Count.ShouldBe(0);

            harness.Manager.StartSubscriptions();
            Drain(main, pool);

            harness.Manager.Root.Children.Count.ShouldBe(1);
        }

        [Fact]
        public async Task ProcessProject_Given_AStartedManager_Then_TheScenarioNodesAreStarted()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Manager.StartSubscriptions();
            Drain(main, pool);

            IManagedNodeViewModel node = await OpenAsync(harness);

            // A scenario that is edited is marked with an asterisk, which its node puts there when the core says so.
            harness.Core.IsProjectScenarioUpdated = false;
            harness.Core.IsProjectScenarioUpdated = true;
            Drain(main, pool);

            node.IsUpdated.ShouldBeTrue();
        }

        [Fact]
        public async Task StartSubscriptions_Given_ScenariosOpenedBeforeIt_Then_StartsTheirNodesWithIt()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            IManagedNodeViewModel node = await OpenAsync(harness);
            harness.Core.IsProjectScenarioUpdated = true;
            Drain(main, pool);
            node.IsUpdated.ShouldBeFalse("a node of a manager that is not started marked its scenario as edited");

            harness.Manager.StartSubscriptions();
            Drain(main, pool);

            node.IsUpdated.ShouldBeTrue();
        }

        [Fact]
        public async Task ProcessProject_Given_AManagerNotStarted_Then_TheScenarioNodesAreNot()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();

            IManagedNodeViewModel node = await OpenAsync(harness);

            harness.Core.IsProjectScenarioUpdated = false;
            harness.Core.IsProjectScenarioUpdated = true;
            Drain(main, pool);

            node.IsUpdated.ShouldBeFalse();
        }

        // Opens the sample project into the manager, and returns the node of the scenario it landed on.
        private static async Task<IManagedNodeViewModel> OpenAsync(Harness harness)
        {
            ProjectModel project = await CoreViewModelFixture.LoadProjectAsync(@"sample_v0_6_1.zpp");
            harness.Manager.ProcessProject(project);

            return harness.Manager.GetNode(project.Current).ShouldNotBeNull();
        }

        // A manager over a core, with the services a test never reaches stood in for.
        private sealed class Harness
            : IDisposable
        {
            private readonly BehaviorSubject<IComparer<IManagedNodeViewModel>> m_Comparer = new(
                ProjectScenarioNodeHelper.BuildSortComparer(SortMode.CreatedOn, SortDirection.Ascending));

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
                // The core and the manager share a settings service, as they do in the application: the core records
                // the scenario it loads there, and the nodes read it back.
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
