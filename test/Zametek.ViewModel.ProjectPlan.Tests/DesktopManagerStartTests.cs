using Shouldly;
using System;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using static Zametek.ViewModel.ProjectPlan.Tests.SequencerScopes;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The managers that only the desktop builds - the settings managers and the tracking managers - make no reactive
    /// pipeline until they are started (see IStartSubscriptions), make theirs when they are, and make none once they have
    /// been killed or disposed.
    /// </summary>
    /// <remarks>
    /// The pumps stand in for the thread pool and the main thread and queue what a manager hands them, so each test says
    /// when a delivery runs, and the count of what is waiting says whether anything was handed over at all. The core under
    /// a manager is never started, so what is counted is the manager's own. A change to every setting a manager observes
    /// stands in for whatever the user does to provoke its pipelines.
    /// </remarks>
    public class DesktopManagerStartTests
    {
        public static TheoryData<string> Managers => new()
        {
            nameof(ResourceSettingsManagerViewModel),
            nameof(WorkStreamSettingsManagerViewModel),
            nameof(HolidaySettingsManagerViewModel),
            nameof(GraphSettingsManagerViewModel),
            nameof(TrackingManagerViewModel),
            nameof(EffortTrackingManagerViewModel),
        };

        [Theory]
        [MemberData(nameof(Managers))]
        public void Construction_Given_AManager_Then_NothingIsHandedToAnotherThread(string manager)
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();

            using Harness harness = Harness.Make(manager);

            main.Pump.PendingCount.ShouldBe(0);
            pool.Pump.PendingCount.ShouldBe(0);
        }

        [Theory]
        [MemberData(nameof(Managers))]
        public void StartSubscriptions_Given_AManagerNotYetStarted_Then_HandsItsPipelinesTheirFirstValues(string manager)
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make(manager);

            harness.Starter.StartSubscriptions();

            (main.Pump.PendingCount + pool.Pump.PendingCount).ShouldBeGreaterThan(0);
        }

        [Theory]
        [MemberData(nameof(Managers))]
        public void StartSubscriptions_Given_AManagerAlreadyStarted_Then_StartsNothingAgain(string manager)
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make(manager);
            harness.Starter.StartSubscriptions();
            Drain(main, pool);

            harness.Starter.StartSubscriptions();

            main.Pump.PendingCount.ShouldBe(0);
            pool.Pump.PendingCount.ShouldBe(0);
        }

        [Theory]
        [MemberData(nameof(Managers))]
        public void StartSubscriptions_Given_AKilledManager_Then_StartsNothing(string manager)
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make(manager);
            harness.Killer.KillSubscriptions();

            harness.Starter.StartSubscriptions();

            main.Pump.PendingCount.ShouldBe(0);
            pool.Pump.PendingCount.ShouldBe(0);
        }

        [Theory]
        [MemberData(nameof(Managers))]
        public void KillSubscriptions_Given_AStartedManager_Then_ItsPipelinesDeliverNothingMore(string manager)
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make(manager);
            harness.Starter.StartSubscriptions();
            Drain(main, pool);
            harness.ChangeWhatTheManagerObserves();
            (main.Pump.PendingCount + pool.Pump.PendingCount).ShouldBeGreaterThan(0, "the pipelines were not running to begin with");
            Drain(main, pool);

            harness.Killer.KillSubscriptions();
            harness.ChangeWhatTheManagerObserves();

            main.Pump.PendingCount.ShouldBe(0);
            pool.Pump.PendingCount.ShouldBe(0);
        }

        [Theory]
        [MemberData(nameof(Managers))]
        public void Dispose_Given_AStartedManager_Then_ItsPipelinesDeliverNothingMore(string manager)
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make(manager);
            harness.Starter.StartSubscriptions();
            Drain(main, pool);

            harness.DisposeManager();
            harness.ChangeWhatTheManagerObserves();

            main.Pump.PendingCount.ShouldBe(0);
            pool.Pump.PendingCount.ShouldBe(0);
        }

        [Fact]
        public void StartSubscriptions_Given_AnEffortTrackingManager_Then_StartsThePipelinesOfItsBaseAsWellAsItsOwn()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make(nameof(EffortTrackingManagerViewModel));

            harness.Starter.StartSubscriptions();

            // The base class hands its column titles to the thread pool, and the manager its timesheet to the main thread.
            pool.Pump.PendingCount.ShouldBeGreaterThan(0, "the base class did not start its pipeline");
            main.Pump.PendingCount.ShouldBeGreaterThan(0, "the manager did not start its own pipeline");
        }

        // A manager over a core, with the services a test never reaches stood in for.
        private sealed class Harness
            : IDisposable
        {
            private readonly CoreViewModel m_Core;
            private readonly ResourceSettingsManagerViewModel m_Resources;
            private readonly object m_Manager;

            private Harness(CoreViewModel core, ResourceSettingsManagerViewModel resources, object manager)
            {
                m_Core = core;
                m_Resources = resources;
                m_Manager = manager;
            }

            public IStartSubscriptions Starter => (IStartSubscriptions)m_Manager;

            public IKillSubscriptions Killer => (IKillSubscriptions)m_Manager;

            public static Harness Make(string manager)
            {
                (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
                IDialogService dialogs = CoreViewModelFixture.CreateDialogService();
                var calculator = new DateTimeCalculator(TimeProvider.System);

                // Not started, whichever manager is under test: the pumps would show what it handed over.
                var resources = new ResourceSettingsManagerViewModel(core, settings, dialogs);

                object subject = manager switch
                {
                    nameof(ResourceSettingsManagerViewModel) => resources,
                    nameof(WorkStreamSettingsManagerViewModel) => new WorkStreamSettingsManagerViewModel(core, resources, settings, dialogs),
                    nameof(HolidaySettingsManagerViewModel) => new HolidaySettingsManagerViewModel(core, calculator, settings, dialogs),
                    nameof(GraphSettingsManagerViewModel) => new GraphSettingsManagerViewModel(core, settings, dialogs),
                    nameof(TrackingManagerViewModel) => new TrackingManagerViewModel(core, resources, calculator),
                    nameof(EffortTrackingManagerViewModel) => new EffortTrackingManagerViewModel(core, resources, calculator),
                    _ => throw new ArgumentOutOfRangeException(nameof(manager), manager, null),
                };

                return new Harness(core, resources, subject);
            }

            // What a plan being loaded changes, or the user: every setting the managers observe, the day the tracking
            // managers show, and - for a settings manager - the mark of an edit made in its own grid. Each setting is a
            // new model, which is never equal to the one before.
            public void ChangeWhatTheManagerObserves()
            {
                m_Core.ResourceSettings = new ResourceSettingsModel();
                m_Core.WorkStreamSettings = new WorkStreamSettingsModel();
                m_Core.HolidaySettings = new HolidaySettingsModel();
                m_Core.GraphSettings = new GraphSettingsModel();
                m_Core.TrackerIndex++;

                switch (m_Manager)
                {
                    case IResourceSettingsManagerViewModel resources:
                        resources.AreSettingsUpdated = true;
                        break;
                    case IWorkStreamSettingsManagerViewModel workStreams:
                        workStreams.AreSettingsUpdated = true;
                        break;
                    case IHolidaySettingsManagerViewModel holidays:
                        holidays.AreSettingsUpdated = true;
                        break;
                    case IGraphSettingsManagerViewModel graph:
                        graph.AreSettingsUpdated = true;
                        break;
                }
            }

            public void DisposeManager()
            {
                (m_Manager as IDisposable)?.Dispose();
            }

            public void Dispose()
            {
                DisposeManager();
                m_Resources.Dispose();
                m_Core.Dispose();
            }
        }
    }
}
