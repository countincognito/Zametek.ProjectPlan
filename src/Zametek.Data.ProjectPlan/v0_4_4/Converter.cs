using AutoMapper;

namespace Zametek.Data.ProjectPlan.v0_4_4
{
    public static class Converter
    {
        public static ProjectModel Upgrade(
            IMapper mapper,
            v0_4_3.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(project);

            List<DependentActivityModel> activities = mapper.Map<List<v0_4_3.DependentActivityModel>, List<DependentActivityModel>>(project.DependentActivities);
            GraphCompilationModel graphCompilation = mapper.Map<v0_4_3.GraphCompilationModel, GraphCompilationModel>(project.GraphCompilation ?? new v0_4_3.GraphCompilationModel());

            List<ResourceScheduleModel> resourceSchedules = [];

            for (int i = 0; i < graphCompilation.ResourceSchedules.Count; i++)
            {
                ResourceScheduleModel resourceSchedule = graphCompilation.ResourceSchedules[i];
                ResourceModel resource = resourceSchedule.Resource ?? new ResourceModel();
                resource = resource with { UnitBilling = resource.UnitCost, Notes = string.Empty };
                resourceSchedule = resourceSchedule with { Resource = resource };
                resourceSchedule.BillingAllocation.Clear();
                resourceSchedule.BillingAllocation.AddRange(resourceSchedule.CostAllocation);
                resourceSchedules.Add(resourceSchedule);
            }

            graphCompilation = graphCompilation with { ResourceSchedules = resourceSchedules };

            ResourceSettingsModel resourceSettings = mapper.Map<v0_4_0.ResourceSettingsModel, ResourceSettingsModel>(project.ResourceSettings ?? new v0_4_0.ResourceSettingsModel());

            List<ResourceModel> resources = [];

            for (int i = 0; i < resourceSettings.Resources.Count; i++)
            {
                ResourceModel resource = resourceSettings.Resources[i];
                resource = resource with { UnitBilling = resource.UnitCost, Notes = string.Empty };
                resources.Add(resource);
            }

            resourceSettings = resourceSettings with
            {
                DefaultUnitBilling = resourceSettings.DefaultUnitCost,
                Resources = resources,
            };

            DisplaySettingsModel displaySettings = mapper.Map<v0_4_1.DisplaySettingsModel, DisplaySettingsModel>(project.DisplaySettings ?? new());

            return new ProjectModel
            {
                ProjectStart = project.ProjectStart,
                Today = project.ProjectStart,
                DependentActivities = activities,
                ArrowGraphSettings = project.ArrowGraphSettings ?? new(),
                ResourceSettings = resourceSettings,
                WorkStreamSettings = project.WorkStreamSettings ?? new(),
                DisplaySettings = displaySettings,
                GraphCompilation = graphCompilation,
                ArrowGraph = project.ArrowGraph ?? new(),
                HasStaleOutputs = project.HasStaleOutputs,
            };
        }

        public static AppSettingsModel Upgrade(
            IMapper mapper,
            v0_4_1.AppSettingsModel appSettingsModel)
        {
            AppSettingsModel appSettings = mapper.Map<v0_4_1.AppSettingsModel, AppSettingsModel>(appSettingsModel);
            return appSettings;
        }
    }
}
