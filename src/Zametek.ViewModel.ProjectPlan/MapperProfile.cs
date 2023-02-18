using AutoMapper;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class MapperProfile
        : Profile
    {
        public MapperProfile()
        {
            CreateMap<ResourceModel, Resource<int>>()
                .ConstructUsing(src => new Resource<int>(src.Id, src.Name, src.IsExplicitTarget, src.InterActivityAllocationType, src.UnitCost, src.AllocationOrder))
                .ReverseMap();

            CreateMap<EventModel, Event<int>>()
                .ConstructUsing(src => new Event<int>(src.Id, src.EarliestFinishTime, src.LatestFinishTime))
                .ReverseMap();

            CreateMap<TrackerViewModel, TrackerModel>();

            CreateMap<ActivityModel, Activity<int, int>>()
                .ConstructUsing(src => new Activity<int, int>(src.Id, src.Duration))
                .BeforeMap((src, dest, ctx) =>
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
                })
            .ReverseMap();

            CreateMap<DependentActivityModel, Activity<int, int>>()
                .ConstructUsing(src => new Activity<int, int>(src.Activity.Id, src.Activity.Duration))
                .BeforeMap((src, dest, ctx) =>
                {
                    ctx.Mapper.Map(src.Activity, dest);
                });

            CreateMap<DependentActivityModel, DependentActivity<int, int>>()
                .ConstructUsing(src => new DependentActivity<int, int>(src.Activity.Id, src.Activity.Duration))
                .BeforeMap((src, dest, ctx) =>
                {
                    dest.Dependencies.Clear();
                    foreach (int dependency in src.Dependencies)
                    {
                        dest.Dependencies.Add(dependency);
                    }
                    dest.ResourceDependencies.Clear();
                    foreach (int resourceDependency in src.ResourceDependencies)
                    {
                        dest.ResourceDependencies.Add(resourceDependency);
                    }
                    ctx.Mapper.Map<ActivityModel, Activity<int, int>>(src.Activity, dest);
                })
                .ForMember(src => src.Dependencies, opt => opt.Ignore())
                .ForMember(src => src.ResourceDependencies, opt => opt.Ignore())
                .ReverseMap()
                .ConstructUsing((src, ctx) =>
                {
                    return new DependentActivityModel
                    {
                        Activity = ctx.Mapper.Map<Activity<int, int>, ActivityModel>(src)
                    };
                })
                .BeforeMap((src, dest, ctx) =>
                {
                    dest.Dependencies.Clear();
                    foreach (int dependencyId in src.Dependencies)
                    {
                        dest.Dependencies.Add(dependencyId);
                    }

                    dest.ResourceDependencies.Clear();
                    foreach (int resourceDependencyId in src.ResourceDependencies)
                    {
                        dest.ResourceDependencies.Add(resourceDependencyId);
                    }
                })
                .ForMember(src => src.Dependencies, opt => opt.Ignore())
                .ForMember(src => src.ResourceDependencies, opt => opt.Ignore());

            CreateMap<ManagedActivityViewModel, ActivityModel>();

            CreateMap<ManagedActivityViewModel, DependentActivityModel>()
                .ConstructUsing((src, ctx) =>
                {
                    return new DependentActivityModel
                    {
                        Activity = ctx.Mapper.Map<ManagedActivityViewModel, ActivityModel>(src)
                    };
                })
                .BeforeMap((src, dest, ctx) =>
                {
                    dest.Dependencies.Clear();
                    foreach (int dependencyId in src.Dependencies)
                    {
                        dest.Dependencies.Add(dependencyId);
                    }

                    dest.ResourceDependencies.Clear();
                    foreach (int resourceDependencyId in src.ResourceDependencies)
                    {
                        dest.ResourceDependencies.Add(resourceDependencyId);
                    }
                })
                .ForMember(src => src.Dependencies, opt => opt.Ignore())
                .ForMember(src => src.ResourceDependencies, opt => opt.Ignore());

            CreateMap<ResourceScheduleModel, IResourceSchedule<int, int>>()
                .ConstructUsing((src, ctx) =>
                {
                    ResourceScheduleBuilder<int, int> resourceScheduleBuilder =
                        src.Resource is null ? new ResourceScheduleBuilder<int, int>() : new ResourceScheduleBuilder<int, int>(ctx.Mapper.Map<ResourceModel, Resource<int>>(src.Resource));

                    IEnumerable<ScheduledActivity<int>> scheduledActivities = ctx.Mapper.Map<IEnumerable<ScheduledActivityModel>, IEnumerable<ScheduledActivity<int>>>(src.ScheduledActivities);

                    foreach (ScheduledActivity<int> scheduledActivity in scheduledActivities)
                    {
                        resourceScheduleBuilder.AppendActivityWithoutChecks(scheduledActivity);
                    }

                    return resourceScheduleBuilder.ToResourceSchedule(src.FinishTime);
                });

            CreateMap<ResourceScheduleModel, ResourceSchedule<int, int>>()
                .ConstructUsing((src, ctx) => (ResourceSchedule<int, int>)ctx.Mapper.Map<ResourceScheduleModel, IResourceSchedule<int, int>>(src));

            CreateMap<ResourceSchedule<int, int>, ResourceScheduleModel>()
                .ForMember(src => src.Resource, opt => opt.Condition(src => src.Resource is not null));

            CreateMap<ScheduledActivityModel, ScheduledActivity<int>>()
                .ConstructUsing(src => new ScheduledActivity<int>(src.Id, src.Name, src.HasNoCost, src.Duration, src.StartTime, src.FinishTime));

            CreateMap<ScheduledActivityModel, IScheduledActivity<int>>()
                .ConstructUsing((src, ctx) => ctx.Mapper.Map<ScheduledActivityModel, ScheduledActivity<int>>(src));

            CreateMap<ScheduledActivity<int>, ScheduledActivityModel>();

            CreateMap<GraphCompilationErrorModel, GraphCompilationError>()
                .ConstructUsing(src => new GraphCompilationError(src.ErrorCode, src.ErrorMessage))
                .ReverseMap();

            CreateMap<GraphCompilationErrorModel, IGraphCompilationError>()
                .ConstructUsing((src, ctx) => ctx.Mapper.Map<GraphCompilationErrorModel, GraphCompilationError>(src));

            CreateMap<GraphCompilationModel, GraphCompilation<int, int, DependentActivity<int, int>>>()
                .ConstructUsing((src, ctx) =>
                {
                    var dependentActivities = ctx.Mapper.Map<IEnumerable<DependentActivityModel>, IEnumerable<DependentActivity<int, int>>>(src.DependentActivities);
                    var resourceSchedules = ctx.Mapper.Map<IEnumerable<ResourceScheduleModel>, IEnumerable<ResourceSchedule<int, int>>>(src.ResourceSchedules);
                    var compilationErrors = ctx.Mapper.Map<IEnumerable<GraphCompilationErrorModel>, IEnumerable<GraphCompilationError>>(src.CompilationErrors);

                    return new GraphCompilation<int, int, DependentActivity<int, int>>(
                        dependentActivities,
                        resourceSchedules,
                        compilationErrors);
                });

            CreateMap<IGraphCompilation<int, int, IDependentActivity<int, int>>, GraphCompilationModel>();

            CreateMap<ActivityEdgeModel, Edge<int, IDependentActivity<int, int>>>()
                .ConstructUsing((src, ctx) => new Edge<int, IDependentActivity<int, int>>(ctx.Mapper.Map<ActivityModel, DependentActivity<int, int>>(src.Content)))
                .ReverseMap();

            CreateMap<EventNodeModel, Node<int, IEvent<int>>>()
                .ConstructUsing((src, ctx) => new Node<int, IEvent<int>>(src.NodeType, ctx.Mapper.Map<EventModel, Event<int>>(src.Content)))
                .BeforeMap((src, dest) =>
                {
                    if (src.NodeType != NodeType.Start && src.NodeType != NodeType.Isolated)
                    {
                        dest.IncomingEdges.Clear();
                        foreach (int incomingEdgeId in src.IncomingEdges)
                        {
                            dest.IncomingEdges.Add(incomingEdgeId);
                        }
                    }

                    if (src.NodeType != NodeType.End && src.NodeType != NodeType.Isolated)
                    {
                        dest.OutgoingEdges.Clear();
                        foreach (int outgoingEdgeId in src.OutgoingEdges)
                        {
                            dest.OutgoingEdges.Add(outgoingEdgeId);
                        }
                    }
                })
                .ForMember(src => src.IncomingEdges, opt => opt.Ignore())
                .ForMember(src => src.OutgoingEdges, opt => opt.Ignore())
                .ReverseMap()
                .BeforeMap((src, dest) =>
                {
                    dest.IncomingEdges.Clear();
                    if (src.NodeType != NodeType.Start && src.NodeType != NodeType.Isolated)
                    {
                        foreach (int incomingEdgeId in src.IncomingEdges)
                        {
                            dest.IncomingEdges.Add(incomingEdgeId);
                        }
                    }

                    dest.OutgoingEdges.Clear();
                    if (src.NodeType != NodeType.End && src.NodeType != NodeType.Isolated)
                    {
                        foreach (int outgoingEdgeId in src.OutgoingEdges)
                        {
                            dest.OutgoingEdges.Add(outgoingEdgeId);
                        }
                    }
                })
                .ForMember(src => src.IncomingEdges, opt => opt.Ignore())
                .ForMember(src => src.OutgoingEdges, opt => opt.Ignore());

            CreateMap<ArrowGraphModel, Graph<int, IDependentActivity<int, int>, IEvent<int>>>()
                .ReverseMap();
        }
    }
}
