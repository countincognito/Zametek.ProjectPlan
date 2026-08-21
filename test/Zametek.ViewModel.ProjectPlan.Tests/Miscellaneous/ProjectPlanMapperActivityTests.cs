using Shouldly;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for the activity mappings, and specifically for the properties this
    /// application adds on top of the ones the graph library defines.
    ///
    /// <see cref="DependentActivity"/> derives from the library's
    /// <see cref="DependentActivity{T, TResourceId, TWorkStreamId}"/> and adds
    /// HasNoRisk, DisplayOrder, OverrideColor, ColorFormat and Trackers. The mapper
    /// declares a mapping for each of the two, so an upcast to the library type
    /// silently selects the mapping that knows nothing about the added properties
    /// and returns a model with all of them left at their defaults - no compiler
    /// warning, no exception, just a HasNoRisk that is always false by the time it
    /// reaches the metrics. That is exactly what happened, and it is why these tests
    /// map from the concrete activity rather than from a hand-built ActivityModel:
    /// a test that starts at ActivityModel cannot see the mapping at all.
    /// </summary>
    public class ProjectPlanMapperActivityTests
    {
        #region Helpers

        // Every value here is deliberately non-default, so a property that is
        // dropped in mapping shows up as a difference rather than coinciding
        // with the destination's default.
        private static DependentActivity MakeFullyPopulatedActivity() =>
            new DependentActivity(
                id: 42,
                displayOrder: 7,
                name: @"Activity Name",
                notes: @"Activity Notes",
                targetWorkStreams: [3, 4],
                targetResources: [5, 6],
                dependencies: [1, 2],
                planningDependencies: [8],
                resourceDependencies: [9],
                successors: [10],
                targetLogicalOperator: LogicalOperator.OR,
                allocatedToResources: [11, 12],
                canBeRemoved: true,
                hasNoCost: true,
                hasNoBilling: true,
                hasNoEffort: true,
                hasNoRisk: true,
                duration: 5,
                freeSlack: 2,
                earliestStartTime: 3,
                latestFinishTime: 20,
                minimumFreeSlack: 1,
                minimumEarliestStartTime: 2,
                maximumLatestFinishTime: 30,
                overrideColor: true,
                colorFormat: new ColorFormatModel { A = 1, R = 2, G = 3, B = 4 },
                trackers:
                [
                    new ActivityTrackerModel { Time = 1, ActivityId = 42, PercentageComplete = 50 },
                ]);

        private static object? ReadProperty(object source, string name) =>
            source.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(source);

        private static bool ValuesMatch(object? left, object? right)
        {
            if (left is null || right is null)
            {
                return left is null && right is null;
            }
            if (left is string || left is not IEnumerable leftSequence)
            {
                return Equals(left, right);
            }
            if (right is not IEnumerable rightSequence)
            {
                return false;
            }
            return leftSequence.Cast<object>().SequenceEqual(rightSequence.Cast<object>());
        }

        #endregion

        #region ToActivityModel

        [Fact]
        public void ToActivityModel_Given_DependentActivity_Then_CarriesTheApplicationsOwnProperties()
        {
            var mapper = new ProjectPlanMapper();
            DependentActivity activity = MakeFullyPopulatedActivity();

            ActivityModel model = mapper.ToActivityModel(activity);

            // The five properties the library's activity does not have. HasNoRisk is
            // the one the risk metrics read.
            model.HasNoRisk.ShouldBeTrue();
            model.DisplayOrder.ShouldBe(7);
            model.OverrideColor.ShouldBeTrue();
            model.ColorFormat.ShouldBe(new ColorFormatModel { A = 1, R = 2, G = 3, B = 4 });
            model.Trackers.ShouldHaveSingleItem().PercentageComplete.ShouldBe(50);
        }

        [Fact]
        public void ToActivityModel_Given_DependentActivity_Then_CarriesTheLibrarysProperties()
        {
            var mapper = new ProjectPlanMapper();
            DependentActivity activity = MakeFullyPopulatedActivity();

            ActivityModel model = mapper.ToActivityModel(activity);

            model.Id.ShouldBe(42);
            model.Name.ShouldBe(@"Activity Name");
            model.Notes.ShouldBe(@"Activity Notes");
            model.TargetWorkStreams.ShouldBe([3, 4], ignoreOrder: true);
            model.TargetResources.ShouldBe([5, 6], ignoreOrder: true);
            model.TargetResourceOperator.ShouldBe(LogicalOperator.OR);
            model.AllocatedToResources.ShouldBe([11, 12], ignoreOrder: true);
            model.CanBeRemoved.ShouldBeTrue();
            model.HasNoCost.ShouldBeTrue();
            model.HasNoBilling.ShouldBeTrue();
            model.HasNoEffort.ShouldBeTrue();
            model.Duration.ShouldBe(5);
            model.FreeSlack.ShouldBe(2);
            model.EarliestStartTime.ShouldBe(3);
            model.LatestFinishTime.ShouldBe(20);
            model.MinimumFreeSlack.ShouldBe(1);
            model.MinimumEarliestStartTime.ShouldBe(2);
            model.MaximumLatestFinishTime.ShouldBe(30);
        }

        /// <summary>
        /// The catch-all: every property of the model that the activity also has, by
        /// name, must arrive with the activity's value. A property added to both in
        /// future but missed by the mapping fails here without anyone remembering to
        /// extend the two tests above.
        /// </summary>
        [Fact]
        public void ToActivityModel_Given_DependentActivity_Then_EveryCommonPropertyIsCarried()
        {
            var mapper = new ProjectPlanMapper();
            DependentActivity activity = MakeFullyPopulatedActivity();

            ActivityModel model = mapper.ToActivityModel(activity);

            IEnumerable<string> commonProperties = typeof(ActivityModel)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(x => x.Name)
                .Where(name => typeof(DependentActivity).GetProperty(name, BindingFlags.Public | BindingFlags.Instance) is not null);

            // Guards the guard: if the reflection above stops finding anything, the
            // test would pass vacuously.
            commonProperties.Count().ShouldBeGreaterThan(15);

            List<string> dropped =
                [.. commonProperties.Where(name => !ValuesMatch(ReadProperty(activity, name), ReadProperty(model, name)))];

            dropped.ShouldBeEmpty(
                $@"these properties did not survive the mapping: {string.Join(@", ", dropped)}");
        }

        /// <summary>
        /// The upcast that caused all this is now a compile error rather than a silent
        /// loss, because the mapper no longer offers a mapping from the library's base
        /// activity type. This test asserts that absence, so that re-declaring the
        /// overload - which would make the trap available again - has to be a deliberate
        /// decision taken against a failing test rather than an easy way to make some
        /// other call site compile.
        /// </summary>
        [Fact]
        public void ToActivityModel_Given_TheLibrarysBaseActivityType_Then_ThereIsNoMappingAtAll()
        {
            IEnumerable<MethodInfo> baseActivityMappings = typeof(ProjectPlanMapper)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(x => x.GetParameters().Length == 1
                    && x.GetParameters()[0].ParameterType == typeof(Activity<int, int, int>));

            baseActivityMappings.ShouldBeEmpty(
                @"a mapping from Activity<int, int, int> cannot see HasNoRisk, DisplayOrder, OverrideColor, ColorFormat or Trackers, and would return them at their defaults");

            // The mapping that does exist takes the activity this application actually
            // uses, so nothing has to be upcast to reach it.
            typeof(ProjectPlanMapper)
                .GetMethod(nameof(ProjectPlanMapper.ToActivityModel), [typeof(DependentActivity)])
                .ShouldNotBeNull();
        }

        #endregion

        #region Independence of the mapped result

        // The mapper deep clones, so nothing it returns may share a mutable collection
        // with what it was given. These are the sites where the two sides have the same
        // property type, which is where an assignment would otherwise be substituted for
        // a copy.

        [Fact]
        public void ToActivityModel_Given_DependentActivity_Then_TheModelSharesNothingWithIt()
        {
            var mapper = new ProjectPlanMapper();
            DependentActivity activity = MakeFullyPopulatedActivity();

            ActivityModel model = mapper.ToActivityModel(activity);

            model.Trackers.ShouldNotBeSameAs(activity.Trackers);
            model.ColorFormat.ShouldNotBeSameAs(activity.ColorFormat);

            model.Trackers.Clear();
            activity.Trackers.ShouldHaveSingleItem();
        }

        [Fact]
        public void ToDependentActivity_Given_ActivityModel_Then_TheActivitySharesNothingWithIt()
        {
            var mapper = new ProjectPlanMapper();
            var model = new ActivityModel
            {
                Id = 1,
                Duration = 3,
                TargetResources = [1, 2],
                Trackers = [new ActivityTrackerModel { Time = 0, ActivityId = 1, PercentageComplete = 10 }],
                ColorFormat = new ColorFormatModel { A = 1, R = 2, G = 3, B = 4 },
            };

            DependentActivity activity = mapper.ToDependentActivity(model);

            activity.Trackers.ShouldNotBeSameAs(model.Trackers);
            activity.ColorFormat.ShouldNotBeSameAs(model.ColorFormat);

            // A DependentActivity is a live, mutable object; a model it was built from
            // must not move when it does.
            activity.Trackers.Clear();
            activity.TargetResources.Clear();
            model.Trackers.ShouldHaveSingleItem();
            model.TargetResources.ShouldBe([1, 2], ignoreOrder: true);
        }

        #endregion

        #region ToDependentActivityModel

        [Fact]
        public void ToDependentActivityModel_Given_DependentActivity_Then_CarriesTheFlagsAndTheDependencies()
        {
            var mapper = new ProjectPlanMapper();
            DependentActivity activity = MakeFullyPopulatedActivity();

            DependentActivityModel model = mapper.ToDependentActivityModel(activity);

            model.Activity.HasNoRisk.ShouldBeTrue();
            model.Activity.HasNoCost.ShouldBeTrue();
            model.Activity.HasNoBilling.ShouldBeTrue();
            model.Activity.HasNoEffort.ShouldBeTrue();

            model.Dependencies.ShouldBe([1, 2], ignoreOrder: true);
            model.PlanningDependencies.ShouldBe([8], ignoreOrder: true);
            model.ResourceDependencies.ShouldBe([9], ignoreOrder: true);
            model.Successors.ShouldBe([10], ignoreOrder: true);

            model.Activity.Trackers.ShouldHaveSingleItem().PercentageComplete.ShouldBe(50);
        }

        /// <summary>
        /// Same-typed collections are mapped across by assignment rather than by copy,
        /// so a model built from an activity would share the activity's own tracker
        /// list unless the mapping takes a copy. Sharing it is not a theoretical
        /// problem: this mapping used to clear the list it had just been handed, which
        /// emptied the activity as well as the model.
        /// </summary>
        [Fact]
        public void ToDependentActivityModel_Given_DependentActivity_Then_DoesNotShareTheTrackerListWithIt()
        {
            var mapper = new ProjectPlanMapper();
            DependentActivity activity = MakeFullyPopulatedActivity();

            DependentActivityModel model = mapper.ToDependentActivityModel(activity);

            activity.Trackers.ShouldHaveSingleItem().PercentageComplete.ShouldBe(50);
            model.Activity.Trackers.ShouldNotBeSameAs(activity.Trackers);

            model.Activity.Trackers.Clear();
            activity.Trackers.ShouldHaveSingleItem();
        }

        [Fact]
        public void DependentActivity_RoundTrippedThroughItsModel_Then_KeepsAllFourFlags()
        {
            var mapper = new ProjectPlanMapper();
            DependentActivity activity = MakeFullyPopulatedActivity();

            DependentActivity roundTripped =
                mapper.ToDependentActivity(mapper.ToDependentActivityModel(activity));

            roundTripped.HasNoRisk.ShouldBe(activity.HasNoRisk);
            roundTripped.HasNoCost.ShouldBe(activity.HasNoCost);
            roundTripped.HasNoBilling.ShouldBe(activity.HasNoBilling);
            roundTripped.HasNoEffort.ShouldBe(activity.HasNoEffort);

            roundTripped.DisplayOrder.ShouldBe(activity.DisplayOrder);
            roundTripped.OverrideColor.ShouldBe(activity.OverrideColor);
            roundTripped.ColorFormat.ShouldBe(activity.ColorFormat);
            roundTripped.Dependencies.ShouldBe(activity.Dependencies, ignoreOrder: true);
        }

        [Theory]
        [InlineData(false, false, false, false)]
        [InlineData(true, false, false, false)]
        [InlineData(false, true, false, false)]
        [InlineData(false, false, true, false)]
        [InlineData(false, false, false, true)]
        [InlineData(true, true, true, true)]
        public void DependentActivity_RoundTrippedThroughItsModel_Then_EachFlagIsIndependent(
            bool hasNoCost,
            bool hasNoBilling,
            bool hasNoEffort,
            bool hasNoRisk)
        {
            var mapper = new ProjectPlanMapper();

            var activity = new DependentActivity(
                id: 1,
                displayOrder: 1,
                name: @"A",
                notes: string.Empty,
                targetWorkStreams: [],
                targetResources: [],
                dependencies: [],
                planningDependencies: [],
                resourceDependencies: [],
                successors: [],
                targetLogicalOperator: LogicalOperator.AND,
                allocatedToResources: [],
                canBeRemoved: false,
                hasNoCost: hasNoCost,
                hasNoBilling: hasNoBilling,
                hasNoEffort: hasNoEffort,
                hasNoRisk: hasNoRisk,
                duration: 1,
                freeSlack: null,
                earliestStartTime: null,
                latestFinishTime: null,
                minimumFreeSlack: null,
                minimumEarliestStartTime: null,
                maximumLatestFinishTime: null,
                overrideColor: false,
                colorFormat: new ColorFormatModel(),
                trackers: []);

            ActivityModel model = mapper.ToActivityModel(activity);

            model.HasNoCost.ShouldBe(hasNoCost);
            model.HasNoBilling.ShouldBe(hasNoBilling);
            model.HasNoEffort.ShouldBe(hasNoEffort);
            model.HasNoRisk.ShouldBe(hasNoRisk);
        }

        #endregion

        #region CloneObject

        /// <summary>
        /// The metric builders clone their inputs before touching them, so a clone
        /// that dropped a flag would be the same bug one layer down.
        /// </summary>
        [Fact]
        public void CloneObject_Given_DependentActivity_Then_KeepsAllFourFlags()
        {
            DependentActivity activity = MakeFullyPopulatedActivity();

            var clone = (DependentActivity)activity.CloneObject();

            clone.HasNoRisk.ShouldBe(activity.HasNoRisk);
            clone.HasNoCost.ShouldBe(activity.HasNoCost);
            clone.HasNoBilling.ShouldBe(activity.HasNoBilling);
            clone.HasNoEffort.ShouldBe(activity.HasNoEffort);
            clone.DisplayOrder.ShouldBe(activity.DisplayOrder);
            clone.Duration.ShouldBe(activity.Duration);
            clone.Dependencies.ShouldBe(activity.Dependencies, ignoreOrder: true);
            clone.PlanningDependencies.ShouldBe(activity.PlanningDependencies, ignoreOrder: true);
            clone.ResourceDependencies.ShouldBe(activity.ResourceDependencies, ignoreOrder: true);
        }

        #endregion
    }
}
