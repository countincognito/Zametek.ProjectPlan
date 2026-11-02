using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using static Zametek.ViewModel.ProjectPlan.Tests.SequencerScopes;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The selector of the Gantt chart follows the filter of connections that a scenario has saved. When a filter arrives, the
    /// display settings are the authority. When the user edits the selection, the selection is the authority.
    /// </summary>
    public class GanttActivitySelectorViewModelTests
    {
        private const int c_ActivityCount = 12;

        private static CoreViewModel CreateLoaded()
        {
            CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            return core;
        }

        private static ProjectScenarioDisplaySettingsModel Filter(params int[] connections) =>
            new() { GanttChartShowConnections = [.. connections] };

        private static int[] Selected(GanttActivitySelectorViewModel selector) =>
            [.. selector.SelectedTargetActivities.Select(x => x.Id).Order()];

        [Fact]
        public void SetValues_Given_ANewFilterWhileTheHandlerRuns_Then_TheNewFilterIsApplied()
        {
            // A load on another thread puts its filter in place while the UI thread applies the filter before it. Here it occurs
            // on one thread, from inside the handler, so that it lands in that window each time.
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModel core = CreateLoaded();
            using var selector = new GanttActivitySelectorViewModel(core);
            selector.StartSubscriptions();
            Drain(main, pool);

            bool arrived = false;
            selector.SelectedTargetActivities.CollectionChanged += (_, _) =>
            {
                if (!arrived)
                {
                    arrived = true;
                    core.DisplaySettingsViewModel.SetValues(Filter(2));
                }
            };

            core.DisplaySettingsViewModel.SetValues(Filter(1));
            Drain(main, pool);

            arrived.ShouldBeTrue("precondition: the handler changed the selection");
            Selected(selector).ShouldBe(new[] { 2 }, "the selector kept the filter from before");
        }

        [Fact]
        public void SetValues_Given_TwoFiltersOneAfterTheOther_Then_TheLastIsApplied()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModel core = CreateLoaded();
            using var selector = new GanttActivitySelectorViewModel(core);
            selector.StartSubscriptions();
            Drain(main, pool);

            core.DisplaySettingsViewModel.SetValues(Filter(1));
            Drain(main, pool);
            core.DisplaySettingsViewModel.SetValues(Filter(2));
            Drain(main, pool);

            Selected(selector).ShouldBe(new[] { 2 });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SetValues_Given_TwoFiltersBeforeTheHandlerRuns_Then_ItRunsOnceWithTheLast(bool acrossARollover)
        {
            // A load puts a filter in place three times, and the UI thread can be behind it. A run clears the selection and
            // fills it again, and the chart rebuilds for each step.
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModel core = CreateLoaded();
            using var selector = new GanttActivitySelectorViewModel(core);
            selector.StartSubscriptions();
            Drain(main, pool);

            if (acrossARollover)
            {
                RevisionCounters.SetGanttChartShowConnectionsRevision(core.DisplaySettingsViewModel, int.MaxValue - 1);
            }

            int runs = 0;
            selector.SelectedTargetActivities.CollectionChanged += (_, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Reset)
                {
                    runs++;
                }
            };

            core.DisplaySettingsViewModel.SetValues(Filter(1));
            core.DisplaySettingsViewModel.SetValues(Filter(2));
            Drain(main, pool);

            runs.ShouldBe(1);
            Selected(selector).ShouldBe(new[] { 2 });
        }

        [Fact]
        public void StartSubscriptions_Given_AFilterSinceTheConstructor_Then_ItIsApplied()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModel core = CreateLoaded();
            using var selector = new GanttActivitySelectorViewModel(core);

            core.DisplaySettingsViewModel.SetValues(Filter(3));
            Selected(selector).ShouldBeEmpty("precondition: a selector that is not started does not follow a filter");

            selector.StartSubscriptions();
            Drain(main, pool);

            Selected(selector).ShouldBe(new[] { 3 });
        }

        [Fact]
        public void Ctor_Given_AFilterInPlace_Then_ASelectorThatIsNotStartedHasIt()
        {
            using CoreViewModel core = CreateLoaded();
            core.DisplaySettingsViewModel.SetValues(Filter(3));

            using var selector = new GanttActivitySelectorViewModel(core);

            Selected(selector).ShouldBe(new[] { 3 });
        }

        [Fact]
        public void SetValues_Given_AnIdThatThePlanDoesNotHave_Then_ThePersistedFilterKeepsIt()
        {
            // A filter that arrives is not an edit by the user. The selection cannot hold the unknown id, and it must not go
            // back into the persisted filter, or mark the scenario as updated.
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModel core = CreateLoaded();
            using var selector = new GanttActivitySelectorViewModel(core);
            selector.StartSubscriptions();
            Drain(main, pool);
            core.IsProjectScenarioUpdated.ShouldBeFalse("precondition: nothing has changed since the load");

            core.DisplaySettingsViewModel.SetValues(Filter(1, 99));
            Drain(main, pool);

            Selected(selector).ShouldBe(new[] { 1 });
            core.DisplaySettingsViewModel.GanttChartShowConnections.ToArray().ShouldBe(new[] { 1, 99 }, "a filter that arrived was written back");
            core.IsProjectScenarioUpdated.ShouldBeFalse("a filter that arrived was taken for an edit by the user");
        }

        [Fact]
        public void SetValues_Given_AnActivityThatTheSelectorHasNotSeen_Then_ItIsSelected()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModel core = CreateLoaded();
            using var selector = new GanttActivitySelectorViewModel(core);
            selector.StartSubscriptions();
            Drain(main, pool);

            int added = core.AddManagedActivity(c_ActivityCount + 1);
            selector.TargetActivities.Select(x => x.Id).ShouldNotContain(added, "precondition: nothing told the selector");

            core.DisplaySettingsViewModel.SetValues(Filter(added));
            Drain(main, pool);

            Selected(selector).ShouldBe(new[] { added });
        }

        [Fact]
        public void SetValues_Given_TheSameFilterAfterTheActivityArrives_Then_ItIsApplied()
        {
            // A load does this: it puts the filter in place, it adds the activities, and it puts the same filter in place again.
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModel core = CreateLoaded();
            using var selector = new GanttActivitySelectorViewModel(core);
            selector.StartSubscriptions();
            Drain(main, pool);

            int next = c_ActivityCount + 1;
            core.DisplaySettingsViewModel.SetValues(Filter(next));
            Drain(main, pool);
            Selected(selector).ShouldBeEmpty("precondition: the plan does not have the activity yet");

            core.AddManagedActivity(next).ShouldBe(next, "precondition: the activity has the id of the filter");
            core.DisplaySettingsViewModel.SetValues(Filter(next));
            Drain(main, pool);

            Selected(selector).ShouldBe(new[] { next });
        }

        [Fact]
        public void SelectedTargetActivities_Given_AnEditByTheUserAfterAFilter_Then_ItGoesToThePersistedFilter()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModel core = CreateLoaded();
            using var selector = new GanttActivitySelectorViewModel(core);
            selector.StartSubscriptions();
            Drain(main, pool);
            core.DisplaySettingsViewModel.SetValues(Filter(1));
            Drain(main, pool);
            core.IsProjectScenarioUpdated.ShouldBeFalse("precondition: nothing has changed since the load");

            selector.SelectedTargetActivities.Add(selector.TargetActivities.First(x => x.Id == 2));

            core.DisplaySettingsViewModel.GanttChartShowConnections.Order().ShouldBe(new[] { 1, 2 });
            core.IsProjectScenarioUpdated.ShouldBeTrue("an edit by the user did not mark the scenario as updated");
        }

        [Fact]
        public void SelectedTargetActivities_Given_AnEditByTheUser_Then_AReadOfTheFilterBeforeItKeepsWhatItHad()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModel core = CreateLoaded();
            using var selector = new GanttActivitySelectorViewModel(core);
            selector.StartSubscriptions();
            Drain(main, pool);
            core.DisplaySettingsViewModel.SetValues(Filter(1));
            Drain(main, pool);

            IEnumerable<int> before = core.DisplaySettingsViewModel.GanttChartShowConnections;

            selector.SelectedTargetActivities.Add(selector.TargetActivities.First(x => x.Id == 2));

            before.ToArray().ShouldBe(new[] { 1 });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ProcessProjectScenario_Given_AFilterInTheScenario_Then_TheSelectorHasIt(bool acrossARollover)
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.StartSubscriptions();
            using var selector = new GanttActivitySelectorViewModel(core);
            selector.StartSubscriptions();
            Drain(main, pool);

            if (acrossARollover)
            {
                // A load makes more than one change, and so the rollover lands inside the first load.
                RevisionCounters.SetGanttChartShowConnectionsRevision(core.DisplaySettingsViewModel, int.MaxValue);
            }

            ProjectScenarioModel plan = CoreViewModelFixture.CreateProjectScenario(c_ActivityCount);

            core.ProcessProjectScenario(plan with { DisplaySettings = Filter(2, 3) }, Guid.NewGuid(), @"First");
            Drain(main, pool);
            Selected(selector).ShouldBe(new[] { 2, 3 });

            core.ProcessProjectScenario(plan with { DisplaySettings = Filter(5) }, Guid.NewGuid(), @"Second");
            Drain(main, pool);
            Selected(selector).ShouldBe(new[] { 5 });

            core.ProcessProjectScenario(plan, Guid.NewGuid(), @"Third");
            Drain(main, pool);
            Selected(selector).ShouldBeEmpty();
        }
    }
}
