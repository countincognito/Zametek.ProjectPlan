namespace Zametek.Data.ProjectPlan.v0_3_2
{
    public static class Converter
    {
        public static ProjectModel Upgrade(
            VersionMapper mapper,
            v0_3_1.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(project);

            return new ProjectModel
            {
                ProjectStart = project.ProjectStart,
                DependentActivities = [.. project.DependentActivities.Select(mapper.FromV0_3_0ToV0_3_2)],
                ArrowGraphSettings = project.ArrowGraphSettings ?? new v0_1_0.ArrowGraphSettingsModel(),
                ResourceSettings = mapper.FromV0_3_1ToV0_3_2(project.ResourceSettings ?? new v0_3_1.ResourceSettingsModel()),
                WorkStreamSettings = new WorkStreamSettingsModel(),
                GraphCompilation = mapper.FromV0_3_1ToV0_3_2(project.GraphCompilation ?? new v0_3_1.GraphCompilationModel()),
                ArrowGraph = mapper.FromV0_3_0ToV0_3_2(project.ArrowGraph ?? new v0_3_0.ArrowGraphModel()),
                HasStaleOutputs = project.HasStaleOutputs,
            };
        }
    }
}
