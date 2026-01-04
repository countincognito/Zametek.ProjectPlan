using AutoMapper;

namespace Zametek.Data.ProjectPlan.v0_4_2
{
    public static class Converter
    {
        public static ProjectModel Upgrade(
            IMapper mapper,
            v0_4_1.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(project);

            return new ProjectModel
            {
                ProjectStart = project.ProjectStart,
                Today = project.ProjectStart,
                DependentActivities = mapper.Map<List<v0_4_0.DependentActivityModel>, List<DependentActivityModel>>(project.DependentActivities),
                ArrowGraphSettings = project.ArrowGraphSettings ?? new(),
                ResourceSettings = project.ResourceSettings ?? new(),
                WorkStreamSettings = project.WorkStreamSettings ?? new(),
                DisplaySettings = project.DisplaySettings ?? new(),
                GraphCompilation = mapper.Map<v0_4_0.GraphCompilationModel, GraphCompilationModel>(project.GraphCompilation ?? new v0_4_0.GraphCompilationModel()),
                ArrowGraph = project.ArrowGraph ?? new(),
                HasStaleOutputs = project.HasStaleOutputs,
            };
        }
    }
}
