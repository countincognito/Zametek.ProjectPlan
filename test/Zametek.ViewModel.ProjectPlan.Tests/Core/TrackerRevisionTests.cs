using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using static Zametek.ViewModel.ProjectPlan.Tests.SequencerScopes;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A revision of the trackers brings the selectors of activities into line with the plan, during the call, on the thread
    /// that makes it. Each request gives a revision. A flag that was already set used to lose the request of an edit made
    /// during a compilation, and the selectors then never heard of the edit.
    /// </summary>
    public class TrackerRevisionTests
    {
        private static readonly TimeSpan s_Patience = TimeSpan.FromSeconds(10);

        private const int c_ActivityCount = 6;
        private const int c_BookedActivityId = 2;
        private const int c_UnknownActivityId = 99;

        private static GatedCoreViewModel CreateLoaded()
        {
            (GatedCoreViewModel core, _) = CoreViewModelFixture.CreateGated();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            return core;
        }

        // A core as the application has it: started, with a plan, and with automatic compilation on.
        private static GatedCoreViewModel CreateStartedWithAutoCompile(MainThreadSequencerScope main, TaskpoolSequencerScope pool)
        {
            (GatedCoreViewModel core, _) = CoreViewModelFixture.CreateGated();
            core.StartSubscriptions();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            Drain(main, pool);
            core.AutoCompile = true;
            Drain(main, pool);
            return core;
        }

        // One day of a resource, with one booking of half a day.
        private static ResourceActivitySelectorViewModel BookedDay(ICoreViewModel core, int activityId = c_BookedActivityId) =>
            new(core, new ResourceTrackerModel
            {
                Time = 0,
                ResourceId = 1,
                ActivityTrackers =
                [
                    new ResourceActivityTrackerModel
                    {
                        Time = 0,
                        ResourceId = 1,
                        ActivityId = activityId,
                        ActivityName = $@"Activity {activityId}",
                        PercentageWorked = 50,
                    },
                ],
            });

        private static int[] Targets(GanttActivitySelectorViewModel selector) =>
            [.. selector.TargetActivities.Select(x => x.Id).Order()];

        private static int[] Targets(ResourceActivitySelectorViewModel day) =>
            [.. day.TargetResourceActivities.Select(x => x.Id).Order()];

        private static string NameOf(ResourceActivitySelectorViewModel day, int activityId) =>
            day.TargetResourceActivities.Single(x => x.Id == activityId).Name;

        // Commits a new name as the grid does.
        private static void Rename(IManagedActivityViewModel activity, string name)
        {
            var editable = (IEditableObject)activity;
            editable.BeginEdit();
            activity.Name = name;
            editable.EndEdit();
        }

        // What the function gives at each raise of the property.
        private static List<T> RecordRaises<T>(ICoreViewModel core, string propertyName, Func<T> read)
        {
            var raises = new List<T>();

            ((INotifyPropertyChanged)core).PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == propertyName)
                {
                    raises.Add(read());
                }
            };

            return raises;
        }

        #region Each request gives a revision

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReviseTrackers_Given_AnActivityAddedDuringACompilation_Then_ItReachesTheSelectors(bool acrossARollover)
        {
            using GatedCoreViewModel core = CreateLoaded();

            if (acrossARollover)
            {
                RevisionCounters.SetTrackersRevision(core, int.MaxValue - 1);
            }

            using var gantt = new GanttActivitySelectorViewModel(core);
            using ResourceActivitySelectorViewModel day = BookedDay(core);

            // The first add, as the activities manager makes it.
            int first = core.AddManagedActivity(c_ActivityCount + 1);
            core.ReviseTrackers();
            Targets(gantt).ShouldContain(first, "precondition: the first add reaches the selector of the Gantt chart");
            Targets(day).ShouldContain(first, "precondition: the first add reaches the day of the resource");

            core.HoldNextCompilation();
            Task compile = Task.Run(core.RunCompile);
            core.WaitUntilHeld();

            int second;

            try
            {
                // The second add lands while that compilation runs.
                second = core.AddManagedActivity(c_ActivityCount + 2);
                core.ReviseTrackers();
            }
            finally
            {
                core.ReleaseHeldCompilation();
            }

            await compile.WaitAsync(s_Patience);

            Targets(gantt).ShouldContain(second, "the selector of the Gantt chart never heard of the second activity");
            Targets(day).ShouldContain(second, "the day of the resource never heard of the second activity");
        }

        [Fact]
        public async Task ReviseTrackers_Given_ARemovalDuringACompilation_Then_TheBookingGoes()
        {
            using GatedCoreViewModel core = CreateLoaded();
            using ResourceActivitySelectorViewModel day = BookedDay(core);
            core.ReviseTrackers();

            core.HoldNextCompilation();
            Task compile = Task.Run(core.RunCompile);
            core.WaitUntilHeld();

            try
            {
                core.RemoveManagedActivities([c_BookedActivityId]);
                core.ReviseTrackers();
            }
            finally
            {
                core.ReleaseHeldCompilation();
            }

            await compile.WaitAsync(s_Patience);

            day.SelectedResourceActivityIds.ShouldBeEmpty("the day still has a booking of an activity that the plan does not have");
            Targets(day).ShouldNotContain(c_BookedActivityId);
        }

        [Fact]
        public async Task Edit_Given_ACompilationThatARevisionStarted_Then_TheNewNameReachesTheSelectors()
        {
            // The compile command asks for a revision and compiles. A rename in the grid then lands during that compilation.
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateStartedWithAutoCompile(main, pool);
            using ResourceActivitySelectorViewModel day = BookedDay(core);
            core.ReviseTrackers();

            core.HoldNextCompilation();
            Task compile = Task.Run(core.RunCompile);
            core.WaitUntilHeld();

            try
            {
                Rename(core.RawActivities.Single(x => x.Id == 3), @"Renamed");
                Drain(main, pool);
            }
            finally
            {
                core.ReleaseHeldCompilation();
            }

            await compile.WaitAsync(s_Patience);
            Drain(main, pool);

            NameOf(day, 3).ShouldBe(@"Renamed", "the day of the resource kept the name from before the edit");
        }

        [Fact]
        public void ReviseTrackers_Given_ASelector_Then_ItHasRevisedWhenTheCallReturns()
        {
            using GatedCoreViewModel core = CreateLoaded();
            using var gantt = new GanttActivitySelectorViewModel(core);
            using ResourceActivitySelectorViewModel day = BookedDay(core);
            int added = core.AddManagedActivity(c_ActivityCount + 1);
            Targets(gantt).ShouldNotContain(added, "precondition: nothing told the selector");
            Targets(day).ShouldNotContain(added, "precondition: nothing told the day");

            core.ReviseTrackers();

            Targets(gantt).ShouldContain(added);
            Targets(day).ShouldContain(added);
        }

        [Fact]
        public void ReviseTrackers_Given_NothingNew_Then_NothingInTheSelectorsChanges()
        {
            // Requests can follow one another with nothing new between them. So a revision that has nothing to do must do nothing.
            // An open dropdown shows these lists, and the UI thread reads them while a revision runs on another thread.
            using GatedCoreViewModel core = CreateLoaded();
            core.DisplaySettingsViewModel.SetValues(new ProjectScenarioDisplaySettingsModel { GanttChartShowConnections = [3] });
            using var gantt = new GanttActivitySelectorViewModel(core);
            using ResourceActivitySelectorViewModel day = BookedDay(core);
            core.IsProjectScenarioUpdated = false;

            int changes = 0;
            void Count(object? sender, NotifyCollectionChangedEventArgs e) => changes++;
            ((INotifyCollectionChanged)gantt.TargetActivities).CollectionChanged += Count;
            gantt.SelectedTargetActivities.CollectionChanged += Count;
            ((INotifyCollectionChanged)day.TargetResourceActivities).CollectionChanged += Count;
            day.SelectedTargetResourceActivities.CollectionChanged += Count;

            core.ReviseTrackers();
            core.ReviseTrackers();

            changes.ShouldBe(0);
            core.IsProjectScenarioUpdated.ShouldBeFalse("a revision was taken for an edit by the user");
            core.DisplaySettingsViewModel.GanttChartShowConnections.ToArray().ShouldBe(new[] { 3 });
            day.SelectedTargetResourceActivities.Single().PercentageWorked.ShouldBe(50);
        }

        [Fact]
        public void ReviseTrackers_Given_TheRemovalOfAnActivityInTheFilter_Then_ThePersistedFilterKeepsIt()
        {
            // A revision takes the activity out of the selection of the Gantt chart. That is not an edit by the user: it must not go
            // to the persisted filter, and it must not mark the scenario as updated.
            using GatedCoreViewModel core = CreateLoaded();
            core.DisplaySettingsViewModel.SetValues(new ProjectScenarioDisplaySettingsModel { GanttChartShowConnections = [c_BookedActivityId, 3] });
            using var gantt = new GanttActivitySelectorViewModel(core);
            gantt.SelectedActivityIds.Order().ToArray().ShouldBe(new[] { c_BookedActivityId, 3 }, "precondition: the selector has the filter");
            core.RemoveManagedActivities([c_BookedActivityId]);
            core.IsProjectScenarioUpdated = false;

            core.ReviseTrackers();

            gantt.SelectedActivityIds.ShouldBe(new[] { 3 });
            core.DisplaySettingsViewModel.GanttChartShowConnections.Order().ToArray().ShouldBe(new[] { c_BookedActivityId, 3 }, "a revision went to the persisted filter");
            core.IsProjectScenarioUpdated.ShouldBeFalse("a revision was taken for an edit by the user");
        }

        [Fact]
        public void UpdateManagedActivities_Given_AutomaticCompilation_Then_TheTrackersAreRevisedBeforeTheCompilationIsRequested()
        {
            using GatedCoreViewModel core = CreateLoaded();
            core.AutoCompile = true;
            using ResourceActivitySelectorViewModel day = BookedDay(core);
            var raises = new List<string>();

            ((INotifyPropertyChanged)core).PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(ICoreViewModel.TrackersRevision) or nameof(CoreViewModel.CompilationRequestRevision))
                {
                    raises.Add(e.PropertyName);
                }
            };

            core.UpdateManagedActivities([new UpdateDependentActivityModel { Id = 3, IsNameEdited = true, Name = @"Renamed" }]);

            raises.ShouldBe([nameof(ICoreViewModel.TrackersRevision), nameof(CoreViewModel.CompilationRequestRevision)]);
            NameOf(day, 3).ShouldBe(@"Renamed");
        }

        #endregion

        #region The first value

        [Fact]
        public void Ctor_Given_ARevisionBeforeIt_Then_TheSelectorDoesNotReviseUntilTheNext()
        {
            // The constructor reads the activities. So the value that a chain gives when it subscribes has nothing new to say, and
            // a plan with bookings makes thousands of these selectors. A revision takes away a booking of an activity that the
            // plan does not have, and so that booking shows that no revision ran.
            using GatedCoreViewModel core = CreateLoaded();
            core.ReviseTrackers();

            using ResourceActivitySelectorViewModel day = BookedDay(core, c_UnknownActivityId);

            day.SelectedResourceActivityIds.ToArray().ShouldBe(new[] { c_UnknownActivityId }, "a revision ran for a request from before the selector");

            core.ReviseTrackers();

            day.SelectedResourceActivityIds.ShouldBeEmpty("the first request after the selector was made gave no revision");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Ctor_Given_ARequestWhileItReadsTheActivities_Then_TheSelectorRevises(bool acrossARollover)
        {
            // The constructor reads the counter before the activities (ARCHITECTURE section 9). A request that lands after it has
            // read them, and before its chain subscribes, must not be lost.
            using GatedCoreViewModel core = CreateLoaded();

            if (acrossARollover)
            {
                RevisionCounters.SetTrackersRevision(core, int.MaxValue);
            }

            int added = 0;
            ICoreViewModel proxy = CoreThatActsAfterARead.Over(
                core,
                () =>
                {
                    added = core.AddManagedActivity(c_ActivityCount + 1);
                    core.ReviseTrackers();
                });

            using ResourceActivitySelectorViewModel day = BookedDay(proxy);

            added.ShouldNotBe(0, "precondition: the request landed");
            Targets(day).ShouldContain(added, "the selector lost a request that landed while it read the activities");
        }

        [Fact]
        public void Ctor_Given_ARequestWhileTheSelectorOfTheGanttChartReadsTheActivities_Then_ItRevises()
        {
            using GatedCoreViewModel core = CreateLoaded();
            int added = 0;
            ICoreViewModel proxy = CoreThatActsAfterARead.Over(
                core,
                () =>
                {
                    added = core.AddManagedActivity(c_ActivityCount + 1);
                    core.ReviseTrackers();
                });

            using var gantt = new GanttActivitySelectorViewModel(proxy);

            added.ShouldNotBe(0, "precondition: the request landed");
            Targets(gantt).ShouldContain(added, "the selector lost a request that landed while it read the activities");
        }

        #endregion

        #region A load

        [Fact]
        public async Task ReviseTrackers_Given_ALoadPartWay_Then_TheBookingsStay()
        {
            // A load empties the plan and fills it again, under the lock of the core. A revision that sees the empty plan takes
            // away each booking of each selector, and the next edit of a resource saves that. A command can ask for a revision on
            // a thread of its own at any time: the compile command does.
            using GatedCoreViewModel core = CreateLoaded();
            using ResourceActivitySelectorViewModel day = BookedDay(core);
            ProjectScenarioModel plan = CoreViewModelFixture.CreateProjectScenario(c_ActivityCount);

            using var inTheLoad = new ManualResetEventSlim();
            using var carryOn = new ManualResetEventSlim();
            int held = 0;

            ((INotifyPropertyChanged)core).PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ICoreViewModel.ResourceSettings)
                    && core.RawActivities.Count == 0
                    && Interlocked.Exchange(ref held, 1) == 0)
                {
                    inTheLoad.Set();
                    carryOn.Wait(s_Patience);
                }
            };

            Task load = Task.Run(() => core.ProcessProjectScenario(plan, Guid.NewGuid(), @"Next"));
            Task revise;

            try
            {
                inTheLoad.Wait(s_Patience).ShouldBeTrue("precondition: the load got to the point where the plan is empty");
                revise = Task.Run(core.ReviseTrackers);

                // Time for a revision that does not wait for the load to do its harm.
                await Task.WhenAny(revise, Task.Delay(TimeSpan.FromMilliseconds(250)));
            }
            finally
            {
                carryOn.Set();
            }

            await load.WaitAsync(s_Patience);
            await revise.WaitAsync(s_Patience);

            day.SelectedResourceActivityIds.ToArray().ShouldBe(new[] { c_BookedActivityId }, "a revision took away a booking part way through a load");
            Targets(day).Length.ShouldBe(c_ActivityCount);
        }

        [Fact]
        public void ProcessProjectScenario_Given_ALoad_Then_TheTrackersAreRevisedOnceWithTheActivitiesInPlace()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            List<int> activitiesAtTheRaise = RecordRaises(core, nameof(ICoreViewModel.TrackersRevision), () => core.RawActivities.Count);

            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");

            activitiesAtTheRaise.ShouldHaveSingleItem().ShouldBe(c_ActivityCount);
        }

        [Fact]
        public void ResetProjectScenario_Given_ABooking_Then_TheTrackersAreNotRevised()
        {
            // A reset empties the plan. A revision would take away each booking.
            using GatedCoreViewModel core = CreateLoaded();
            using ResourceActivitySelectorViewModel day = BookedDay(core);
            List<int> raises = RecordRaises(core, nameof(ICoreViewModel.TrackersRevision), () => core.TrackersRevision);

            core.ResetProjectScenario();

            raises.ShouldBeEmpty();
            day.SelectedResourceActivityIds.ToArray().ShouldBe(new[] { c_BookedActivityId });
        }

        #endregion

        #region An edit

        [Fact]
        public void Edit_Given_AutomaticCompilation_Then_TheTrackersAreRevisedBeforeTheCompilationIsRequested()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateStartedWithAutoCompile(main, pool);
            using ResourceActivitySelectorViewModel day = BookedDay(core);
            var raises = new List<string>();

            ((INotifyPropertyChanged)core).PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(ICoreViewModel.TrackersRevision)
                    or nameof(CoreViewModel.CompilationRequestRevision)
                    or nameof(ICoreViewModel.GraphCompilation))
                {
                    raises.Add(e.PropertyName);
                }
            };

            Rename(core.RawActivities.Single(x => x.Id == 3), @"Renamed");
            Drain(main, pool);

            raises.Take(3).ShouldBe(
                [nameof(ICoreViewModel.TrackersRevision), nameof(CoreViewModel.CompilationRequestRevision), nameof(ICoreViewModel.GraphCompilation)]);
            NameOf(day, 3).ShouldBe(@"Renamed");
        }

        [Fact]
        public void Edit_Given_ACompilationThatHasItBeforeTheDelivery_Then_TheNewNameReachesTheSelectors()
        {
            // The compilation of an earlier edit took the copy of the plan after this edit, and before the delivery of this edit.
            // The plan is not due a compilation, and the selectors have still not heard of the edit.
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateStartedWithAutoCompile(main, pool);
            using ResourceActivitySelectorViewModel day = BookedDay(core);

            Rename(core.RawActivities.Single(x => x.Id == 3), @"Renamed");
            core.RunCompile();
            var compilation = core.GraphCompilation;
            Drain(main, pool);

            NameOf(day, 3).ShouldBe(@"Renamed", "the day of the resource kept the name from before the edit");
            core.GraphCompilation.ShouldBeSameAs(compilation, "the plan was compiled again with nothing new");
        }

        [Fact]
        public void Edit_Given_TwoBeforeTheFirstDelivery_Then_OneRevisionSeesBoth()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateStartedWithAutoCompile(main, pool);
            using ResourceActivitySelectorViewModel day = BookedDay(core);
            List<int> raises = RecordRaises(core, nameof(ICoreViewModel.TrackersRevision), () => core.TrackersRevision);

            Rename(core.RawActivities.Single(x => x.Id == 3), @"Third");
            Rename(core.RawActivities.Single(x => x.Id == 4), @"Fourth");
            Drain(main, pool);

            raises.ShouldHaveSingleItem();
            NameOf(day, 3).ShouldBe(@"Third");
            NameOf(day, 4).ShouldBe(@"Fourth");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Edit_Given_OneAfterTheRevisionOfTheOther_Then_EachGivesARevision(bool acrossARollover)
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateStartedWithAutoCompile(main, pool);
            using ResourceActivitySelectorViewModel day = BookedDay(core);

            if (acrossARollover)
            {
                RevisionCounters.SetTrackersRevision(core, int.MaxValue);
            }

            Rename(core.RawActivities.Single(x => x.Id == 3), @"Third");
            Drain(main, pool);
            NameOf(day, 3).ShouldBe(@"Third");

            Rename(core.RawActivities.Single(x => x.Id == 4), @"Fourth");
            Drain(main, pool);
            NameOf(day, 4).ShouldBe(@"Fourth");
        }

        [Fact]
        public void UpdateManagedActivities_Given_ADeliveryAfterIt_Then_ItsOwnRevisionIsEnough()
        {
            // The update asks for a revision itself, and each activity that it changes gives a delivery after it.
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateStartedWithAutoCompile(main, pool);
            List<int> raises = RecordRaises(core, nameof(ICoreViewModel.TrackersRevision), () => core.TrackersRevision);

            core.UpdateManagedActivities([.. core.RawActivities.Select(x => new UpdateDependentActivityModel { Id = x.Id, IsNameEdited = true, Name = @"Renamed" })]);
            Drain(main, pool);

            raises.ShouldHaveSingleItem();
        }

        [Fact]
        public void AddManagedActivity_Given_TheRequestOfTheCommandBeforeTheDelivery_Then_OneRevision()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateStartedWithAutoCompile(main, pool);
            List<int> raises = RecordRaises(core, nameof(ICoreViewModel.TrackersRevision), () => core.TrackersRevision);

            // As the activities manager does it, on a thread of its own.
            core.AddManagedActivity(c_ActivityCount + 1);
            core.ReviseTrackers();
            core.RunAutoCompile();
            Drain(main, pool);

            raises.ShouldHaveSingleItem();
        }

        [Fact]
        public void Edit_Given_NoAutomaticCompilation_Then_TheTrackersAreNotRevised()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateStartedWithAutoCompile(main, pool);
            core.AutoCompile = false;
            using ResourceActivitySelectorViewModel day = BookedDay(core);
            List<int> raises = RecordRaises(core, nameof(ICoreViewModel.TrackersRevision), () => core.TrackersRevision);

            Rename(core.RawActivities.Single(x => x.Id == 3), @"Renamed");
            Drain(main, pool);

            raises.ShouldBeEmpty();
            NameOf(day, 3).ShouldBe(@"Activity 3", "with no compilation, the names wait for the compile command");
        }

        [Fact]
        public void Edit_Given_AutomaticCompilationSwitchedOnAfterAnEdit_Then_TheNextEditIsRevisedAndCompiled()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using GatedCoreViewModel core = CreateStartedWithAutoCompile(main, pool);
            core.AutoCompile = false;
            using ResourceActivitySelectorViewModel day = BookedDay(core);
            Rename(core.RawActivities.Single(x => x.Id == 3), @"First");
            Drain(main, pool);

            core.AutoCompile = true;
            Drain(main, pool);
            NameOf(day, 3).ShouldBe(@"Activity 3", "precondition: switching automatic compilation on does nothing by itself");
            var compilation = core.GraphCompilation;

            Rename(core.RawActivities.Single(x => x.Id == 4), @"Second");
            Drain(main, pool);

            NameOf(day, 3).ShouldBe(@"First");
            NameOf(day, 4).ShouldBe(@"Second");
            core.GraphCompilation.ShouldNotBeSameAs(compilation, "the edit was never compiled");
        }

        #endregion

        /// <summary>
        /// A core that acts one time, just after a selector has read the activities: that is the window between the read and
        /// the chain of the selector. Anything that watches the core through this watches the real core.
        /// </summary>
        public class CoreThatActsAfterARead
            : DispatchProxy
        {
            private GatedCoreViewModel? m_Core;
            private Action? m_Act;

            public interface INotifyingCore
                : ICoreViewModel, INotifyPropertyChanged
            {
            }

            public static ICoreViewModel Over(GatedCoreViewModel core, Action act)
            {
                INotifyingCore proxy = Create<INotifyingCore, CoreThatActsAfterARead>();
                var self = (CoreThatActsAfterARead)(object)proxy;
                self.m_Core = core;
                self.m_Act = act;
                return proxy;
            }

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                object? result = targetMethod!.Invoke(m_Core, args);

                if (targetMethod.Name == $@"get_{nameof(ICoreViewModel.RawActivities)}")
                {
                    Interlocked.Exchange(ref m_Act, null)?.Invoke();
                }

                return result;
            }
        }
    }
}
