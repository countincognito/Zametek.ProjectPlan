using AutoMapper;

namespace Zametek.Data.ProjectPlan.v0_4_4
{
    public static class Converter
    {
        public static ProjectPlanModel Upgrade(
            IMapper mapper,
            v0_4_3.ProjectPlanModel projectPlan)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(projectPlan);

            List<DependentActivityModel> activities = mapper.Map<List<v0_4_3.DependentActivityModel>, List<DependentActivityModel>>(projectPlan.DependentActivities);
            GraphCompilationModel graphCompilation = mapper.Map<v0_4_3.GraphCompilationModel, GraphCompilationModel>(projectPlan.GraphCompilation ?? new v0_4_3.GraphCompilationModel());

            List<ResourceScheduleModel> resourceSchedules = [];

            for (int i = 0; i < graphCompilation.ResourceSchedules.Count; i++)
            {
                ResourceScheduleModel resourceSchedule = graphCompilation.ResourceSchedules[i];
                ResourceModel resource = resourceSchedule.Resource ?? new ResourceModel();
                resource = resource with { UnitBilling = resource.UnitCost };
                resourceSchedule = resourceSchedule with { Resource = resource };
                resourceSchedule.BillingAllocation.Clear();
                resourceSchedule.BillingAllocation.AddRange(resourceSchedule.CostAllocation);
                resourceSchedules.Add(resourceSchedule);
            }

            graphCompilation = graphCompilation with { ResourceSchedules = resourceSchedules };

            ResourceSettingsModel resourceSettings = mapper.Map<v0_4_0.ResourceSettingsModel, ResourceSettingsModel>(projectPlan.ResourceSettings ?? new v0_4_0.ResourceSettingsModel());

            List<ResourceModel> resources = [];

            for (int i = 0; i < resourceSettings.Resources.Count; i++)
            {
                ResourceModel resource = resourceSettings.Resources[i];
                resource = resource with { UnitBilling = resource.UnitCost };
                resources.Add(resource);
            }

            resourceSettings = resourceSettings with
            {
                DefaultUnitBilling = resourceSettings.DefaultUnitCost,
                Resources = resources,
            };

            var plan = new ProjectPlanModel
            {
                ProjectStart = projectPlan.ProjectStart,
                Today = projectPlan.ProjectStart,
                DependentActivities = activities,
                ArrowGraphSettings = projectPlan.ArrowGraphSettings ?? new(),
                ResourceSettings = resourceSettings,
                WorkStreamSettings = projectPlan.WorkStreamSettings ?? new(),
                DisplaySettings = projectPlan.DisplaySettings ?? new(),
                GraphCompilation = graphCompilation,
                ArrowGraph = projectPlan.ArrowGraph ?? new(),
                HasStaleOutputs = projectPlan.HasStaleOutputs,
            };

            return plan;
        }
    }
}
