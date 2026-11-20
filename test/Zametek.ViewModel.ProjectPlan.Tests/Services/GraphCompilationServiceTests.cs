using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for the vertex graph that the application draws from a compilation: that each node has the times of the
    /// compilation, and that the graph draws the dependencies that the resources gave.
    /// </summary>
    /// <remarks>
    /// The compilation of a plan schedules its resources. The schedule gives resource dependencies: when two activities
    /// follow each other on one resource, the second gets a dependency on the first. The vertex graph draws them as links.
    /// To do this, the build makes each resource dependency a dependency of the activity.
    /// <para>
    /// The build must not schedule the resources a second time after that. The dependencies that it added change the slack
    /// of the activities, and the scheduler takes the activities in the order of their slack. Thus, a second schedule can be
    /// a different one, with other times and another finish. That schedule is correct for the activities that the scheduler
    /// got, but it is not the schedule of the plan. The build did this from 0.9.3 to 0.11.0-beta.1, and the vertex graph of
    /// some plans then showed other times than the list of the activities and the arrow graph.
    /// </para>
    /// <para>
    /// Each test compiles a small plan with the compiler of the application, as the core does, and gives the result to the
    /// build. <see cref="ScheduleAgain"/> is what the build did before. A test uses it only to show that its plan can tell
    /// the two apart: a plan for which a second schedule is the same schedule proves nothing. These checks depend on how
    /// the scheduler of the library decides. If a later version of the library schedules these plans in the same way a
    /// second time, a check fails, and its test needs another plan.
    /// </para>
    /// </remarks>
    public class GraphCompilationServiceTests
    {
        #region Helpers

        private static GraphCompilationService CreateService() =>
            new(new ProjectPlanMapper(), new CoreViewModelFixture.TestSettingService());

        // An activity that takes time on one of its target resources. With two targets, either resource can do it.
        private static DependentActivity Activity(int id, int duration, int[] targetResources, params int[] dependencies) =>
            new(
                id: id,
                displayOrder: id,
                name: $@"Activity {id}",
                notes: string.Empty,
                targetWorkStreams: [],
                targetResources: targetResources,
                dependencies: dependencies,
                planningDependencies: [],
                resourceDependencies: [],
                successors: [],
                targetLogicalOperator: LogicalOperator.OR,
                allocatedToResources: [],
                canBeRemoved: false,
                hasNoCost: false,
                hasNoBilling: false,
                hasNoEffort: false,
                hasNoRisk: false,
                duration: duration,
                freeSlack: null,
                earliestStartTime: null,
                latestFinishTime: null,
                minimumFreeSlack: null,
                minimumEarliestStartTime: null,
                maximumLatestFinishTime: null,
                overrideColor: false,
                colorFormat: new ColorFormatModel(),
                trackers: []);

        // Two resources that an activity must name to get. The scheduler tries resource 1 first, because it has the lower
        // allocation order.
        private static List<IResource<int, int>> TwoResources() =>
        [
            new Resource<int, int>(1, @"Resource 1", isExplicitTarget: true, isInactive: false, InterActivityAllocationType.Direct, 1.0, 1.0, allocationOrder: 0, []),
            new Resource<int, int>(2, @"Resource 2", isExplicitTarget: true, isInactive: false, InterActivityAllocationType.Direct, 1.0, 1.0, allocationOrder: 1, []),
        ];

        // The compilation of a plan, as CompilationScheduler and the core make it: a new compiler, the activities in their
        // order, and the resources.
        private static IGraphCompilation<int, int, int, IDependentActivity> Compile(
            IEnumerable<DependentActivity> activities,
            List<IResource<int, int>> resources)
        {
            var compiler = new VertexGraphCompiler();

            foreach (DependentActivity activity in activities)
            {
                compiler.AddActivity(activity);
            }

            IGraphCompilation<int, int, int, IDependentActivity> compilation =
                compiler.Compile(resources, [], CancellationToken.None);

            compilation.CompilationErrors.ShouldBeEmpty();
            return compilation;
        }

        // What the build of the vertex graph did before: it made each resource dependency a dependency, and then it
        // compiled the activities once more with the resources. Only for the checks that a plan means something.
        private static IGraphCompilation<int, int, int, IDependentActivity> ScheduleAgain(
            IGraphCompilation<int, int, int, IDependentActivity> compilation,
            List<IResource<int, int>> resources)
        {
            var compiler = new VertexGraphCompiler();

            foreach (IDependentActivity activity in compilation.DependentActivities.Select(x => (IDependentActivity)x.CloneObject()))
            {
                activity.Dependencies.UnionWith(activity.ResourceDependencies);
                activity.ResourceDependencies.Clear();
                compiler.AddActivity(activity);
            }

            return compiler.Compile(resources, [], CancellationToken.None);
        }

        // The build gets the activities of the compilation and nothing else. It cannot schedule the resources of the plan
        // again, because it does not get them.
        private static VertexGraphModel BuildVertexGraph(IGraphCompilation<int, int, int, IDependentActivity> compilation) =>
            CreateService().BuildVertexGraph(compilation.DependentActivities);

        private static int Finish(IGraphCompilation<int, int, int, IDependentActivity> compilation) =>
            compilation.DependentActivities.Max(x => x.EarliestFinishTime.GetValueOrDefault());

        private static string Resources(IEnumerable<int> resources) => string.Join(@" ", resources.Order());

        // The values of an activity that a compilation gives and that a node of the vertex graph shows.
        private static string Describe(IDependentActivity activity) =>
            $@"{activity.Id}: EST {activity.EarliestStartTime}, EFT {activity.EarliestFinishTime}, LST {activity.LatestStartTime}, LFT {activity.LatestFinishTime}, free slack {activity.FreeSlack}, total slack {activity.TotalSlack}, resources [{Resources(activity.AllocatedToResources)}]";

        private static string Describe(ActivityModel activity) =>
            $@"{activity.Id}: EST {activity.EarliestStartTime}, EFT {activity.EarliestFinishTime}, LST {activity.LatestStartTime}, LFT {activity.LatestFinishTime}, free slack {activity.FreeSlack}, total slack {activity.TotalSlack}, resources [{Resources(activity.AllocatedToResources)}]";

        private static List<string> Describe(IGraphCompilation<int, int, int, IDependentActivity> compilation) =>
            [.. compilation.DependentActivities.OrderBy(x => x.Id).Select(Describe)];

        private static List<string> Describe(VertexGraphModel vertexGraph) =>
            [.. vertexGraph.Nodes.Select(x => x.Content).OrderBy(x => x.Id).Select(Describe)];

        // The links of the graph, each as the activity that it comes from and the activity that it goes to.
        private static List<string> Links(VertexGraphModel vertexGraph)
        {
            var links = new List<string>();

            foreach (EventEdgeModel edge in vertexGraph.Edges)
            {
                ActivityNodeModel tail = vertexGraph.Nodes.Single(x => x.OutgoingEdges.Contains(edge.Content.Id));
                ActivityNodeModel head = vertexGraph.Nodes.Single(x => x.IncomingEdges.Contains(edge.Content.Id));
                links.Add($@"{tail.Content.Id}->{head.Content.Id}");
            }

            links.Sort(StringComparer.Ordinal);
            return links;
        }

        // The smallest plan found for which a second schedule has another finish, with version 4.2.0 of the library.
        //
        // Activity 1 is on resource 1, and the other three come after it. Either resource can do activity 2 (one day) and
        // activity 3 (two days). Only resource 2 can do activity 4 (one day). Activity 3 has the least slack, and so the
        // compilation takes it first: it puts 3 on resource 1, then 2 on resource 2, and then 4 after 2 on resource 2. The
        // plan finishes at time 3, and 4 gets a resource dependency on 2.
        //
        // With that dependency as a dependency of 4, activity 2 has no slack either, and the scheduler takes it before 3. It
        // puts 2 on resource 1, the first that is free, and 3 on resource 2. Activity 4 is not ready until 2 finishes, and
        // then it must wait for resource 2 until 3 finishes. The plan finishes at time 4.
        //
        // Up to version 4.1.1 of the library, a smaller change of the order was enough. From 4.2.0, an activity that can use
        // either resource gives way to an activity that is ready at the same moment and can use only one. Here activity 4
        // is not ready at that moment, and so nothing gives way.
        private static List<DependentActivity> PlanThatASecondScheduleMakesLonger() =>
        [
            Activity(1, 1, [1]),
            Activity(2, 1, [1, 2], 1),
            Activity(3, 2, [1, 2], 1),
            Activity(4, 1, [2], 1),
        ];

        #endregion

        [Fact]
        public void BuildVertexGraph_Given_APlanThatASecondScheduleMakesLonger_Then_TheNodesHaveTheTimesOfTheCompilation()
        {
            List<IResource<int, int>> resources = TwoResources();
            IGraphCompilation<int, int, int, IDependentActivity> compilation = Compile(PlanThatASecondScheduleMakesLonger(), resources);

            // The plan is the one that the helper describes.
            Finish(compilation).ShouldBe(3);
            compilation.DependentActivities.Single(x => x.Id == 4).ResourceDependencies.ShouldBe(new HashSet<int> { 2 });
            Finish(ScheduleAgain(compilation, resources)).ShouldBe(4, "a second schedule of this plan is the same schedule, and the test proves nothing");

            VertexGraphModel vertexGraph = BuildVertexGraph(compilation);

            Describe(vertexGraph).ShouldBe(Describe(compilation), "the vertex graph shows another schedule than the compilation gave");
        }

        [Fact]
        public void BuildVertexGraph_Given_APlanThatASecondScheduleGivesOtherSlack_Then_TheNodesHaveTheSlackOfTheCompilation()
        {
            // Activities 2, 3 and 4 come after activity 1, and each takes one day. Either resource can do 2 and 3, and only
            // resource 2 can do 4. A second schedule has the same finish, but 2 and 3 change places, and so activity 2 gets
            // other latest times.
            List<IResource<int, int>> resources = TwoResources();
            IGraphCompilation<int, int, int, IDependentActivity> compilation = Compile(
                [
                    Activity(1, 1, [1]),
                    Activity(2, 1, [1, 2], 1),
                    Activity(3, 1, [1, 2], 1),
                    Activity(4, 1, [2], 1),
                ],
                resources);

            Describe(ScheduleAgain(compilation, resources)).ShouldNotBe(Describe(compilation), "a second schedule of this plan is the same schedule, and the test proves nothing");

            VertexGraphModel vertexGraph = BuildVertexGraph(compilation);

            Describe(vertexGraph).ShouldBe(Describe(compilation), "the vertex graph shows another schedule than the compilation gave");
        }

        [Fact]
        public void BuildVertexGraph_Given_AResourceDependency_Then_TheGraphDrawsItAsALink()
        {
            List<IResource<int, int>> resources = TwoResources();
            IGraphCompilation<int, int, int, IDependentActivity> compilation = Compile(PlanThatASecondScheduleMakesLonger(), resources);

            compilation.DependentActivities.Single(x => x.Id == 4).ResourceDependencies.ShouldBe(new HashSet<int> { 2 });

            VertexGraphModel vertexGraph = BuildVertexGraph(compilation);

            // 2->4 is the link that the resource gave. Activity 4 also depends on activity 1, but the link from 2 implies
            // that one, and so the graph does not draw it.
            Links(vertexGraph).ShouldBe(new[] { @"1->2", @"1->3", @"2->4" });
        }

        [Fact]
        public void BuildVertexGraph_Given_ACompilationWithNoResources_Then_TheNodesHaveTheTimesOfTheCompilation()
        {
            // A plan whose resources are disabled is compiled with no resources. The compiler of the library then puts the
            // activities on lanes of its own, and each activity gets the number of its lane as its resource. From version
            // 4.2.0 of the library, the lanes give no resource dependency. (Up to 4.1.1 they did: activity 4 followed
            // activity 2 on a lane here, and activity 2 lost one day of slack by it.) The vertex graph shows what the
            // compilation gave: the times, the lanes, and no link but that of the user.
            List<IResource<int, int>> noResources = [];
            IGraphCompilation<int, int, int, IDependentActivity> compilation = Compile(
                [
                    Activity(1, 5, []),
                    Activity(2, 1, []),
                    Activity(3, 2, []),
                    Activity(4, 1, [], 3),
                ],
                noResources);

            compilation.DependentActivities.SelectMany(x => x.ResourceDependencies).ShouldBeEmpty();
            compilation.DependentActivities.Single(x => x.Id == 2).AllocatedToResources.ShouldBe(new HashSet<int> { 2 }, "the test cannot tell the lanes of the compilation from none");

            VertexGraphModel vertexGraph = BuildVertexGraph(compilation);

            Describe(vertexGraph).ShouldBe(Describe(compilation));
            Links(vertexGraph).ShouldBe(new[] { @"3->4" });
        }

        [Fact]
        public void BuildVertexGraph_Given_ManySmallPlans_Then_TheNodesOfEachHaveTheTimesOfItsCompilation()
        {
            // The plans come from a fixed seed, and so each run of the test has the same plans. A plan has three to six
            // activities. Each activity takes one to three days on resource 1, on resource 2, or on either of them, and it
            // depends on each activity before it with a chance of one in five.
            const int planCount = 300;
            var random = new Random(20261120);
            List<IResource<int, int>> resources = TwoResources();
            int plansThatASecondScheduleChanges = 0;

            for (int plan = 0; plan < planCount; plan++)
            {
                var activities = new List<DependentActivity>();
                int activityCount = random.Next(3, 7);

                for (int id = 1; id <= activityCount; id++)
                {
                    int[] targetResources = random.Next(3) switch
                    {
                        0 => [1],
                        1 => [2],
                        _ => [1, 2],
                    };
                    int[] dependencies = [.. Enumerable.Range(1, id - 1).Where(_ => random.Next(5) == 0)];
                    activities.Add(Activity(id, random.Next(1, 4), targetResources, dependencies));
                }

                string description = string.Join(
                    @"; ",
                    activities.Select(x => $@"{x.Id} ({x.Duration} on {Resources(x.TargetResources)}, after [{Resources(x.Dependencies)}])"));

                IGraphCompilation<int, int, int, IDependentActivity> compilation = Compile(activities, resources);

                if (!Describe(ScheduleAgain(compilation, resources)).SequenceEqual(Describe(compilation)))
                {
                    plansThatASecondScheduleChanges++;
                }

                VertexGraphModel vertexGraph = BuildVertexGraph(compilation);

                Describe(vertexGraph).ShouldBe(Describe(compilation), $@"plan {plan}: {description}");
            }

            // The sweep means something only if a second schedule changes some of its plans.
            plansThatASecondScheduleChanges.ShouldBeGreaterThan(0, "no plan of the sweep can tell a second schedule from the first, and the sweep proves nothing");
        }
    }
}
