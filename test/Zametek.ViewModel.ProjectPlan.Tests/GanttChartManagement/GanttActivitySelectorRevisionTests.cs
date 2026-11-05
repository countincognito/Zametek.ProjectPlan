using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A dropdown of the Gantt chart shows the lists of its selector, and a bound list can change only on the UI thread. A
    /// revision of the trackers reads the plan on the thread that asks for it, and the selector changes its lists on the UI
    /// thread after that. The revision used to change them on its own thread, and a read on the UI thread then met the change.
    /// </summary>
    public class GanttActivitySelectorRevisionTests
    {
        private static readonly TimeSpan s_Patience = TimeSpan.FromSeconds(10);

        private const int c_ActivityCount = 6;

        private static CoreViewModel CreateLoaded()
        {
            CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            return core;
        }

        private static int[] Targets(GanttActivitySelectorViewModel selector) =>
            [.. selector.TargetActivities.Select(x => x.Id).Order()];

        // The thread of each change of either list of the selector.
        private static List<int> RecordChanges(GanttActivitySelectorViewModel selector)
        {
            var threads = new List<int>();

            void Record(object? sender, NotifyCollectionChangedEventArgs e)
            {
                lock (threads)
                {
                    threads.Add(Environment.CurrentManagedThreadId);
                }
            }

            ((INotifyCollectionChanged)selector.TargetActivities).CollectionChanged += Record;
            selector.SelectedTargetActivities.CollectionChanged += Record;
            return threads;
        }

        // Commits a new name as the grid does. With no automatic compilation, nothing asks for a revision.
        private static void Rename(IManagedActivityViewModel activity, string name)
        {
            var editable = (IEditableObject)activity;
            editable.BeginEdit();
            activity.Name = name;
            editable.EndEdit();
        }

        [Fact]
        public async Task ReviseTrackers_Given_AnAddOnAnotherThread_Then_TheListsChangeOnlyOnTheUIThread()
        {
            // A command adds an activity and asks for a revision on a thread of its own.
            using CoreViewModel core = CreateLoaded();
            var ui = new QueueingUIDispatcher();
            using var selector = new GanttActivitySelectorViewModel(core, ui);
            List<int> threads = RecordChanges(selector);

            int added = await Task.Run(() =>
            {
                int id = core.AddManagedActivity(c_ActivityCount + 1);
                core.ReviseTrackers();
                return id;
            });

            threads.ShouldBeEmpty("a list of the selector changed on the thread of the revision");
            Targets(selector).ShouldNotContain(added, "the list changed before the UI thread ran");

            ui.RunAll();

            Targets(selector).ShouldContain(added);
            threads.ShouldNotBeEmpty("precondition: the list changed");
            threads.ShouldAllBe(thread => thread == Environment.CurrentManagedThreadId, "a list changed on a thread that is not the UI thread");
        }

        [Fact]
        public async Task ReviseTrackers_Given_ARemovalOfASelectedActivityOnAnotherThread_Then_TheSelectionChangesOnlyOnTheUIThread()
        {
            using CoreViewModel core = CreateLoaded();
            core.DisplaySettingsViewModel.SetValues(new ProjectScenarioDisplaySettingsModel { GanttChartShowConnections = [2, 3] });
            var ui = new QueueingUIDispatcher();
            using var selector = new GanttActivitySelectorViewModel(core, ui);
            selector.SelectedActivityIds.Order().ToArray().ShouldBe(new[] { 2, 3 }, "precondition: the selector has the filter");
            List<int> threads = RecordChanges(selector);
            core.RemoveManagedActivities([2]);
            core.IsProjectScenarioUpdated = false;

            await Task.Run(core.ReviseTrackers);

            threads.ShouldBeEmpty("a list of the selector changed on the thread of the revision");

            ui.RunAll();

            selector.SelectedActivityIds.ShouldBe(new[] { 3 });
            Targets(selector).ShouldNotContain(2);
            threads.ShouldAllBe(thread => thread == Environment.CurrentManagedThreadId, "a list changed on a thread that is not the UI thread");
            core.DisplaySettingsViewModel.GanttChartShowConnections.Order().ToArray().ShouldBe(new[] { 2, 3 }, "a revision went to the persisted filter");
            core.IsProjectScenarioUpdated.ShouldBeFalse("a revision was taken for an edit by the user");
        }

        [Fact]
        public void ReviseTrackers_Given_TwoBeforeTheUIThreadRuns_Then_OnlyTheLastIsApplied()
        {
            // Each revision posts its work. The work reads the activities of the last revision when it runs, and so the work of the
            // first revision does not apply an older list.
            using CoreViewModel core = CreateLoaded();
            var ui = new QueueingUIDispatcher();
            using var selector = new GanttActivitySelectorViewModel(core, ui);
            ISelectableActivityViewModel third = selector.TargetActivities.Single(x => x.Id == 3);
            var names = new List<string>();
            ((INotifyPropertyChanged)third).PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ISelectableActivityViewModel.Name))
                {
                    names.Add(third.Name);
                }
            };
            IManagedActivityViewModel activity = core.RawActivities.Single(x => x.Id == 3);

            Rename(activity, @"First");
            core.ReviseTrackers();
            Rename(activity, @"Second");
            core.ReviseTrackers();
            ui.Pending.ShouldBe(2, "precondition: each revision posted its work");

            ui.RunAll();

            names.ShouldBe([@"Second"]);
        }

        [Fact]
        public async Task ReviseTrackers_Given_ALoadPartWayWhenTheUIThreadRuns_Then_TheListsHaveThePlanOfTheRevision()
        {
            // The revision reads the plan under the lock of the core, which it holds. The UI thread can run while a load holds that
            // lock and the plan is empty part way. It must not read the plan then.
            using CoreViewModel core = CreateLoaded();
            var ui = new QueueingUIDispatcher();
            using var selector = new GanttActivitySelectorViewModel(core, ui);
            core.AddManagedActivity(c_ActivityCount + 1);
            core.ReviseTrackers();
            int[] plan = [.. core.RawActivities.Select(x => x.Id).Order()];

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

            Task load = Task.Run(() => core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Next"));
            int[] applied;

            try
            {
                inTheLoad.Wait(s_Patience).ShouldBeTrue("precondition: the load got to the point where the plan is empty");
                ui.RunAll();
                applied = Targets(selector);
            }
            finally
            {
                carryOn.Set();
            }

            await load.WaitAsync(s_Patience);

            applied.ShouldBe(plan, "the UI thread read the plan part way through a load");
        }

        [Fact]
        public void KillSubscriptions_Given_AChangeOnItsWay_Then_ItChangesNothing()
        {
            using CoreViewModel core = CreateLoaded();
            var ui = new QueueingUIDispatcher();
            using var selector = new GanttActivitySelectorViewModel(core, ui);
            int added = core.AddManagedActivity(c_ActivityCount + 1);
            core.ReviseTrackers();

            selector.KillSubscriptions();
            ui.RunAll();

            Targets(selector).ShouldNotContain(added);
        }

        // A dispatcher that holds what it is given until it is told to run it, as the UI thread does.
        private sealed class QueueingUIDispatcher
            : IUIDispatcher
        {
            private readonly Queue<Action> m_Actions = new();
            private readonly Lock m_Lock = new();

            public int Pending
            {
                get
                {
                    lock (m_Lock)
                    {
                        return m_Actions.Count;
                    }
                }
            }

            public Task InvokeAsync(Action action)
            {
                action();
                return Task.CompletedTask;
            }

            public void Defer(Action action)
            {
                lock (m_Lock)
                {
                    m_Actions.Enqueue(action);
                }
            }

            public void RunAll()
            {
                while (true)
                {
                    Action action;

                    lock (m_Lock)
                    {
                        if (m_Actions.Count == 0)
                        {
                            return;
                        }

                        action = m_Actions.Dequeue();
                    }

                    action();
                }
            }
        }
    }
}
