namespace Zametek.Data.ProjectPlan.v0_4_2
{
    public static class Converter
    {
        public static ProjectModel Upgrade(
            VersionMapper mapper,
            v0_4_1.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(project);

            return new ProjectModel
            {
                ProjectStart = project.ProjectStart,
                Today = project.ProjectStart,
                DependentActivities = [.. project.DependentActivities.Select(mapper.FromV0_4_0ToV0_4_2)],
                ArrowGraphSettings = project.ArrowGraphSettings ?? new(),
                ResourceSettings = project.ResourceSettings ?? new(),
                WorkStreamSettings = project.WorkStreamSettings ?? new(),
                DisplaySettings = project.DisplaySettings ?? new(),
                GraphCompilation = mapper.FromV0_4_0ToV0_4_2(project.GraphCompilation ?? new v0_4_0.GraphCompilationModel()),
                ArrowGraph = project.ArrowGraph ?? new(),
                HasStaleOutputs = project.HasStaleOutputs,
            };
        }
    }
}
