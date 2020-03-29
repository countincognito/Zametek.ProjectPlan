using AutoMapper;
using System.Collections.Generic;
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
                });

            CreateMap<DependentActivityModel, Activity<int, int>>()
                .ConstructUsing(src => new Activity<int, int>(src.Activity.Id, src.Activity.Duration))
                .BeforeMap((src, dest, ctx) =>
                {
                    ctx.Mapper.Map<ActivityModel, Activity<int, int>>(src.Activity, dest);
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
                });

            CreateMap<ResourceScheduleModel, ResourceSchedule<int, int>>()
                .ConstructUsing((src, ctx) => new ResourceSchedule<int, int>(ctx.Mapper.Map<ResourceModel, Resource<int>>(src.Resource), ctx.Mapper.Map<IEnumerable<ScheduledActivityModel>, IEnumerable<ScheduledActivity<int>>>(src.ScheduledActivities), src.FinishTime))
                .ReverseMap();

            CreateMap<ScheduledActivityModel, ScheduledActivity<int>>()
                .ConstructUsing(src => new ScheduledActivity<int>(src.Id, src.Name, src.Duration, src.StartTime, src.FinishTime))
                .ReverseMap();

            CreateMap<GraphCompilationErrorsModel, GraphCompilationErrors<int>>()
                .ConstructUsing((src, ctx) => new GraphCompilationErrors<int>(src.AllResourcesExplicitTargetsButNotAllActivitiesTargeted, ctx.Mapper.Map<IEnumerable<CircularDependencyModel>, IEnumerable<CircularDependency<int>>>(src.CircularDependencies), src.MissingDependencies))
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
                })
                .ReverseMap();







        }
    }
}
