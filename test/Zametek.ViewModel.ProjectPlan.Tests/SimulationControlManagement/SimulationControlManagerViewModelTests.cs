using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using static Zametek.ViewModel.ProjectPlan.Tests.SequencerScopes;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The dock "Simulation": it is the shared simulation seen as a control panel, and what it adds is the name of the
    /// scenario. It makes no reactive pipeline until it is started (see IStartSubscriptions), and it makes none once it is
    /// killed or disposed.
    /// </summary>
    /// <remarks>
    /// The pumps stand in for the thread pool and the main thread and queue what the dock hands them, so each test says
    /// when a delivery runs. A change of the title is what the scenario manager does when a scenario loads, closes or is
    /// renamed: it changes the setting and arms the title.
    /// </remarks>
    public class SimulationControlManagerViewModelTests
    {
        private sealed class Harness
            : IDisposable
        {
            private readonly CoreViewModel m_Core;
            private readonly ProjectScenarioManagerViewModel m_Project;
            private readonly SimulationChartManagerViewModel m_Simulation;

            private Harness(
                CoreViewModel core,
                ISettingService settings,
                ProjectScenarioManagerViewModel project,
                SimulationChartManagerViewModel simulation,
                SimulationControlManagerViewModel control)
            {
                m_Core = core;
                Settings = settings;
                m_Project = project;
                m_Simulation = simulation;
                Control = control;
            }

            public ISettingService Settings { get; }

            public SimulationControlManagerViewModel Control { get; }

            public SimulationChartManagerViewModel Simulation => m_Simulation;

            public static Harness Make(string scenarioTitle = "")
            {
                (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
                IDialogService dialogs = CoreViewModelFixture.CreateDialogService();
                var calculator = new DateTimeCalculator(TimeProvider.System);

                if (scenarioTitle.Length != 0)
                {
                    settings.SetProjectScenarioTitle(scenarioTitle);
                }

                var project = new ProjectScenarioManagerViewModel(
                    core,
                    settings,
                    dialogs,
                    calculator,
                    CoreViewModelFixture.CreateUIDispatcher());

                var simulation = new SimulationChartManagerViewModel(
                    core,
                    settings,
                    dialogs,
                    calculator,
                    new ScottPlotImageExporter(),
                    CoreViewModelFixture.CreateUIDispatcher(),
                    NullLogger<SimulationChartManagerViewModel>.Instance);

                var control = new SimulationControlManagerViewModel(simulation, project, settings);

                return new Harness(core, settings, project, simulation, control);
            }

            // What the scenario manager does when the name of the scenario changes: the setting first, then the title is armed.
            public void ChangeScenarioTitle(string title)
            {
                Settings.SetProjectScenarioTitle(title);
                m_Project.IsReadyToReviseTitle = ReadyToRevise.Yes;
            }

            public void Dispose()
            {
                Control.Dispose();
                m_Simulation.Dispose();
                m_Project.Dispose();
                m_Core.Dispose();
            }
        }

        [Fact]
        public void Construction_Given_ADock_Then_ItIsTheDockSimulationOverTheSharedSimulation()
        {
            using Harness harness = Harness.Make();

            harness.Control.Title.ShouldBe(Resource.ProjectPlan.Titles.Title_Simulation);
            harness.Control.Id.ShouldBe(Resource.ProjectPlan.Titles.Title_Simulation);
            harness.Control.Simulation.ShouldBeSameAs(harness.Simulation);
        }

        [Fact]
        public void Construction_Given_ADock_Then_NothingIsHandedToAnotherThread()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();

            using Harness harness = Harness.Make();

            main.Pump.PendingCount.ShouldBe(0);
            pool.Pump.PendingCount.ShouldBe(0);
        }

        [Fact]
        public void Construction_Given_ASettingWithAScenarioName_Then_TheDockShowsItAtOnce()
        {
            using Harness harness = Harness.Make("Plan A");

            harness.Control.ScenarioTitle.ShouldBe("Plan A");
            harness.Control.HasScenarioTitle.ShouldBeTrue();
        }

        [Fact]
        public void Construction_Given_NoScenarioName_Then_TheDockHasNone()
        {
            using Harness harness = Harness.Make();

            harness.Control.ScenarioTitle.ShouldBeEmpty();
            harness.Control.HasScenarioTitle.ShouldBeFalse();
        }

        [Fact]
        public void StartSubscriptions_Given_TheTitleIsArmed_Then_TheDockShowsTheNameOfTheScenarioThatIsOpen()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Control.StartSubscriptions();
            Drain(main, pool);
            harness.Control.HasScenarioTitle.ShouldBeFalse();
            List<string?> changed = [];
            harness.Control.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

            harness.ChangeScenarioTitle("Plan B");
            Drain(main, pool);

            harness.Control.ScenarioTitle.ShouldBe("Plan B");
            harness.Control.HasScenarioTitle.ShouldBeTrue();
            changed.ShouldContain(nameof(SimulationControlManagerViewModel.ScenarioTitle));
            changed.ShouldContain(nameof(SimulationControlManagerViewModel.HasScenarioTitle));

            // A project that closes has no scenario, and thus no name.
            harness.ChangeScenarioTitle(string.Empty);
            Drain(main, pool);

            harness.Control.ScenarioTitle.ShouldBeEmpty();
            harness.Control.HasScenarioTitle.ShouldBeFalse();
        }

        [Fact]
        public void StartSubscriptions_Given_TheSameNameAgain_Then_NothingIsAnnounced()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make("Plan A");
            harness.Control.StartSubscriptions();
            Drain(main, pool);
            List<string?> changed = [];
            harness.Control.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

            // A scenario is loaded again with the name that it had.
            harness.ChangeScenarioTitle("Plan A");
            Drain(main, pool);

            changed.ShouldBeEmpty();
            harness.Control.ScenarioTitle.ShouldBe("Plan A");
        }

        [Fact]
        public void StartSubscriptions_Given_ADockAlreadyStarted_Then_StartsNothingAgain()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Control.StartSubscriptions();
            Drain(main, pool);

            harness.Control.StartSubscriptions();

            main.Pump.PendingCount.ShouldBe(0);
            pool.Pump.PendingCount.ShouldBe(0);
        }

        [Fact]
        public void StartSubscriptions_Given_AKilledDock_Then_StartsNothing()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Control.KillSubscriptions();

            harness.Control.StartSubscriptions();

            main.Pump.PendingCount.ShouldBe(0);
            pool.Pump.PendingCount.ShouldBe(0);
        }

        [Fact]
        public void KillSubscriptions_Given_AStartedDock_Then_ItDeliversNothingMore()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Control.StartSubscriptions();
            Drain(main, pool);
            harness.ChangeScenarioTitle("Plan B");
            (main.Pump.PendingCount + pool.Pump.PendingCount).ShouldBeGreaterThan(0, "the pipeline was not running to begin with");
            Drain(main, pool);

            harness.Control.KillSubscriptions();
            harness.ChangeScenarioTitle("Plan C");
            Drain(main, pool);

            harness.Control.ScenarioTitle.ShouldBe("Plan B");
        }

        [Fact]
        public void Dispose_Given_AStartedDock_Then_ItDeliversNothingMore()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Control.StartSubscriptions();
            Drain(main, pool);

            harness.Control.Dispose();
            harness.ChangeScenarioTitle("Plan B");

            main.Pump.PendingCount.ShouldBe(0);
            pool.Pump.PendingCount.ShouldBe(0);
        }
    }
}
