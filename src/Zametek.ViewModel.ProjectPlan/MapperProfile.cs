using AutoMapper;
using System.Collections.Generic;
using System.Linq;
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
                    ctx.Mapper.Map<ActivityModel, Activity<int, int>>(src.Activity, dest);
                });
            //.ReverseMap();

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
                .BeforeMap((src, dest, ctx) =>
                {
                    dest.Dependencies = src.Dependencies.ToList();
                    dest.ResourceDependencies = src.ResourceDependencies.ToList();
                    dest.Activity = ctx.Mapper.Map<Activity<int, int>, ActivityModel>(src);
                })
                .ForMember(src => src.Dependencies, opt => opt.Ignore())
                .ForMember(src => src.ResourceDependencies, opt => opt.Ignore());

            CreateMap<ManagedActivityViewModel, ActivityModel>();

            CreateMap<ManagedActivityViewModel, DependentActivityModel>()
                .BeforeMap((src, dest, ctx) =>
                {
                    dest.Dependencies = src.Dependencies.ToList();
                    dest.ResourceDependencies = src.ResourceDependencies.ToList();
                    dest.Activity = ctx.Mapper.Map<ManagedActivityViewModel, ActivityModel>(src);
                })
                .ForMember(src => src.Dependencies, opt => opt.Ignore())
                .ForMember(src => src.ResourceDependencies, opt => opt.Ignore());

            CreateMap<ResourceScheduleModel, ResourceSchedule<int, int>>()
                .ConstructUsing((src, ctx) =>
                {
                    ResourceScheduleBuilder<int, int> resourceScheduleBuilder =
                        src.Resource is null ? new ResourceScheduleBuilder<int, int>() : new ResourceScheduleBuilder<int, int>(ctx.Mapper.Map<ResourceModel, Resource<int>>(src.Resource));

                    IEnumerable<ScheduledActivity<int>> scheduledActivities = ctx.Mapper.Map<IEnumerable<ScheduledActivityModel>, IEnumerable<ScheduledActivity<int>>>(src.ScheduledActivities);

                    foreach (ScheduledActivity<int> scheduledActivity in scheduledActivities)
                    {
                        resourceScheduleBuilder.AppendActivityWithoutChecks(scheduledActivity);
                    }

                    return resourceScheduleBuilder.ToResourceSchedule(src.FinishTime) as ResourceSchedule<int, int>;
                })
                .ReverseMap();

            CreateMap<ScheduledActivityModel, ScheduledActivity<int>>()
                .ConstructUsing(src => new ScheduledActivity<int>(src.Id, src.Name, src.HasNoCost, src.Duration, src.StartTime, src.FinishTime))
                .ReverseMap();

            CreateMap<GraphCompilationErrorsModel, GraphCompilationErrors<int>>()
                .ConstructUsing((src, ctx) =>
                    new GraphCompilationErrors<int>(
                        src.AllResourcesExplicitTargetsButNotAllActivitiesTargeted,
                        ctx.Mapper.Map<IEnumerable<CircularDependencyModel>,
                        IEnumerable<CircularDependency<int>>>(src.CircularDependencies),
                        src.MissingDependencies,
                        src.InvalidConstraints))
                .ReverseMap();

            CreateMap<CircularDependencyModel, CircularDependency<int>>()
                .ConstructUsing(src => new CircularDependency<int>(src.Dependencies))
                .ReverseMap();

            CreateMap<GraphCompilationModel, GraphCompilation<int, int, DependentActivity<int, int>>>()
                .ConstructUsing((src, ctx) =>
                {
                    if (src.Errors != null)
                    {
                        return new GraphCompilation<int, int, DependentActivity<int, int>>(
                            ctx.Mapper.Map<IEnumerable<DependentActivityModel>, IEnumerable<DependentActivity<int, int>>>(src.DependentActivities),
                            ctx.Mapper.Map<IEnumerable<ResourceScheduleModel>, IEnumerable<ResourceSchedule<int, int>>>(src.ResourceSchedules),
                            ctx.Mapper.Map<GraphCompilationErrorsModel, GraphCompilationErrors<int>>(src.Errors));
                    }
                    else
                    {
                        return new GraphCompilation<int, int, DependentActivity<int, int>>(
                            ctx.Mapper.Map<IEnumerable<DependentActivityModel>, IEnumerable<DependentActivity<int, int>>>(src.DependentActivities),
                            ctx.Mapper.Map<IEnumerable<ResourceScheduleModel>, IEnumerable<ResourceSchedule<int, int>>>(src.ResourceSchedules));
                    }
                });
            //.ReverseMap();


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
                    if (src.NodeType != NodeType.Start && src.NodeType != NodeType.Isolated)
                    {
                        dest.IncomingEdges = src.IncomingEdges.ToList();
                    }
                    else
                    {
                        dest.IncomingEdges = new List<int>();
                    }

                    if (src.NodeType != NodeType.End && src.NodeType != NodeType.Isolated)
                    {
                        dest.OutgoingEdges = src.OutgoingEdges.ToList();
                    }
                    else
                    {
                        dest.OutgoingEdges = new List<int>();
                    }
                })
                .ForMember(src => src.IncomingEdges, opt => opt.Ignore())
                .ForMember(src => src.OutgoingEdges, opt => opt.Ignore());

            CreateMap<ArrowGraphModel, Graph<int, IDependentActivity<int, int>, IEvent<int>>>()
                .ReverseMap();
        }
    }
}
