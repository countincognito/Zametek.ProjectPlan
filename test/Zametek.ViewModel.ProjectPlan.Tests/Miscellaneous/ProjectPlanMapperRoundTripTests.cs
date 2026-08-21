using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The reading half of the mapper: the mappings that turn stored models back into
    /// the graph, compilation and schedule objects the plan is made of.
    ///
    /// Nothing in the application calls these today - the plan is rebuilt by compiling
    /// the activities rather than by reading back a stored compilation - so they have no
    /// coverage from anywhere else, and a fault in one of them would sit unnoticed until
    /// the day something did call it. These tests pin what each of them carries, and,
    /// where a mapping deliberately narrows, pin the narrowing too, so that the losses
    /// on record here are the ones that were meant.
    /// </summary>
    public class ProjectPlanMapperRoundTripTests
    {
        #region Helpers

        private static ActivityModel MakeActivityModel(
            int id,
            bool hasNoCost = false,
            bool hasNoBilling = false,
            bool hasNoEffort = false,
            bool hasNoRisk = false) =>
            new ActivityModel
            {
                Id = id,
                DisplayOrder = id,
                Name = $@"Activity {id}",
                Notes = $@"Notes {id}",
                TargetWorkStreams = [1],
                TargetResources = [2, 3],
                TargetResourceOperator = LogicalOperator.OR,
                AllocatedToResources = [2],
                CanBeRemoved = false,
                HasNoCost = hasNoCost,
                HasNoBilling = hasNoBilling,
                HasNoEffort = hasNoEffort,
                HasNoRisk = hasNoRisk,
                Duration = 4,
                FreeSlack = 1,
                EarliestStartTime = 2,
                LatestFinishTime = 9,
                MinimumFreeSlack = 1,
                MinimumEarliestStartTime = 2,
                MaximumLatestFinishTime = 20,
                OverrideColor = true,
                ColorFormat = new ColorFormatModel { A = 1, R = 2, G = 3, B = 4 },
                Trackers = [new ActivityTrackerModel { Time = 0, ActivityId = id, PercentageComplete = 25 }],
            };

        private static DependentActivityModel MakeDependentActivityModel(
            int id,
            bool hasNoCost = false,
            bool hasNoBilling = false,
            bool hasNoEffort = false,
            bool hasNoRisk = false) =>
            new DependentActivityModel
            {
                Activity = MakeActivityModel(id, hasNoCost, hasNoBilling, hasNoEffort, hasNoRisk),
                Dependencies = [id - 1],
                PlanningDependencies = [id - 2],
                ResourceDependencies = [id - 3],
                Successors = [id + 1],
            };

        #endregion

        #region GraphCompilation

        [Fact]
        public void GraphCompilation_RoundTrippedThroughItsModel_Then_KeepsTheActivitiesAndTheirFlags()
        {
            var mapper = new ProjectPlanMapper();

            var model = new GraphCompilationModel
            {
                DependentActivities =
                [
                    MakeDependentActivityModel(4),
                    MakeDependentActivityModel(5, hasNoCost: true, hasNoBilling: true, hasNoEffort: true, hasNoRisk: true),
                ],
                WorkStreams = [new WorkStreamModel { Id = 1, Name = @"Stream", IsPhase = true }],
                CompilationErrors =
                [
                    new GraphCompilationErrorModel { ErrorCode = GraphCompilationErrorCode.C0010, ErrorMessage = @"Boom" },
                ],
            };

            IGraphCompilation<int, int, int, DependentActivity> compilation = mapper.ToGraphCompilation(model);
            GraphCompilationModel roundTripped = mapper.ToGraphCompilationModel(compilation);

            roundTripped.DependentActivities.Count.ShouldBe(2);

            DependentActivityModel plain = roundTripped.DependentActivities.Single(x => x.Activity.Id == 4);
            plain.Activity.HasNoCost.ShouldBeFalse();
            plain.Activity.HasNoBilling.ShouldBeFalse();
            plain.Activity.HasNoEffort.ShouldBeFalse();
            plain.Activity.HasNoRisk.ShouldBeFalse();

            DependentActivityModel flagged = roundTripped.DependentActivities.Single(x => x.Activity.Id == 5);
            flagged.Activity.HasNoCost.ShouldBeTrue();
            flagged.Activity.HasNoBilling.ShouldBeTrue();
            flagged.Activity.HasNoEffort.ShouldBeTrue();
            flagged.Activity.HasNoRisk.ShouldBeTrue();

            flagged.Dependencies.ShouldBe([4], ignoreOrder: true);
            flagged.PlanningDependencies.ShouldBe([3], ignoreOrder: true);
            flagged.ResourceDependencies.ShouldBe([2], ignoreOrder: true);
            flagged.Successors.ShouldBe([6], ignoreOrder: true);

            flagged.Activity.Trackers.ShouldHaveSingleItem().PercentageComplete.ShouldBe(25);
            flagged.Activity.ColorFormat.ShouldBe(new ColorFormatModel { A = 1, R = 2, G = 3, B = 4 });
            flagged.Activity.OverrideColor.ShouldBeTrue();
            flagged.Activity.DisplayOrder.ShouldBe(5);

            roundTripped.CompilationErrors.ShouldHaveSingleItem().ErrorMessage.ShouldBe(@"Boom");
        }

        [Fact]
        public void GraphCompilation_BuiltFromAModel_Then_SharesNothingWithIt()
        {
            var mapper = new ProjectPlanMapper();
            var model = new GraphCompilationModel { DependentActivities = [MakeDependentActivityModel(4)] };

            IGraphCompilation<int, int, int, DependentActivity> compilation = mapper.ToGraphCompilation(model);

            DependentActivity activity = compilation.DependentActivities.Single();
            activity.Trackers.ShouldNotBeSameAs(model.DependentActivities[0].Activity.Trackers);

            activity.Trackers.Clear();
            activity.Dependencies.Clear();
            activity.TargetResources.Clear();

            model.DependentActivities[0].Activity.Trackers.ShouldHaveSingleItem();
            model.DependentActivities[0].Dependencies.ShouldBe([3], ignoreOrder: true);
            model.DependentActivities[0].Activity.TargetResources.ShouldBe([2, 3], ignoreOrder: true);
        }

        #endregion

        #region Arrow and vertex graphs

        [Fact]
        public void ArrowGraph_RoundTrippedThroughItsModel_Then_KeepsItsEdgesAndNodes()
        {
            var mapper = new ProjectPlanMapper();

            var model = new ArrowGraphModel
            {
                Edges =
                [
                    new ActivityEdgeModel { Content = MakeActivityModel(1, hasNoRisk: true) },
                    new ActivityEdgeModel { Content = MakeActivityModel(2) },
                ],
                Nodes =
                [
                    new EventNodeModel
                    {
                        NodeType = Maths.Graphs.NodeType.Normal,
                        Content = new EventModel { Id = 10, EarliestFinishTime = 3, LatestFinishTime = 7 },
                        IncomingEdges = [1],
                        OutgoingEdges = [2],
                    },
                ],
            };

            Graph<int, IDependentActivity, IEvent<int>> graph = mapper.ToArrowGraph(model);
            ArrowGraphModel roundTripped = mapper.ToArrowGraphModel(graph);

            roundTripped.Edges.Select(x => x.Content.Id).ShouldBe([1, 2]);

            // The flags travel on the edge content, which is a full activity.
            roundTripped.Edges.Single(x => x.Content.Id == 1).Content.HasNoRisk.ShouldBeTrue();
            roundTripped.Edges.Single(x => x.Content.Id == 2).Content.HasNoRisk.ShouldBeFalse();

            EventNodeModel node = roundTripped.Nodes.ShouldHaveSingleItem();
            node.NodeType.ShouldBe(Maths.Graphs.NodeType.Normal);
            node.Content.Id.ShouldBe(10);
            node.Content.EarliestFinishTime.ShouldBe(3);
            node.Content.LatestFinishTime.ShouldBe(7);
            node.IncomingEdges.ShouldBe([1]);
            node.OutgoingEdges.ShouldBe([2]);
        }

        [Fact]
        public void VertexGraph_RoundTrippedThroughItsModel_Then_KeepsItsEdgesAndNodes()
        {
            var mapper = new ProjectPlanMapper();

            var model = new VertexGraphModel
            {
                Edges =
                [
                    new EventEdgeModel { Content = new EventModel { Id = 10, EarliestFinishTime = 3, LatestFinishTime = 7 } },
                ],
                Nodes =
                [
                    new ActivityNodeModel
                    {
                        NodeType = Maths.Graphs.NodeType.Normal,
                        Content = MakeActivityModel(1, hasNoCost: true, hasNoRisk: true),
                        IncomingEdges = [9],
                        OutgoingEdges = [10],
                    },
                ],
            };

            Graph<int, IEvent<int>, IDependentActivity> graph = mapper.ToVertexGraph(model);
            VertexGraphModel roundTripped = mapper.ToVertexGraphModel(graph);

            roundTripped.Edges.ShouldHaveSingleItem().Content.Id.ShouldBe(10);

            ActivityNodeModel node = roundTripped.Nodes.ShouldHaveSingleItem();
            node.NodeType.ShouldBe(Maths.Graphs.NodeType.Normal);
            node.Content.Id.ShouldBe(1);
            node.Content.HasNoCost.ShouldBeTrue();
            node.Content.HasNoRisk.ShouldBeTrue();
            node.Content.HasNoBilling.ShouldBeFalse();
            node.IncomingEdges.ShouldBe([9]);
            node.OutgoingEdges.ShouldBe([10]);
        }

        [Fact]
        public void ArrowGraph_BuiltFromAModel_Then_SharesNoEdgeListsWithIt()
        {
            var mapper = new ProjectPlanMapper();

            var node = new EventNodeModel
            {
                NodeType = Maths.Graphs.NodeType.Normal,
                Content = new EventModel { Id = 10 },
                IncomingEdges = [1],
                OutgoingEdges = [2],
            };
            var model = new ArrowGraphModel { Nodes = [node] };

            Graph<int, IDependentActivity, IEvent<int>> graph = mapper.ToArrowGraph(model);

            Node<int, IEvent<int>> mapped = graph.Nodes.Single();
            mapped.IncomingEdges.Clear();
            mapped.OutgoingEdges.Clear();

            node.IncomingEdges.ShouldBe([1]);
            node.OutgoingEdges.ShouldBe([2]);
        }

        #endregion

        #region ResourceSchedule

        [Fact]
        public void ResourceSchedule_RoundTrippedThroughItsModel_Then_KeepsItsResourceAndItsActivities()
        {
            var mapper = new ProjectPlanMapper();

            var model = new ResourceScheduleModel
            {
                Resource = new ResourceModel
                {
                    Id = 7,
                    Name = @"Resource Seven",
                    IsExplicitTarget = true,
                    IsInactive = true,
                    InterActivityAllocationType = InterActivityAllocationType.Indirect,
                    UnitCost = 11.0,
                    UnitBilling = 22.0,
                    AllocationOrder = 3,
                    InterActivityPhases = [1, 2],
                },
                ScheduledActivities =
                [
                    new ScheduledActivityModel { Id = 1, Name = @"One", Duration = 2, StartTime = 0, FinishTime = 2 },
                    new ScheduledActivityModel { Id = 2, Name = @"Two", Duration = 3, StartTime = 2, FinishTime = 5, HasNoCost = true, HasNoBilling = true, HasNoEffort = true },
                ],
                StartTime = 0,
                FinishTime = 5,
            };

            ResourceScheduleModel roundTripped =
                mapper.ToResourceScheduleModel(mapper.ToResourceSchedule(model));

            roundTripped.Resource.Id.ShouldBe(7);
            roundTripped.Resource.Name.ShouldBe(@"Resource Seven");
            roundTripped.Resource.IsExplicitTarget.ShouldBeTrue();
            roundTripped.Resource.IsInactive.ShouldBeTrue();
            roundTripped.Resource.InterActivityAllocationType.ShouldBe(InterActivityAllocationType.Indirect);
            roundTripped.Resource.UnitCost.ShouldBe(11.0);
            roundTripped.Resource.UnitBilling.ShouldBe(22.0);
            roundTripped.Resource.AllocationOrder.ShouldBe(3);
            roundTripped.Resource.InterActivityPhases.ShouldBe([1, 2], ignoreOrder: true);

            roundTripped.ScheduledActivities.Count.ShouldBe(2);

            ScheduledActivityModel exempt = roundTripped.ScheduledActivities.Single(x => x.Id == 2);
            exempt.Name.ShouldBe(@"Two");
            exempt.Duration.ShouldBe(3);
            exempt.HasNoCost.ShouldBeTrue();
            exempt.HasNoBilling.ShouldBeTrue();
            exempt.HasNoEffort.ShouldBeTrue();
        }

        #endregion

        #region The narrowings, pinned

        /// <summary>
        /// A resource carries more in the settings than the scheduler's own resource
        /// type has room for, so the seven settings-only properties do not survive a
        /// trip through it. That is by design - the schedule only needs the identity and
        /// the allocation type, and the settings are stored separately - but it means
        /// nothing may read a display order, a colour or a fixed charge off a resource
        /// that came back from a schedule, because it will always read the default.
        /// </summary>
        [Fact]
        public void Resource_RoundTrippedThroughTheSchedulersType_Then_KeepsOnlyWhatTheSchedulerNeeds()
        {
            var mapper = new ProjectPlanMapper();

            var model = new ResourceModel
            {
                Id = 7,
                DisplayOrder = 4,
                Notes = @"Notes",
                ActivityAllocationType = ActivityAllocationType.Indirect,
                InterActivityAllocationType = InterActivityAllocationType.Indirect,
                FixedCost = 100.0,
                FixedBilling = 250.0,
                ColorFormat = new ColorFormatModel { A = 1, R = 2, G = 3, B = 4 },
                Trackers = [new ResourceTrackerModel { Time = 1, ResourceId = 7 }],
            };

            ResourceModel roundTripped = mapper.ToResourceModel(mapper.ToResource(model));

            // Kept: what the scheduler reads.
            roundTripped.Id.ShouldBe(7);
            roundTripped.InterActivityAllocationType.ShouldBe(InterActivityAllocationType.Indirect);

            // Dropped: what only the settings own.
            roundTripped.DisplayOrder.ShouldBe(0);
            roundTripped.Notes.ShouldBeEmpty();
            roundTripped.ActivityAllocationType.ShouldBe(default(ActivityAllocationType));
            roundTripped.FixedCost.ShouldBe(0.0);
            roundTripped.FixedBilling.ShouldBe(0.0);
            roundTripped.ColorFormat.ShouldBe(new ColorFormatModel());
            roundTripped.Trackers.ShouldBeEmpty();
        }

        /// <summary>
        /// The same narrowing for work streams: the compiler's work stream is an
        /// identity, a name and whether it is a phase, so the display order and the
        /// colour are the settings' to keep.
        /// </summary>
        [Fact]
        public void WorkStream_RoundTrippedThroughTheCompilersType_Then_LosesItsDisplayProperties()
        {
            var mapper = new ProjectPlanMapper();

            var model = new WorkStreamModel
            {
                Id = 3,
                Name = @"Stream",
                IsPhase = true,
                DisplayOrder = 9,
                ColorFormat = new ColorFormatModel { A = 1, R = 2, G = 3, B = 4 },
            };

            WorkStreamModel roundTripped = mapper.ToWorkStreamModel(mapper.ToWorkStream(model));

            roundTripped.Id.ShouldBe(3);
            roundTripped.Name.ShouldBe(@"Stream");
            roundTripped.IsPhase.ShouldBeTrue();

            roundTripped.DisplayOrder.ShouldBe(0);
            roundTripped.ColorFormat.ShouldBe(new ColorFormatModel());
        }

        #endregion
    }
}
