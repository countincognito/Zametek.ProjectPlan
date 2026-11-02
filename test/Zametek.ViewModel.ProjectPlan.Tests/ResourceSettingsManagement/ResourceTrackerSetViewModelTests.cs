using Shouldly;
using System;
using System.ComponentModel;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using static Zametek.ViewModel.ProjectPlan.Tests.SequencerScopes;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The set of trackers of a resource follows the window of days and the compilations, as the set of an activity does. It
    /// holds a selector for each day with a booking, and a revision of the trackers changes these on the thread that asks for it.
    /// </summary>
    public class ResourceTrackerSetViewModelTests
    {
        private const int c_ActivityCount = 6;
        private const int c_ResourceId = 1;
        private const int c_BookedActivityId = 2;
        private const int c_BookedDay = 4;

        private static readonly ResourceTrackerModel[] s_Trackers =
        [
            new ResourceTrackerModel
            {
                Time = c_BookedDay,
                ResourceId = c_ResourceId,
                ActivityTrackers =
                [
                    new ResourceActivityTrackerModel { Time = c_BookedDay, ResourceId = c_ResourceId, ActivityId = c_BookedActivityId, ActivityName = @"Booked", PercentageWorked = 50 },
                ],
            },
        ];

        // The set refreshes what its day cells show when its pipeline delivers, and this counts that.
        private sealed class Refreshes
        {
            public Refreshes(ResourceTrackerSetViewModel set)
            {
                ((INotifyPropertyChanged)set).PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(IResourceTrackerSetViewModel.SearchSymbol))
                    {
                        Count++;
                    }
                };
            }

            public int Count { get; private set; }
        }

        private static GatedCoreViewModel CreateLoaded(int compilationTimeoutMilliseconds = AppSettingsModel.DefaultCompilationTimeoutMilliseconds)
        {
            (GatedCoreViewModel core, _) = CoreViewModelFixture.CreateGated(compilationTimeoutMilliseconds);
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            return core;
        }

        private static (ResourceTrackerSetViewModel Set, Refreshes Refreshes) StartSet(GatedCoreViewModel core, MainThreadSequencerScope main, TaskpoolSequencerScope pool)
        {
            var set = new ResourceTrackerSetViewModel(core, DoNothingProxy.Of<IManagedResourceViewModel>(), c_ResourceId, s_Trackers);
            set.StartSubscriptions();
            Drain(main, pool);
            return (set, new Refreshes(set));
        }

        [Fact]
        public void RunCompile_Given_NoRequestBeforeIt_Then_TheSetRefreshes()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateLoaded();
            (ResourceTrackerSetViewModel set, Refreshes refreshes) = StartSet(core, main, pool);
            using ResourceTrackerSetViewModel owner = set;

            core.RunCompile();
            Drain(main, pool);

            refreshes.Count.ShouldBe(1);
        }

        [Fact]
        public void RunCompile_Given_ARequestBeforeIt_Then_TheSetRefreshesOnce()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateLoaded();
            (ResourceTrackerSetViewModel set, Refreshes refreshes) = StartSet(core, main, pool);
            using ResourceTrackerSetViewModel owner = set;

            core.ReviseTrackers();
            core.RunCompile();
            Drain(main, pool);

            refreshes.Count.ShouldBe(1);
        }

        [Fact]
        public void ReviseTrackers_Given_NoCompilation_Then_TheSetDoesNotRefresh()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateLoaded();
            (ResourceTrackerSetViewModel set, Refreshes refreshes) = StartSet(core, main, pool);
            using ResourceTrackerSetViewModel owner = set;

            core.ReviseTrackers();
            Drain(main, pool);

            refreshes.Count.ShouldBe(0);
        }

        [Fact]
        public void TrackerIndex_Given_ARequestWithNoCompilation_Then_TheSetFollowsTheWindow()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateLoaded();
            (ResourceTrackerSetViewModel set, Refreshes refreshes) = StartSet(core, main, pool);
            using ResourceTrackerSetViewModel owner = set;

            core.ReviseTrackers();
            Drain(main, pool);
            core.TrackerIndex = 3;
            Drain(main, pool);

            refreshes.Count.ShouldBe(1, "the set did not follow the window");
            set.GetDay(c_BookedDay - 3).SelectedResourceActivityIds.ToArray().ShouldBe(new[] { c_BookedActivityId });
        }

        [Fact]
        public void TrackerIndex_Given_ACompilationThatRanOut_Then_TheSetFollowsTheWindow()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateLoaded(compilationTimeoutMilliseconds: 50);
            (ResourceTrackerSetViewModel set, Refreshes refreshes) = StartSet(core, main, pool);
            using ResourceTrackerSetViewModel owner = set;

            core.ReviseTrackers();
            core.StallNextCompilation();
            Should.Throw<GraphCompilationTimeoutException>(core.RunCompile);
            Drain(main, pool);
            core.TrackerIndex = 3;
            Drain(main, pool);

            refreshes.Count.ShouldBe(1, "the set did not follow the window after a compilation ran out");
        }

        [Fact]
        public void RunCompile_Given_TheRemovalOfTheOnlyBookingOfADay_Then_TheSetLetsTheDayGo()
        {
            // The refresh that follows a compilation takes away the selector of a day with no bookings.
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateLoaded();
            (ResourceTrackerSetViewModel set, _) = StartSet(core, main, pool);
            using ResourceTrackerSetViewModel owner = set;
            set.LastTrackerIndex.ShouldBe(c_BookedDay, "precondition: the set has a booking");

            core.RemoveManagedActivities([c_BookedActivityId]);
            core.ReviseTrackers();
            core.RunCompile();
            Drain(main, pool);

            set.LastTrackerIndex.ShouldBeNull();
            set.CloneTrackers().ShouldBeEmpty();
        }

        [Fact]
        public void KillSubscriptions_Given_AStartedSet_Then_ACompilationRefreshesNothing()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateLoaded();
            (ResourceTrackerSetViewModel set, Refreshes refreshes) = StartSet(core, main, pool);
            using ResourceTrackerSetViewModel owner = set;

            set.KillSubscriptions();
            core.RunCompile();
            Drain(main, pool);

            refreshes.Count.ShouldBe(0);
        }
    }
}
