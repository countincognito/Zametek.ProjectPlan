using Riok.Mapperly.Abstractions;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    [Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
    public partial class ProjectPlanMapper
    {
        public static string FromNullableToDefault(string? src)
            => src is null ? string.Empty : src;

        public static List<int> FromNullableToDefault(HashSet<int>? src)
            => src is null ? [] : [.. src];

        public static List<bool> FromNullableToDefault(IEnumerable<bool>? src)
            => src is null ? [] : [.. src];

        public ResourceModel FromNullableToDefault(IResource<int, int>? src)
            => src is null ? new() : ToResourceModel((Resource<int, int>)src);

        public ScheduledActivityModel FromNullableToDefault(IScheduledActivity<int>? src)
            => src is null ? new() : ToScheduledActivityModel((ScheduledActivity<int>)src);

        public GraphCompilationErrorModel FromNullableToDefault(IGraphCompilationError? src)
            => src is null ? new() : ToGraphCompilationErrorModel((GraphCompilationError)src);

        public ResourceScheduleModel FromNullableToDefault(IResourceSchedule<int, int, int>? src)
            => src is null ? new() : ToResourceScheduleModel((ResourceSchedule<int, int, int>)src);

        public WorkStreamModel FromNullableToDefault(IWorkStream<int>? src)
            => src is null ? new() : ToWorkStreamModel((WorkStream<int>)src);

        // --------------------------------------------------------------------
        // Resource
        // --------------------------------------------------------------------

        // Auto: Resource<int,int> -> ResourceModel (same-named properties)
        public partial ResourceModel ToResourceModel(Resource<int, int> src);

        // Manual: ResourceModel -> Resource<int,int> (uses ctor)
        public Resource<int, int> ToResource(ResourceModel src)
            => new(
                id: src.Id,
                name: src.Name,
                isExplicitTarget: src.IsExplicitTarget,
                isInactive: src.IsInactive,
                interActivityAllocationType: src.InterActivityAllocationType,
                unitCost: src.UnitCost,
                unitBilling: src.UnitBilling,
                allocationOrder: src.AllocationOrder,
                interActivityPhases: src.InterActivityPhases
            );

        // --------------------------------------------------------------------
        // Event
        // --------------------------------------------------------------------

        public partial EventModel ToEventModel(Event<int> src);

        public static Event<int> ToEvent(EventModel src)
            => new(
                id: src.Id,
                earliestFinishTime: src.EarliestFinishTime,
                latestFinishTime: src.LatestFinishTime
            );

        // --------------------------------------------------------------------
        // Activity
        // --------------------------------------------------------------------

        // Auto: Activity<int,int,int> -> ActivityModel
        public partial ActivityModel ToActivityModel(Activity<int, int, int> src);

        // Manual: ActivityModel -> Activity<int,int,int>
        public static Activity<int, int, int> ToActivity(ActivityModel src)
            => new(
                    id: src.Id,
                    name: src.Name,
                    notes: src.Notes,
                    targetWorkStreams: src.TargetWorkStreams,
                    targetResources: src.TargetResources,
                    targetLogicalOperator: src.TargetResourceOperator,
                    allocatedToResources: src.AllocatedToResources,
                    canBeRemoved: src.CanBeRemoved,
                    hasNoCost: src.HasNoCost,
                    hasNoBilling: src.HasNoBilling,
                    hasNoEffort: src.HasNoEffort,
                    duration: src.Duration,
                    freeSlack: src.FreeSlack,
                    earliestStartTime: src.EarliestStartTime,
                    latestFinishTime: src.LatestFinishTime,
                    minimumFreeSlack: src.MinimumFreeSlack,
                    minimumEarliestStartTime: src.MinimumEarliestStartTime,
                    maximumLatestFinishTime: src.MaximumLatestFinishTime
                );

        // --------------------------------------------------------------------
        // DependentActivityModel <-> DependentActivity
        // --------------------------------------------------------------------

        public partial ActivityModel ToActivityModel(DependentActivity src);

        // DependentActivityModel -> DependentActivity
        public DependentActivity ToDependentActivity(DependentActivityModel src)
            => new(
                    id: src.Activity.Id,
                    name: src.Activity.Name,
                    notes: src.Activity.Notes,
                    targetWorkStreams: src.Activity.TargetWorkStreams,
                    targetResources: src.Activity.TargetResources,
                    dependencies: src.Dependencies,
                    planningDependencies: src.PlanningDependencies,
                    resourceDependencies: src.ResourceDependencies,
                    successors: src.Successors,
                    targetLogicalOperator: src.Activity.TargetResourceOperator,
                    allocatedToResources: src.Activity.AllocatedToResources,
                    canBeRemoved: src.Activity.CanBeRemoved,
                    hasNoCost: src.Activity.HasNoCost,
                    hasNoBilling: src.Activity.HasNoBilling,
                    hasNoEffort: src.Activity.HasNoEffort,
                    hasNoRisk: src.Activity.HasNoRisk,
                    duration: src.Activity.Duration,
                    freeSlack: src.Activity.FreeSlack,
                    earliestStartTime: src.Activity.EarliestStartTime,
                    latestFinishTime: src.Activity.LatestFinishTime,
                    minimumFreeSlack: src.Activity.MinimumFreeSlack,
                    minimumEarliestStartTime: src.Activity.MinimumEarliestStartTime,
                    maximumLatestFinishTime: src.Activity.MaximumLatestFinishTime,
                    trackers: src.Activity.Trackers
                );

        // DependentActivity -> DependentActivityModel
        public DependentActivityModel ToDependentActivityModel(DependentActivity src)
        {
            var model = new DependentActivityModel
            {
                Activity = ToActivityModel((Activity<int, int, int>)src)
            };

            model.Dependencies.AddRange(src.Dependencies);
            model.PlanningDependencies.AddRange(src.PlanningDependencies);
            model.ResourceDependencies.AddRange(src.ResourceDependencies);
            model.Successors.AddRange(src.Successors);

            model.Activity.Trackers.Clear();
            model.Activity.Trackers.AddRange(src.Trackers);

            return model;
        }

        // --------------------------------------------------------------------
        // ManagedActivityViewModel
        // --------------------------------------------------------------------

        // Auto: ManagedActivityViewModel -> ActivityModel except Trackers
        public partial ActivityModel ToActivityModelCore(ManagedActivityViewModel src);

        public ActivityModel ToActivityModel(ManagedActivityViewModel src)
        {
            var dest = ToActivityModelCore(src);
            dest.Trackers.Clear();
            dest.Trackers.AddRange(src.TrackerSet.Trackers);
            return dest;
        }

        public DependentActivityModel ToDependentActivityModel(ManagedActivityViewModel src)
        {
            var dest = new DependentActivityModel
            {
                Activity = ToActivityModel(src)
            };

            dest.Dependencies.AddRange(src.Dependencies);
            dest.PlanningDependencies.AddRange(src.PlanningDependencies);
            dest.ResourceDependencies.AddRange(src.ResourceDependencies);
            dest.Successors.AddRange(src.Successors);

            return dest;
        }

        // --------------------------------------------------------------------
        // ScheduledActivity
        // --------------------------------------------------------------------

        public static ScheduledActivity<int> ToScheduledActivity(ScheduledActivityModel src)
            => new(
                id: src.Id,
                name: src.Name,
                hasNoCost: src.HasNoCost,
                hasNoBilling: src.HasNoBilling,
                hasNoEffort: src.HasNoEffort,
                duration: src.Duration,
                startTime: src.StartTime,
                finishTime: src.FinishTime
            );

        public partial ScheduledActivityModel ToScheduledActivityModel(ScheduledActivity<int> src);

        // --------------------------------------------------------------------
        // ResourceSchedule
        // --------------------------------------------------------------------

        public IResourceSchedule<int, int, int> ToResourceSchedule(ResourceScheduleModel src)
        {
            ResourceScheduleBuilder<int, int, int> builder =
                src.Resource == default || src.Resource is null
                ? new()
                : new(ToResource(src.Resource));

            foreach (ScheduledActivityModel scheduledActivity in src.ScheduledActivities)
            {
                builder.AppendActivityWithoutChecks(ToScheduledActivity(scheduledActivity));
            }

            return builder.ToResourceSchedule([], src.StartTime, src.FinishTime);
        }

        public ResourceSchedule<int, int, int> ToConcreteResourceSchedule(ResourceScheduleModel src)
            => (ResourceSchedule<int, int, int>)ToResourceSchedule(src);

        public ResourceScheduleModel ToResourceScheduleModel(IResourceSchedule<int, int, int> src)
        {
            ResourceModel resourceModel =
                src.Resource == default || src.Resource is null
                ? new()
                : ToResourceModel((Resource<int, int>)src.Resource);

            return new ResourceScheduleModel
            {
                Resource = resourceModel,
                ScheduledActivities = [.. src.ScheduledActivities.Select(x => ToScheduledActivityModel((ScheduledActivity<int>)x))],
                ActivityAllocation = [.. src.ActivityAllocation],
                CostAllocation = [.. src.CostAllocation],
                BillingAllocation = [.. src.BillingAllocation],
                EffortAllocation = [.. src.EffortAllocation],
                StartTime = src.StartTime,
                FinishTime = src.FinishTime
            };
        }

        // --------------------------------------------------------------------
        // WorkStream
        // --------------------------------------------------------------------

        public WorkStream<int> ToWorkStream(WorkStreamModel src)
            => new(
                id: src.Id,
                name: src.Name,
                isPhase: src.IsPhase
            );

        public partial WorkStreamModel ToWorkStreamModel(WorkStream<int> src);

        // --------------------------------------------------------------------
        // GraphCompilationError
        // --------------------------------------------------------------------

        public static GraphCompilationError ToGraphCompilationError(GraphCompilationErrorModel src)
            => new(
                errorCode: src.ErrorCode,
                errorMessage: src.ErrorMessage
            );

        public partial GraphCompilationErrorModel ToGraphCompilationErrorModel(GraphCompilationError src);

        // --------------------------------------------------------------------
        // GraphCompilation
        // --------------------------------------------------------------------

        public IGraphCompilation<int, int, int, DependentActivity> ToGraphCompilation(GraphCompilationModel src)
        {
            List<DependentActivity> dependentActivities = [.. src.DependentActivities.Select(ToDependentActivity)];
            List<ResourceSchedule<int, int, int>> resourceSchedules = [.. src.ResourceSchedules.Select(ToConcreteResourceSchedule)];
            List<WorkStream<int>> workStreams = [.. src.WorkStreams.Select(ToWorkStream)];
            List<GraphCompilationError> compilationErrors = [.. src.CompilationErrors.Select(ToGraphCompilationError)];

            return new GraphCompilation<int, int, int, DependentActivity>(
                dependentActivities: dependentActivities,
                resourceSchedules: resourceSchedules,
                workStreams: workStreams,
                compilationErrors: compilationErrors
            );
        }

        public IEnumerable<ResourceScheduleModel> ToResourceScheduleModels(IGraphCompilation<int, int, int, IDependentActivity> src)
            => src.ResourceSchedules.Select(ToResourceScheduleModel);

        public GraphCompilationModel ToGraphCompilationModel(IGraphCompilation<int, int, int, IDependentActivity> src)
        {
            return new GraphCompilationModel
            {
                DependentActivities = [.. src.DependentActivities.Select(x => ToDependentActivityModel((DependentActivity)x))],
                ResourceSchedules = [.. src.ResourceSchedules.Select(x => ToResourceScheduleModel(x))],
                WorkStreams = [.. src.WorkStreams.Select(x => ToWorkStreamModel((WorkStream<int>)x))],
                CompilationErrors = [.. src.CompilationErrors.Select(x => ToGraphCompilationErrorModel((GraphCompilationError)x))],
                CyclomaticComplexity = default,
                Duration = default
            };
        }

        // --------------------------------------------------------------------
        // Edges, Nodes, Graphs
        // --------------------------------------------------------------------

        public partial DependentActivity ToDependentActivity(ActivityModel src);

        public Edge<int, IDependentActivity> ToActivityEdge(ActivityEdgeModel src)
            => new(ToDependentActivity(src.Content));

        public partial ActivityEdgeModel ToActivityEdgeModel(Edge<int, IDependentActivity> src);

        public static Edge<int, IEvent<int>> ToEventEdge(EventEdgeModel src)
            => new(ToEvent(src.Content));

        public partial EventEdgeModel ToEventEdgeModel(Edge<int, IEvent<int>> src);

        public static Node<int, IEvent<int>> ToEventNode(EventNodeModel src)
        {
            var dest = new Node<int, IEvent<int>>(src.NodeType, ToEvent(src.Content));

            if (src.NodeType != Maths.Graphs.NodeType.Start
                && src.NodeType != Maths.Graphs.NodeType.Isolated)
            {
                dest.IncomingEdges.Clear();
                foreach (int incomingEdgeId in src.IncomingEdges)
                {
                    dest.IncomingEdges.Add(incomingEdgeId);
                }
            }

            if (src.NodeType != Maths.Graphs.NodeType.End
                && src.NodeType != Maths.Graphs.NodeType.Isolated)
            {
                dest.OutgoingEdges.Clear();
                foreach (int outgoingEdgeId in src.OutgoingEdges)
                {
                    dest.OutgoingEdges.Add(outgoingEdgeId);
                }
            }

            return dest;
        }

        public EventNodeModel ToEventNodeModel(Node<int, IEvent<int>> src)
        {
            var dest = new EventNodeModel
            {
                NodeType = src.NodeType,
                Content = ToEventModel((Event<int>)src.Content)
            };

            if (src.NodeType != Maths.Graphs.NodeType.Start
                && src.NodeType != Maths.Graphs.NodeType.Isolated)
            {
                dest.IncomingEdges.Clear();
                foreach (int incomingEdgeId in src.IncomingEdges)
                {
                    dest.IncomingEdges.Add(incomingEdgeId);
                }
            }

            if (src.NodeType != Maths.Graphs.NodeType.End
                && src.NodeType != Maths.Graphs.NodeType.Isolated)
            {
                dest.OutgoingEdges.Clear();
                foreach (int outgoingEdgeId in src.OutgoingEdges)
                {
                    dest.OutgoingEdges.Add(outgoingEdgeId);
                }
            }

            return dest;
        }

        public Node<int, IDependentActivity> ToActivityNode(ActivityNodeModel src)
        {
            var dest = new Node<int, IDependentActivity>(src.NodeType, ToDependentActivity(src.Content));

            if (src.NodeType != Maths.Graphs.NodeType.Start
                && src.NodeType != Maths.Graphs.NodeType.Isolated)
            {
                dest.IncomingEdges.Clear();
                foreach (int incomingEdgeId in src.IncomingEdges)
                {
                    dest.IncomingEdges.Add(incomingEdgeId);
                }
            }

            if (src.NodeType != Maths.Graphs.NodeType.End
                && src.NodeType != Maths.Graphs.NodeType.Isolated)
            {
                dest.OutgoingEdges.Clear();
                foreach (int outgoingEdgeId in src.OutgoingEdges)
                {
                    dest.OutgoingEdges.Add(outgoingEdgeId);
                }
            }

            return dest;
        }

        public ActivityNodeModel ToActivityNodeModel(Node<int, IDependentActivity> src)
        {
            var dest = new ActivityNodeModel
            {
                NodeType = src.NodeType,
                Content = ToActivityModel((DependentActivity)src.Content)
            };

            if (src.NodeType != Maths.Graphs.NodeType.Start
                && src.NodeType != Maths.Graphs.NodeType.Isolated)
            {
                dest.IncomingEdges.Clear();
                foreach (int incomingEdgeId in src.IncomingEdges)
                {
                    dest.IncomingEdges.Add(incomingEdgeId);
                }
            }

            if (src.NodeType != Maths.Graphs.NodeType.End
                && src.NodeType != Maths.Graphs.NodeType.Isolated)
            {
                dest.OutgoingEdges.Clear();
                foreach (int outgoingEdgeId in src.OutgoingEdges)
                {
                    dest.OutgoingEdges.Add(outgoingEdgeId);
                }
            }

            return dest;
        }

        public ArrowGraphModel ToArrowGraphModel(
            Graph<int, IDependentActivity,
                IEvent<int>> src)
        {
            var dest = new ArrowGraphModel
            {
                Edges = [.. src.Edges.Select(ToActivityEdgeModel)],
                Nodes = [.. src.Nodes.Select(ToEventNodeModel)],
                IsStale = false
            };
            return dest;
        }

        public partial Graph<int, IDependentActivity, IEvent<int>> ToArrowGraph(ArrowGraphModel src);

        public VertexGraphModel ToVertexGraphModel(
            Graph<int, IEvent<int>,
                IDependentActivity> src)
        {
            var dest = new VertexGraphModel
            {
                Edges = [.. src.Edges.Select(ToEventEdgeModel)],
                Nodes = [.. src.Nodes.Select(ToActivityNodeModel)],
            };
            return dest;
        }

        public partial Graph<int, IEvent<int>, IDependentActivity> ToVertexGraph(VertexGraphModel src);
    }
}
