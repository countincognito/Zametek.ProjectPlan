namespace Zametek.Data.ProjectPlan.v0_3_1
{
    public static class Converter
    {
        public static ProjectModel Upgrade(
            VersionMapper mapper,
            v0_3_0.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(project);

            return new ProjectModel
            {
                ProjectStart = project.ProjectStart,
                DependentActivities = project.DependentActivities ?? [],
                ArrowGraphSettings = project.ArrowGraphSettings ?? new v0_1_0.ArrowGraphSettingsModel(),
                ResourceSettings = mapper.FromV0_1_0ToV0_3_1(project.ResourceSettings ?? new v0_1_0.ResourceSettingsModel()),
                GraphCompilation = mapper.FromV0_3_0ToV0_3_1(project.GraphCompilation ?? new v0_3_0.GraphCompilationModel()),
                ArrowGraph = project.ArrowGraph ?? new v0_3_0.ArrowGraphModel(),
                HasStaleOutputs = project.HasStaleOutputs,
            };
        }
    }
}
