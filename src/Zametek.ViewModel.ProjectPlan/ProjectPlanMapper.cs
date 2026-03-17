using Riok.Mapperly.Abstractions;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    [Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
    public partial class ProjectPlanMapper
    {
        public static string FromNullableStringToEmpty(string? src)
            => src is null ? string.Empty : src;

        // --------------------------------------------------------------------
        // Resource
        // --------------------------------------------------------------------

        // Auto: Resource<int,int> -> ResourceModel (same-named properties)
        public partial ResourceModel ToResourceModel(Resource<int, int> src);

        // Manual: ResourceModel -> Resource<int,int> (uses ctor)
        public Resource<int, int> ToResource(ResourceModel src)
            => new(
                src.Id,
                src.Name,
                src.IsExplicitTarget,
                src.IsInactive,
                src.InterActivityAllocationType,
                src.UnitCost,
                src.UnitBilling,
                src.AllocationOrder,
                src.InterActivityPhases
            );

        // --------------------------------------------------------------------
        // Event
        // --------------------------------------------------------------------

        public partial EventModel ToEventModel(Event<int> src);

        public Event<int> ToEvent(EventModel src)
            => new(src.Id, src.EarliestFinishTime, src.LatestFinishTime);

        // --------------------------------------------------------------------
        // Activity
        // --------------------------------------------------------------------

        // Auto: Activity<int,int,int> -> ActivityModel
        public partial ActivityModel ToActivityModel(Activity<int, int, int> src);

        // Manual: ActivityModel -> Activity<int,int,int>
        public Activity<int, int, int> ToActivity(ActivityModel src)
        {
            var dest = new Activity<int, int, int>(src.Id, src.Duration);

            dest.TargetResources.Clear();
            foreach (int targetResource in src.TargetResources)
            {
                dest.TargetResources.Add(targetResource);
            }
            dest.AllocatedToResources.Clear();
            foreach (int allocatedToResource in src.AllocatedToResources)
            {
                dest.AllocatedToResources.Add(allocatedToResource);
            }
            dest.TargetWorkStreams.Clear();
            foreach (int targetWorkStream in src.TargetWorkStreams)
            {
                dest.TargetWorkStreams.Add(targetWorkStream);
            }

            return dest;
        }

        // --------------------------------------------------------------------
        // DependentActivityModel <-> DependentActivity
        // --------------------------------------------------------------------

        // Base Activity<int,int,int> -> ActivityModel
        [MapProperty(nameof(DependentActivity.Id), nameof(ActivityModel.Id))]
        [MapProperty(nameof(DependentActivity.Duration), nameof(ActivityModel.Duration))]
        public partial ActivityModel ToActivityModel(DependentActivity src);

        // DependentActivityModel -> DependentActivity
        public DependentActivity ToDependentActivity(DependentActivityModel src)
        {
            // Create with base activity data
            var dest = new DependentActivity(src.Activity.Id, src.Activity.Duration);

            // Map “Activity” part
            Activity<int, int, int> baseActivity = ToActivity(src.Activity);
            CopyActivity(baseActivity, dest);

            // Map dependency collections
            dest.Dependencies.Clear();
            foreach (int dependency in src.Dependencies)
            {
                dest.Dependencies.Add(dependency);
            }

            dest.PlanningDependencies.Clear();
            foreach (int planningDependency in src.PlanningDependencies)
            {
                dest.PlanningDependencies.Add(planningDependency);
            }

            dest.ResourceDependencies.Clear();
            foreach (int resourceDependency in src.ResourceDependencies)
            {
                dest.ResourceDependencies.Add(resourceDependency);
            }

            dest.Successors.Clear();
            foreach (int successor in src.Successors)
            {
                dest.Successors.Add(successor);
            }

            dest.Trackers.Clear();
            dest.Trackers.AddRange(src.Activity.Trackers);

            return dest;
        }

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

        private static void CopyActivity(Activity<int, int, int> src, DependentActivity dest)
        {
            dest.TargetResources.Clear();
            foreach (int targetResource in src.TargetResources)
            {
                dest.TargetResources.Add(targetResource);
            }
            dest.AllocatedToResources.Clear();
            foreach (int allocatedToResource in src.AllocatedToResources)
            {
                dest.AllocatedToResources.Add(allocatedToResource);
            }
            dest.TargetWorkStreams.Clear();
            foreach (int targetWorkStream in src.TargetWorkStreams)
            {
                dest.TargetWorkStreams.Add(targetWorkStream);
            }
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

        public ScheduledActivity<int> ToScheduledActivity(ScheduledActivityModel src)
            => new(
                src.Id,
                src.Name,
                src.HasNoCost,
                src.HasNoBilling,
                src.HasNoEffort,
                src.Duration,
                src.StartTime,
                src.FinishTime
            );

        public partial ScheduledActivityModel ToScheduledActivityModel(ScheduledActivity<int> src);

        // --------------------------------------------------------------------
        // ResourceSchedule
        // --------------------------------------------------------------------

        public IResourceSchedule<int, int, int> ToResourceSchedule(ResourceScheduleModel src)
        {
            var builder = src.Resource is null
                ? new ResourceScheduleBuilder<int, int, int>()
                : new ResourceScheduleBuilder<int, int, int>(ToResource(src.Resource));

            foreach (var sa in src.ScheduledActivities)
            {
                builder.AppendActivityWithoutChecks(ToScheduledActivity(sa));
            }

            return builder.ToResourceSchedule([], src.StartTime, src.FinishTime);
        }

        public ResourceSchedule<int, int, int> ToConcreteResourceSchedule(ResourceScheduleModel src)
            => (ResourceSchedule<int, int, int>)ToResourceSchedule(src);

        // Auto: IResourceSchedule<int,int,int> -> ResourceScheduleModel (simple props & collections)
        public partial ResourceScheduleModel ToResourceScheduleModel(IResourceSchedule<int, int, int> src);

        // --------------------------------------------------------------------
        // WorkStream
        // --------------------------------------------------------------------

        public WorkStream<int> ToWorkStream(WorkStreamModel src)
            => new(src.Id, src.Name, src.IsPhase);

        public partial WorkStreamModel ToWorkStreamModel(WorkStream<int> src);

        // --------------------------------------------------------------------
        // GraphCompilationError
        // --------------------------------------------------------------------

        public GraphCompilationError ToCompilationError(GraphCompilationErrorModel src)
            => new(src.ErrorCode, src.ErrorMessage);

        public partial GraphCompilationErrorModel ToCompilationErrorModel(GraphCompilationError src);

        // --------------------------------------------------------------------
        // GraphCompilation
        // --------------------------------------------------------------------

        public IGraphCompilation<int, int, int, DependentActivity> ToGraphCompilation(GraphCompilationModel src)
        {
            var dependentActivities = src.DependentActivities.Select(ToDependentActivity).ToList();
            var resourceSchedules = src.ResourceSchedules.Select(ToConcreteResourceSchedule).ToList();
            var workStreams = src.WorkStreams.Select(ToWorkStream).ToList();
            var errors = src.CompilationErrors.Select(ToCompilationError).ToList();

            return new GraphCompilation<int, int, int, DependentActivity>(
                dependentActivities,
                resourceSchedules,
                workStreams,
                errors
            );
        }

        public IEnumerable<ResourceScheduleModel> ToResourceScheduleModels(
            IGraphCompilation<int, int, int, IDependentActivity> src)
            => src.ResourceSchedules.Select(ToResourceScheduleModel);

        public partial GraphCompilationModel ToGraphCompilationModel(
            IGraphCompilation<int, int, int, IDependentActivity> src);

        // --------------------------------------------------------------------
        // Edges, Nodes, Graphs
        // --------------------------------------------------------------------

        public partial DependentActivity ToDependentActivity(ActivityModel src);

        public Edge<int, IDependentActivity> ToActivityEdge(ActivityEdgeModel src)
            => new(ToDependentActivity(src.Content));

        public partial ActivityEdgeModel ToActivityEdgeModel(Edge<int, IDependentActivity> src);

        public Edge<int, IEvent<int>> ToEventEdge(EventEdgeModel src)
            => new(ToEvent(src.Content));

        public partial EventEdgeModel ToEventEdgeModel(Edge<int, IEvent<int>> src);

        public Node<int, IEvent<int>> ToEventNode(EventNodeModel src)
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

        public ArrowGraphModel ToArrowGraphModel(Graph<int, IDependentActivity, IEvent<int>> src)
        {
            var dest = new ArrowGraphModel
            {
                Edges = [.. src.Edges.Select(ToActivityEdgeModel)],
                Nodes = [.. src.Nodes.Select(ToEventNodeModel)],
                IsStale = false
            };
            return dest;
        }

        public partial Graph<int, IDependentActivity, IEvent<int>> ToArrowGraph(
            ArrowGraphModel src);

        public VertexGraphModel ToVertexGraphModel(
            Graph<int, IEvent<int>, IDependentActivity> src)
        {
            var dest = new VertexGraphModel
            {
                Edges = [.. src.Edges.Select(ToEventEdgeModel)],
                Nodes = [.. src.Nodes.Select(ToActivityNodeModel)],
            };
            return dest;
        }

        public partial Graph<int, IEvent<int>, IDependentActivity> ToVertexGraph(
            VertexGraphModel src);
    }

}
