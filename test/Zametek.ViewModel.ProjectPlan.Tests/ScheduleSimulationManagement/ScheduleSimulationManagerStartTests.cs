using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System;
using Xunit;
using static Zametek.ViewModel.ProjectPlan.Tests.SequencerScopes;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The simulation panel makes no reactive pipeline until it is started (see IStartSubscriptions), makes its
    /// pipeline when it is, and makes none once it has been killed or disposed.
    /// </summary>
    /// <remarks>
    /// The pumps stand in for the thread pool and the main thread and queue what the panel hands them, so each test
    /// says when a delivery runs. The core under the panel is never started, so what is counted is the panel's own.
    /// A change to the project start stands in for whatever the user does to change how the result is shown.
    /// </remarks>
    public class ScheduleSimulationManagerStartTests
    {
        private sealed class Harness
            : IDisposable
        {
            private readonly CoreViewModel m_Core;

            public ScheduleSimulationManagerViewModel Panel { get; }

            private Harness(CoreViewModel core, ScheduleSimulationManagerViewModel panel)
            {
                m_Core = core;
                Panel = panel;
            }

            public static Harness Make()
            {
                CoreViewModel core = CoreViewModelFixture.Create();
                var panel = new ScheduleSimulationManagerViewModel(
                    core,
                    CoreViewModelFixture.CreateSettingService(),
                    CoreViewModelFixture.CreateDialogService(),
                    new DateTimeCalculator(TimeProvider.System),
                    new ScottPlotImageExporter(),
                    CoreViewModelFixture.CreateUIDispatcher(),
                    NullLogger<ScheduleSimulationManagerViewModel>.Instance);

                return new Harness(core, panel);
            }

            public void ChangeWhatThePanelObserves()
            {
                m_Core.ProjectStart = m_Core.ProjectStart.AddDays(1);
            }

            public void Dispose()
            {
                Panel.Dispose();
                m_Core.Dispose();
            }
        }

        [Fact]
        public void Construction_Given_APanel_Then_NothingIsHandedToAnotherThread()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();

            using Harness harness = Harness.Make();

            main.Pump.PendingCount.ShouldBe(0);
            pool.Pump.PendingCount.ShouldBe(0);
        }

        [Fact]
        public void StartSubscriptions_Given_APanelNotYetStarted_Then_HandsItsPipelineItsFirstValue()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();

            harness.Panel.StartSubscriptions();

            (main.Pump.PendingCount + pool.Pump.PendingCount).ShouldBeGreaterThan(0);
        }

        [Fact]
        public void StartSubscriptions_Given_APanelAlreadyStarted_Then_StartsNothingAgain()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Panel.StartSubscriptions();
            Drain(main, pool);

            harness.Panel.StartSubscriptions();

            main.Pump.PendingCount.ShouldBe(0);
            pool.Pump.PendingCount.ShouldBe(0);
        }

        [Fact]
        public void StartSubscriptions_Given_AKilledPanel_Then_StartsNothing()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Panel.KillSubscriptions();

            harness.Panel.StartSubscriptions();

            main.Pump.PendingCount.ShouldBe(0);
            pool.Pump.PendingCount.ShouldBe(0);
        }

        [Fact]
        public void KillSubscriptions_Given_AStartedPanel_Then_ItsPipelineDeliversNothingMore()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Panel.StartSubscriptions();
            Drain(main, pool);
            harness.ChangeWhatThePanelObserves();
            (main.Pump.PendingCount + pool.Pump.PendingCount).ShouldBeGreaterThan(0, "the pipeline was not running to begin with");
            Drain(main, pool);

            harness.Panel.KillSubscriptions();
            harness.ChangeWhatThePanelObserves();

            main.Pump.PendingCount.ShouldBe(0);
            pool.Pump.PendingCount.ShouldBe(0);
        }

        [Fact]
        public void Dispose_Given_AStartedPanel_Then_ItsPipelineDeliversNothingMore()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Panel.StartSubscriptions();
            Drain(main, pool);

            harness.Panel.Dispose();
            harness.ChangeWhatThePanelObserves();

            main.Pump.PendingCount.ShouldBe(0);
            pool.Pump.PendingCount.ShouldBe(0);
        }
    }
}
