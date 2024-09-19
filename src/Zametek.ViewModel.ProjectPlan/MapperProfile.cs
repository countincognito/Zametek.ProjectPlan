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
            CreateMap<ResourceModel, Resource<int, int>>()
                .ConstructUsing(src => new Resource<int, int>(src.Id, src.Name, src.IsExplicitTarget, src.IsInactive, src.InterActivityAllocationType, src.UnitCost, src.AllocationOrder, src.InterActivityPhases))
                .ReverseMap();

            CreateMap<EventModel, Event<int>>()
                .ConstructUsing(src => new Event<int>(src.Id, src.EarliestFinishTime, src.LatestFinishTime))
                .ReverseMap();

            // TODO
            //CreateMap<ActivityTrackerViewModel, ActivityTrackerModel>();

            CreateMap<ActivityModel, Activity<int, int, int>>()
                .ConstructUsing(src => new Activity<int, int, int>(src.Id, src.Duration))
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

            CreateMap<DependentActivityModel, Activity<int, int, int>>()
                .ConstructUsing(src => new Activity<int, int, int>(src.Activity.Id, src.Activity.Duration))
                .BeforeMap((src, dest, ctx) =>
                {
                    ctx.Mapper.Map(src.Activity, dest);
                });

            CreateMap<DependentActivityModel, DependentActivity<int, int, int>>()
                .ConstructUsing(src => new DependentActivity<int, int, int>(src.Activity.Id, src.Activity.Duration))
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
                    ctx.Mapper.Map<ActivityModel, Activity<int, int, int>>(src.Activity, dest);
                })
                .ForMember(src => src.Dependencies, opt => opt.Ignore())
                .ForMember(src => src.ResourceDependencies, opt => opt.Ignore())
                .ReverseMap()
                .ConstructUsing((src, ctx) =>
                {
                    return new DependentActivityModel
                    {
                        Activity = ctx.Mapper.Map<Activity<int, int, int>, ActivityModel>(src)
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

            CreateMap<ManagedActivityViewModel, ActivityModel>()
                .BeforeMap((src, dest, ctx) =>
                {
                    dest.Trackers.Clear();
                    dest.Trackers.AddRange(src.Trackers.Trackers);
                })
                .ForMember(src => src.Trackers, opt => opt.Ignore());

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

            // TODO
            //CreateMap<GraphCompilationModel, IEnumerable<IResourceSchedule<int, int, int>>>()
            //    .ConstructUsing((src, ctx) =>
            //    {
            //        var dependentActivities = ctx.Mapper.Map<IEnumerable<DependentActivityModel>, IEnumerable<DependentActivity<int, int, int>>>(src.DependentActivities);

            //        var resourceSchedules = new List<IResourceSchedule<int, int, int>>();

            //        foreach (ResourceScheduleModel resourceScheduleModel in src.ResourceSchedules)
            //        {
            //            ResourceScheduleBuilder<int, int, int> resourceScheduleBuilder =
            //                resourceScheduleModel.Resource is null ? new ResourceScheduleBuilder<int, int, int>() : new ResourceScheduleBuilder<int, int, int>(ctx.Mapper.Map<ResourceModel, Resource<int, int>>(resourceScheduleModel.Resource));

            //            IEnumerable<ScheduledActivity<int>> scheduledActivities = ctx.Mapper.Map<IEnumerable<ScheduledActivityModel>, IEnumerable<ScheduledActivity<int>>>(resourceScheduleModel.ScheduledActivities);

            //            foreach (ScheduledActivity<int> scheduledActivity in scheduledActivities)
            //            {
            //                resourceScheduleBuilder.AppendActivityWithoutChecks(scheduledActivity);
            //            }

            //            IResourceSchedule<int, int, int> resourceSchedule = resourceScheduleBuilder.ToResourceSchedule(
            //                dependentActivities, resourceScheduleModel.FinishTime);

            //            resourceSchedules.Add(resourceSchedule);
            //        }

            //        return resourceSchedules;
            //    });










            CreateMap<ResourceScheduleModel, IResourceSchedule<int, int, int>>()
                .ConstructUsing((src, ctx) =>
                {
                    ResourceScheduleBuilder<int, int, int> resourceScheduleBuilder =
                        src.Resource is null ? new ResourceScheduleBuilder<int, int, int>() : new ResourceScheduleBuilder<int, int, int>(ctx.Mapper.Map<ResourceModel, Resource<int, int>>(src.Resource));

                    IEnumerable<ScheduledActivity<int>> scheduledActivities = ctx.Mapper.Map<IEnumerable<ScheduledActivityModel>, IEnumerable<ScheduledActivity<int>>>(src.ScheduledActivities);

                    foreach (ScheduledActivity<int> scheduledActivity in scheduledActivities)
                    {
                        resourceScheduleBuilder.AppendActivityWithoutChecks(scheduledActivity);
                    }

                    return resourceScheduleBuilder.ToResourceSchedule([], src.FinishTime);
                });

            CreateMap<ResourceScheduleModel, ResourceSchedule<int, int, int>>()
                .ConstructUsing((src, ctx) => (ResourceSchedule<int, int, int>)ctx.Mapper.Map<ResourceScheduleModel, IResourceSchedule<int, int, int>>(src));












            CreateMap<ResourceSchedule<int, int, int>, ResourceScheduleModel>()
                .ForMember(src => src.Resource, opt => opt.Condition(src => src.Resource is not null));





            CreateMap<IGraphCompilation<int, int, int, IDependentActivity<int, int, int>>, IEnumerable<ResourceScheduleModel>>()
                .ConstructUsing((src, ctx) =>
                {
                    var resourceSchedules = new List<ResourceScheduleModel>();

                    foreach (IResourceSchedule<int, int, int> resourceSchedule in src.ResourceSchedules)
                    {
                        // TODO
                        //ResourceScheduleBuilder<int, int, int> resourceScheduleBuilder =
                        //    resourceScheduleModel.Resource is null ? new ResourceScheduleBuilder<int, int, int>() : new ResourceScheduleBuilder<int, int, int>(ctx.Mapper.Map<ResourceModel, Resource<int, int>>(resourceScheduleModel.Resource));

                        //IEnumerable<ScheduledActivity<int>> scheduledActivities = ctx.Mapper.Map<IEnumerable<ScheduledActivityModel>, IEnumerable<ScheduledActivity<int>>>(resourceScheduleModel.ScheduledActivities);

                        //foreach (ScheduledActivity<int> scheduledActivity in scheduledActivities)
                        //{
                        //    resourceScheduleBuilder.AppendActivityWithoutChecks(scheduledActivity);
                        //}

                        //IResourceSchedule<int, int, int> resourceSchedule = resourceScheduleBuilder.ToResourceSchedule(
                        //    src.DependentActivities, resourceScheduleModel.FinishTime);

                        resourceSchedules.Add(ctx.Mapper.Map<IResourceSchedule<int, int, int>, ResourceScheduleModel>(resourceSchedule));
                    }

                    return resourceSchedules;
                });





            CreateMap<WorkStreamModel, WorkStream<int>>()
                .ConstructUsing(src => new WorkStream<int>(src.Id, src.Name, src.IsPhase));

            CreateMap<WorkStreamModel, IWorkStream<int>>()
                .ConstructUsing((src, ctx) => ctx.Mapper.Map<WorkStreamModel, WorkStream<int>>(src));

            CreateMap<WorkStream<int>, WorkStreamModel>();

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

            CreateMap<GraphCompilationModel, GraphCompilation<int, int, int, DependentActivity<int, int, int>>>()
                .ConstructUsing((src, ctx) =>
                {
                    var dependentActivities = ctx.Mapper.Map<IEnumerable<DependentActivityModel>, IEnumerable<DependentActivity<int, int, int>>>(src.DependentActivities);

                    var resourceSchedules = ctx.Mapper.Map<IEnumerable<ResourceScheduleModel>, IEnumerable<ResourceSchedule<int, int, int>>>(src.ResourceSchedules);



                    var workStreams = ctx.Mapper.Map<IEnumerable<WorkStreamModel>, IEnumerable<WorkStream<int>>>(src.WorkStreams);
                    var compilationErrors = ctx.Mapper.Map<IEnumerable<GraphCompilationErrorModel>, IEnumerable<GraphCompilationError>>(src.CompilationErrors);

                    return new GraphCompilation<int, int, int, DependentActivity<int, int, int>>(
                        dependentActivities,
                        resourceSchedules,
                        workStreams,
                        compilationErrors);
                });

            CreateMap<IGraphCompilation<int, int, int, IDependentActivity<int, int, int>>, GraphCompilationModel>();

            CreateMap<ActivityEdgeModel, Edge<int, IDependentActivity<int, int, int>>>()
                .ConstructUsing((src, ctx) => new Edge<int, IDependentActivity<int, int, int>>(ctx.Mapper.Map<ActivityModel, DependentActivity<int, int, int>>(src.Content)))
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

            CreateMap<ArrowGraphModel, Graph<int, IDependentActivity<int, int, int>, IEvent<int>>>()
                .ReverseMap();
        }
    }
}
