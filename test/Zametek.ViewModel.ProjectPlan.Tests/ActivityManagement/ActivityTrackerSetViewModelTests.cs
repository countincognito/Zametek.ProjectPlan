using Shouldly;
using System;
using System.ComponentModel;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using static Zametek.ViewModel.ProjectPlan.Tests.SequencerScopes;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The day cells of an activity follow the window of days and the compilations. A request for a revision of the trackers
    /// changes nothing that they show, and so it does not refresh them. They used to follow a flag that a compilation cleared:
    /// the cells did not follow the window while the flag was set, and a compilation with no request before it refreshed nothing.
    /// </summary>
    public class ActivityTrackerSetViewModelTests
    {
        private const int c_ActivityCount = 6;
        private const int c_ActivityId = 1;

        private static readonly ActivityTrackerModel[] s_Trackers =
        [
            new ActivityTrackerModel { Time = 0, ActivityId = c_ActivityId, PercentageComplete = 10 },
            new ActivityTrackerModel { Time = 3, ActivityId = c_ActivityId, PercentageComplete = 40 },
        ];

        // The set refreshes what its day cells show when its pipeline delivers, and these count that.
        private sealed class Refreshes
        {
            public Refreshes(ActivityTrackerSetViewModel set)
            {
                ((INotifyPropertyChanged)set).PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(IActivityTrackerSetViewModel.SearchSymbol))
                    {
                        OfTheSet++;
                    }
                };

                ((INotifyPropertyChanged)set.Days[0]).PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(IActivityTrackerDayViewModel.PercentageCompleted))
                    {
                        OfTheFirstDay++;
                    }
                };
            }

            public int OfTheSet { get; private set; }

            public int OfTheFirstDay { get; private set; }
        }

        private static GatedCoreViewModel CreateLoaded(int compilationTimeoutMilliseconds = AppSettingsModel.DefaultCompilationTimeoutMilliseconds)
        {
            (GatedCoreViewModel core, _) = CoreViewModelFixture.CreateGated(compilationTimeoutMilliseconds);
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            return core;
        }

        private static (ActivityTrackerSetViewModel Set, Refreshes Refreshes) StartSet(GatedCoreViewModel core, MainThreadSequencerScope main, TaskpoolSequencerScope pool)
        {
            var set = new ActivityTrackerSetViewModel(core, c_ActivityId, s_Trackers);
            set.StartSubscriptions();
            Drain(main, pool);
            return (set, new Refreshes(set));
        }

        [Fact]
        public void RunCompile_Given_NoRequestBeforeIt_Then_TheDaysRefresh()
        {
            // A change of a setting compiles the plan with no request for a revision.
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateLoaded();
            (ActivityTrackerSetViewModel set, Refreshes refreshes) = StartSet(core, main, pool);
            using ActivityTrackerSetViewModel owner = set;

            core.RunCompile();
            Drain(main, pool);

            refreshes.OfTheSet.ShouldBe(1);
            refreshes.OfTheFirstDay.ShouldBe(1);
        }

        [Fact]
        public void RunCompile_Given_ARequestBeforeIt_Then_TheDaysRefreshOnce()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateLoaded();
            (ActivityTrackerSetViewModel set, Refreshes refreshes) = StartSet(core, main, pool);
            using ActivityTrackerSetViewModel owner = set;

            core.ReviseTrackers();
            core.RunCompile();
            Drain(main, pool);

            refreshes.OfTheSet.ShouldBe(1);
            refreshes.OfTheFirstDay.ShouldBe(1);
        }

        [Fact]
        public void ReviseTrackers_Given_NoCompilation_Then_TheDaysDoNotRefresh()
        {
            // A plan has a set for each activity, with fifteen day cells in each. A refresh for each request is a large fan-out.
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateLoaded();
            (ActivityTrackerSetViewModel set, Refreshes refreshes) = StartSet(core, main, pool);
            using ActivityTrackerSetViewModel owner = set;

            core.ReviseTrackers();
            Drain(main, pool);

            refreshes.OfTheSet.ShouldBe(0);
            refreshes.OfTheFirstDay.ShouldBe(0);
        }

        [Fact]
        public void TrackerIndex_Given_ARequestWithNoCompilation_Then_TheDaysFollowTheWindow()
        {
            // With no automatic compilation, the remove command asks for a revision and nothing compiles. The days of the columns
            // then moved with the window, and the values in the cells stayed where they were.
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateLoaded();
            (ActivityTrackerSetViewModel set, Refreshes refreshes) = StartSet(core, main, pool);
            using ActivityTrackerSetViewModel owner = set;

            core.ReviseTrackers();
            Drain(main, pool);
            core.TrackerIndex = 3;
            Drain(main, pool);

            refreshes.OfTheFirstDay.ShouldBe(1, "the cells did not follow the window");
            set.Days[0].PercentageCompleted.ShouldBe(40);
        }

        [Fact]
        public void TrackerIndex_Given_ACompilationThatRanOut_Then_TheDaysFollowTheWindow()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateLoaded(compilationTimeoutMilliseconds: 50);
            (ActivityTrackerSetViewModel set, Refreshes refreshes) = StartSet(core, main, pool);
            using ActivityTrackerSetViewModel owner = set;

            core.ReviseTrackers();
            core.StallNextCompilation();
            Should.Throw<GraphCompilationTimeoutException>(core.RunCompile);
            Drain(main, pool);
            core.TrackerIndex = 3;
            Drain(main, pool);

            refreshes.OfTheFirstDay.ShouldBe(1, "the cells did not follow the window after a compilation ran out");
        }

        [Fact]
        public void TrackerIndex_Given_NothingElse_Then_TheDaysFollowTheWindow()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateLoaded();
            (ActivityTrackerSetViewModel set, Refreshes refreshes) = StartSet(core, main, pool);
            using ActivityTrackerSetViewModel owner = set;

            core.TrackerIndex = 3;
            Drain(main, pool);

            refreshes.OfTheFirstDay.ShouldBe(1);
            set.Days[0].PercentageCompleted.ShouldBe(40);
        }

        [Fact]
        public void ResetProjectScenario_Given_NoRequestBeforeIt_Then_TheDaysRefresh()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateLoaded();
            (ActivityTrackerSetViewModel set, Refreshes refreshes) = StartSet(core, main, pool);
            using ActivityTrackerSetViewModel owner = set;

            core.ResetProjectScenario();
            Drain(main, pool);

            refreshes.OfTheFirstDay.ShouldBe(1);
        }

        [Fact]
        public void KillSubscriptions_Given_AStartedSet_Then_ACompilationRefreshesNothing()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateLoaded();
            (ActivityTrackerSetViewModel set, Refreshes refreshes) = StartSet(core, main, pool);
            using ActivityTrackerSetViewModel owner = set;

            set.KillSubscriptions();
            core.RunCompile();
            Drain(main, pool);

            refreshes.OfTheSet.ShouldBe(0);
        }
    }
}
