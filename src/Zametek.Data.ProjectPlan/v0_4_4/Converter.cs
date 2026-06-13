namespace Zametek.Data.ProjectPlan.v0_4_4
{
    public static class Converter
    {
        public static ProjectModel Upgrade(
            VersionMapper mapper,
            v0_4_3.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(project);

            List<DependentActivityModel> activities = [.. project.DependentActivities.Select(mapper.FromV0_4_3ToV0_4_4)];
            GraphCompilationModel graphCompilation = mapper.FromV0_4_3ToV0_4_4(project.GraphCompilation ?? new v0_4_3.GraphCompilationModel());

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

            ResourceSettingsModel resourceSettings = mapper.FromV0_4_0ToV0_4_4(project.ResourceSettings ?? new v0_4_0.ResourceSettingsModel());

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

            DisplaySettingsModel displaySettings = mapper.FromV0_4_1ToV0_4_4(project.DisplaySettings ?? new());

            return new ProjectModel
            {
                ProjectStart = project.ProjectStart,
                Today = project.Today,
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
            VersionMapper mapper,
            v0_4_1.AppSettingsModel appSettingsModel)
        {
            AppSettingsModel appSettings = mapper.FromV0_4_1ToV0_4_4(appSettingsModel);
            return appSettings;
        }
    }
}
