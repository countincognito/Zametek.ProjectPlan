using Shouldly;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Xunit;
using Zametek.Contract.ProjectPlan;
using static Zametek.ViewModel.ProjectPlan.Tests.SequencerScopes;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A core view model makes no reactive pipeline until it is started (see IStartSubscriptions), and the activities it
    /// makes are started with it - those it made before and those it makes after - and not otherwise.
    /// </summary>
    /// <remarks>
    /// The pumps stand in for the thread pool and the main thread and queue what the core hands them, so each test says
    /// when a delivery runs, and the count of what is waiting says whether anything was handed over at all. No test waits
    /// for time to pass.
    /// </remarks>
    public class CoreViewModelStartTests
    {
        private const int c_ActivityCount = 6;

        [Fact]
        public void Construction_Given_ACoreAndAPlanLoadedIntoIt_Then_NothingIsHandedToAnotherThread()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();

            using CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");

            core.RawActivities.Count.ShouldBe(c_ActivityCount);
            main.Pump.PendingCount.ShouldBe(0);
            pool.Pump.PendingCount.ShouldBe(0);
        }

        [Fact]
        public void StartSubscriptions_Given_ACoreNotYetStarted_Then_HandsItsPipelinesTheirFirstValues()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModel core = CoreViewModelFixture.Create();
            pool.Pump.PendingCount.ShouldBe(0);

            core.StartSubscriptions();

            pool.Pump.PendingCount.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void StartSubscriptions_Given_ACoreAlreadyStarted_Then_StartsNothingAgain()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.StartSubscriptions();
            Drain(main, pool);

            core.StartSubscriptions();

            main.Pump.PendingCount.ShouldBe(0);
            pool.Pump.PendingCount.ShouldBe(0);
        }

        [Fact]
        public void StartSubscriptions_Given_AKilledCore_Then_StartsNothing()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.KillSubscriptions();

            core.StartSubscriptions();

            main.Pump.PendingCount.ShouldBe(0);
            pool.Pump.PendingCount.ShouldBe(0);
        }

        [Fact]
        public void ProcessProjectScenario_Given_AStartedCore_Then_TheActivitiesItMakesAreStarted()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.StartSubscriptions();
            Drain(main, pool);

            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            Dictionary<int, int> refreshed = WatchTrackerSets(core);
            Drain(main, pool);

            refreshed.Count.ShouldBe(c_ActivityCount);
            refreshed.Values.ShouldAllBe(x => x > 0, "an activity's tracker set did not refresh its days");
        }

        [Fact]
        public void StartSubscriptions_Given_ActivitiesMadeBeforeIt_Then_StartsThemWithIt()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            Dictionary<int, int> refreshed = WatchTrackerSets(core);
            Drain(main, pool);
            refreshed.Values.ShouldAllBe(x => x == 0, "an activity of a core that is not started refreshed its days");

            core.StartSubscriptions();
            Drain(main, pool);

            refreshed.Count.ShouldBe(c_ActivityCount);
            refreshed.Values.ShouldAllBe(x => x > 0, "an activity's tracker set did not refresh its days");
        }

        [Fact]
        public void ProcessProjectScenario_Given_ACoreNotStarted_Then_TheActivitiesItMakesAreNot()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModel core = CoreViewModelFixture.Create();

            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            Dictionary<int, int> refreshed = WatchTrackerSets(core);
            core.TrackerIndex = 3;
            Drain(main, pool);

            refreshed.Count.ShouldBe(c_ActivityCount);
            refreshed.Values.ShouldAllBe(x => x == 0, "an activity of a core that is not started refreshed its days");
        }

        [Fact]
        public void KillSubscriptions_Given_AStartedActivity_Then_ItsTrackerSetDeliversNothingMore()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.StartSubscriptions();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            Drain(main, pool);
            IManagedActivityViewModel activity = core.RawActivities.First();
            int refreshed = 0;
            ((INotifyPropertyChanged)activity.TrackerSet).PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(IActivityTrackerSetViewModel.SearchSymbol))
                {
                    refreshed++;
                }
            };
            core.TrackerIndex = 2;
            Drain(main, pool);
            refreshed.ShouldBeGreaterThan(0, "the tracker set was not refreshing its days to begin with");
            refreshed = 0;

            activity.KillSubscriptions();
            core.TrackerIndex = 3;
            Drain(main, pool);

            refreshed.ShouldBe(0);
        }

        // How many times each activity's tracker set has refreshed what its day cells show, which it does when its
        // pipeline delivers.
        private static Dictionary<int, int> WatchTrackerSets(CoreViewModel core)
        {
            Dictionary<int, int> refreshed = core.RawActivities.ToDictionary(x => x.Id, _ => 0);

            foreach (IManagedActivityViewModel activity in core.RawActivities)
            {
                int id = activity.Id;
                ((INotifyPropertyChanged)activity.TrackerSet).PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(IActivityTrackerSetViewModel.SearchSymbol))
                    {
                        refreshed[id]++;
                    }
                };
            }

            return refreshed;
        }
    }
}
