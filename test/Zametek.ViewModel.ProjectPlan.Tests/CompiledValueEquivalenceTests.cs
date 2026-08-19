using Shouldly;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Pins the equivalence the snapshot/compile/publish change rests on.
    ///
    /// A compilation used to be run over the live activity view models: they were the
    /// compiler's graph nodes, so it read its inputs from them and wrote its results
    /// straight back into them. It now compiles an independent copy of the plan and
    /// publishes the results back afterwards (ARCHITECTURE section 7 rule 11). The
    /// claim that makes that safe is that the two are indistinguishable from the
    /// outside - every value the compiler would have written directly, the publish
    /// route writes too, with the same value.
    ///
    /// Each test below runs both routes over the same activities and compares every
    /// public property of <see cref="IDependentActivity"/> and its parent interfaces.
    /// The properties are found by reflection rather than listed, so a property added
    /// to any of those interfaces later is compared automatically instead of being
    /// silently skipped.
    ///
    /// Running the two routes in sequence over the same objects is sound because
    /// everything a compilation writes is also something it resets before it reads:
    /// the times and slack are cleared by the critical path pass, the allocations and
    /// resource dependencies by ResetResourceState, and the successors are recomputed
    /// wholesale. So the second route cannot see anything the first one left behind.
    /// </summary>
    public class CompiledValueEquivalenceTests
    {
        private const string c_SampleProjectFileName = @"sample_v0_6_1.zpp";

        /// <summary>
        /// The properties every comparison covers: everything declared on
        /// IDependentActivity and on each interface it inherits, which is the whole
        /// surface an activity presents to the compiler and to the application.
        /// </summary>
        private static readonly IReadOnlyList<PropertyInfo> s_ComparedProperties =
            [.. new[] { typeof(IDependentActivity) }
                .Concat(typeof(IDependentActivity).GetInterfaces())
                .SelectMany(x => x.GetProperties())
                .DistinctBy(x => x.Name)
                .OrderBy(x => x.Name, StringComparer.Ordinal)];

        [Fact]
        public void ComparedProperties_Then_CoverTheWholeInterface()
        {
            // A guard on the comparison itself: if this ever collapses to a handful of
            // properties - because the reflection stopped finding the parent interfaces,
            // say - every test below would still pass while checking almost nothing.
            string[] names = [.. s_ComparedProperties.Select(x => x.Name)];

            names.ShouldContain(nameof(IDependentActivity.Id));
            names.ShouldContain(nameof(IDependentActivity.CanBeRemoved));
            names.ShouldContain(nameof(IDependentActivity.Duration));
            names.ShouldContain(nameof(IDependentActivity.EarliestStartTime));
            names.ShouldContain(nameof(IDependentActivity.LatestFinishTime));
            names.ShouldContain(nameof(IDependentActivity.FreeSlack));
            names.ShouldContain(nameof(IDependentActivity.TotalSlack));
            names.ShouldContain(nameof(IDependentActivity.InterferingSlack));
            names.ShouldContain(nameof(IDependentActivity.IsCritical));
            names.ShouldContain(nameof(IDependentActivity.IsDummy));
            names.ShouldContain(nameof(IDependentActivity.AllocatedToResources));
            names.ShouldContain(nameof(IDependentActivity.ResourceDependencies));
            names.ShouldContain(nameof(IDependentActivity.Successors));
            names.ShouldContain(nameof(IDependentActivity.Dependencies));
            names.ShouldContain(nameof(IDependentActivity.PlanningDependencies));
            names.ShouldContain(nameof(IDependentActivity.TargetResources));
            names.ShouldContain(nameof(IDependentActivity.TargetWorkStreams));
            names.ShouldContain(nameof(IDependentActivity.TargetResourceOperator));
            names.ShouldContain(nameof(IDependentActivity.Trackers));
            names.ShouldContain(nameof(IDependentActivity.ColorFormat));
            names.ShouldContain(nameof(IDependentActivity.DisplayOrder));

            // Twenty-odd at the time of writing; the floor only catches a collapse.
            s_ComparedProperties.Count.ShouldBeGreaterThanOrEqualTo(25);
        }

        [Fact]
        public async Task RealPlan_Given_IncrementalEdits_Then_PublishMatchesDirectCompilation()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            ProjectScenarioModel scenario = await CoreViewModelFixture.LoadProjectScenarioAsync(c_SampleProjectFileName);
            core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"Equivalence");

            core.RawActivities.Count.ShouldBe(46);
            core.HasCompilationErrors.ShouldBeFalse();

            // The comparison is only worth anything if there is something to compare, so
            // check the plan actually schedules: without this, a plan that compiled to
            // nothing at all would agree with itself on every property and pass.
            core.RawActivities.ShouldAllBe(x => x.EarliestStartTime != null && x.LatestFinishTime != null);
            core.RawActivities.Count(x => x.AllocatedToResources.Count > 0).ShouldBeGreaterThan(30);
            core.RawActivities.Count(x => x.Successors.Count > 0).ShouldBeGreaterThan(20);
            core.RawActivities.Count(x => x.ResourceDependencies.Count > 0).ShouldBeGreaterThan(10);
            core.RawActivities.Count(x => x.IsCritical).ShouldBeGreaterThan(0);

            // Each step changes an input a user can change, then both routes compile the
            // plan as it now stands. The edits accumulate, so later steps compile a plan
            // that carries every change before it.
            (string Description, Action<CoreViewModel> Apply)[] steps =
            [
                (@"as loaded", _ => { }),
                (@"a duration is lengthened", c => Activity(c, 3).Duration += 7),
                (@"a duration is shortened to zero, making the activity a dummy", c => Activity(c, 5).Duration = 0),
                (@"an activity is retargeted onto one resource", c => Edit(Activity(c, 7), a => a.ResourceSelector.SetSelectedTargetResources([1]))),
                (@"an activity is retargeted onto several resources", c => Edit(Activity(c, 9), a => a.ResourceSelector.SetSelectedTargetResources([1, 2, 3]))),
                (@"the target resource operator is changed", c => Activity(c, 9).TargetResourceOperator = LogicalOperator.AND),
                (@"an activity's targets are cleared", c => Edit(Activity(c, 11), a => a.ResourceSelector.SetSelectedTargetResources([]))),
                (@"a dependency is added", c => Activity(c, 13).DependenciesString = string.Join(@",", Activity(c, 13).Dependencies.Append(2).Distinct().Order())),
                (@"dependencies are removed", c => Activity(c, 15).DependenciesString = string.Empty),
                (@"a planning dependency is added", c => Activity(c, 17).PlanningDependenciesString = @"4"),
                (@"a minimum earliest start time is set", c => Activity(c, 19).MinimumEarliestStartTime = 25),
                (@"a maximum latest finish time is set", c => Activity(c, 21).MaximumLatestFinishTime = 400),
                (@"a minimum free slack is set", c => Activity(c, 23).MinimumFreeSlack = 5),
                (@"the cost, billing, effort and risk flags are turned on", c => Edit(Activity(c, 25), a => { a.HasNoCost = true; a.HasNoBilling = true; a.HasNoEffort = true; a.HasNoRisk = true; })),
                (@"a name, notes and colour are changed", c => Edit(Activity(c, 27), a => { a.Name = @"Renamed activity"; a.Notes = @"Edited notes"; a.OverrideColor = true; a.ColorFormat = ColorHelper.Red(); })),
                (@"a new activity is added and made to depend on an existing one", c =>
                {
                    c.AddManagedActivity(c.RawActivities.Max(x => x.DisplayOrder) + 1);
                    IManagedActivityViewModel added = c.RawActivities[^1];
                    added.Duration = 12;
                    added.DependenciesString = @"2";
                }),
                (@"a resource is made inactive", c => c.ResourceSettings = c.ResourceSettings with
                {
                    Resources = [.. c.ResourceSettings.Resources.Select(x => x.Id == 2 ? x with { IsInactive = true } : x)],
                }),
                (@"a resource is made an explicit target", c => c.ResourceSettings = c.ResourceSettings with
                {
                    Resources = [.. c.ResourceSettings.Resources.Select(x => x.Id == 4 ? x with { IsExplicitTarget = true } : x)],
                }),
                (@"the project start is moved", c => c.ProjectStart = c.ProjectStart.AddDays(14)),
            ];

            foreach ((string description, Action<CoreViewModel> apply) in steps)
            {
                apply(core);
                AssertRoutesAgree(core, description);
            }
        }

        [Fact]
        public async Task SyntheticPlan_Given_IncrementalEdits_Then_PublishMatchesDirectCompilation()
        {
            // The same comparison over a plan whose activities all compete for two
            // resources, so nearly every activity has to queue behind another and the
            // schedule is decided by resource contention rather than by the dependencies.
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(30), Guid.NewGuid(), @"Equivalence");

            await Task.CompletedTask;

            (string Description, Action<CoreViewModel> Apply)[] steps =
            [
                (@"as created", _ => { }),
                (@"contention is increased by lengthening several activities", c =>
                {
                    foreach (int id in new[] { 4, 8, 12, 16 })
                    {
                        Activity(c, id).Duration += 9;
                    }
                }),
                (@"a chain of activities is put onto the same single resource", c =>
                {
                    foreach (int id in new[] { 5, 6, 7, 8 })
                    {
                        Edit(Activity(c, id), a => a.ResourceSelector.SetSelectedTargetResources([1]));
                    }
                }),
                (@"resources are disabled altogether", c => c.ResourceSettings = c.ResourceSettings with { AreDisabled = true }),
                (@"resources are enabled again", c => c.ResourceSettings = c.ResourceSettings with { AreDisabled = false }),
                (@"a work stream is added and an activity put into it", c =>
                {
                    c.WorkStreamSettings = c.WorkStreamSettings with
                    {
                        WorkStreams = [new WorkStreamModel { Id = 1, Name = @"Phase 1", IsPhase = true }],
                    };
                    Edit(Activity(c, 2), a => a.WorkStreamSelector.SetSelectedTargetWorkStreams([1]));
                }),
            ];

            foreach ((string description, Action<CoreViewModel> apply) in steps)
            {
                apply(core);
                AssertRoutesAgree(core, description);
            }
        }

        #region Helpers

        private static IManagedActivityViewModel Activity(CoreViewModel core, int activityId) =>
            core.RawActivities.Single(x => x.Id == activityId);

        /// <summary>
        /// Applies a change the way the grid does, through a begin/end edit pair, which is
        /// what commits the selectors back onto the activity.
        /// </summary>
        private static void Edit(IManagedActivityViewModel activity, Action<IManagedActivityViewModel> change)
        {
            var editable = (System.ComponentModel.IEditableObject)activity;
            editable.BeginEdit();
            change(activity);
            editable.EndEdit();
        }

        /// <summary>
        /// Compiles the plan both ways and compares the activities afterwards.
        /// </summary>
        private static void AssertRoutesAgree(CoreViewModel core, string description)
        {
            // The route under test: a copy of the plan is compiled and the results are
            // published back to the activities.
            core.RunCompile();

            // The activities must be in the same order in the list as in the graph, because
            // the reference compilation below is built from the list while this one was
            // built from the graph, and the scheduling priority list resolves a tie in
            // favour of whichever activity it meets first. If these ever diverge, the
            // comparison would be between two different schedules rather than two routes.
            core.GraphCompilation.DependentActivities.Select(x => x.Id)
                .ShouldBe(core.RawActivities.Select(x => x.Id), $@"graph and list order diverged after {description}");

            bool hadCompilationErrors = core.HasCompilationErrors;
            Dictionary<int, Dictionary<string, object?>> published = Capture(core.RawActivities);

            // The reference route: the same activities handed to a compiler as its graph
            // nodes, exactly as they were before the change, so that it reads from them and
            // writes its results back into them.
            IGraphCompilation<int, int, int, IDependentActivity> direct = CompileDirectly(core);

            direct.CompilationErrors.Any().ShouldBe(hadCompilationErrors, $@"the two routes disagreed about whether the plan compiles after {description}");

            Dictionary<int, Dictionary<string, object?>> written = Capture(core.RawActivities);

            published.Keys.OrderBy(x => x).ShouldBe(written.Keys.OrderBy(x => x), $@"the two routes produced different activities after {description}");

            foreach ((int activityId, Dictionary<string, object?> publishedValues) in published.OrderBy(x => x.Key))
            {
                Dictionary<string, object?> writtenValues = written[activityId];

                foreach (PropertyInfo property in s_ComparedProperties)
                {
                    object? publishedValue = publishedValues[property.Name];
                    object? writtenValue = writtenValues[property.Name];

                    ValuesMatch(publishedValue, writtenValue).ShouldBeTrue(
                        $@"activity {activityId}.{property.Name} differed after {description}: publishing gave {Describe(publishedValue)}, compiling directly gave {Describe(writtenValue)}");
                }
            }
        }

        /// <summary>
        /// Compiles with the live activities as the graph nodes - what the application did
        /// before compilations were run over a copy. The resources and work streams are
        /// prepared exactly as <c>RunCompile</c> prepares them, so the only difference
        /// between this and the route under test is whether the compiler is given the
        /// activities themselves or copies of them.
        /// </summary>
        private static IGraphCompilation<int, int, int, IDependentActivity> CompileDirectly(CoreViewModel core)
        {
            var mapper = new ProjectPlanMapper();

            var availableResources = new List<IResource<int, int>>();

            if (!core.ResourceSettings.AreDisabled)
            {
                availableResources.AddRange(core.ResourceSettings.Resources.OrderBy(x => x.Id).Select(mapper.ToResource));
            }

            var workStreams = new List<IWorkStream<int>>();
            workStreams.AddRange(core.WorkStreamSettings.WorkStreams.Select(mapper.ToWorkStream));

            var compiler = new VertexGraphCompiler();

            foreach (IManagedActivityViewModel activity in core.RawActivities)
            {
                compiler.AddActivity(activity).ShouldBeTrue($@"activity {activity.Id} could not be added to the reference compiler");
            }

            return compiler.Compile(availableResources, workStreams, CancellationToken.None);
        }

        private static Dictionary<int, Dictionary<string, object?>> Capture(
            IEnumerable<IManagedActivityViewModel> activities)
        {
            var captured = new Dictionary<int, Dictionary<string, object?>>();

            foreach (IManagedActivityViewModel activity in activities)
            {
                var values = new Dictionary<string, object?>();

                foreach (PropertyInfo property in s_ComparedProperties)
                {
                    // Collections are copied as they are read, because the reference
                    // compilation is about to rewrite the very ones just captured.
                    values[property.Name] = Snapshot(property.GetValue(activity));
                }

                captured[activity.Id] = values;
            }

            return captured;
        }

        private static object? Snapshot(object? value)
        {
            return value switch
            {
                null => null,
                string text => text,
                IEnumerable items => items.Cast<object?>().ToList(),
                _ => value,
            };
        }

        private static bool ValuesMatch(object? left, object? right)
        {
            if (left is null || right is null)
            {
                return left is null && right is null;
            }

            if (left is List<object?> leftItems && right is List<object?> rightItems)
            {
                // Order is not part of the meaning of any of these collections - they are
                // sets on the activity - so they are compared as unordered.
                return leftItems.Count == rightItems.Count
                    && leftItems.All(x => rightItems.Any(y => Equals(x, y)))
                    && rightItems.All(x => leftItems.Any(y => Equals(x, y)));
            }

            return Equals(left, right);
        }

        private static string Describe(object? value)
        {
            return value switch
            {
                null => @"null",
                List<object?> items => $@"[{string.Join(@",", items.Select(Describe))}]",
                _ => value.ToString() ?? string.Empty,
            };
        }

        #endregion
    }
}
