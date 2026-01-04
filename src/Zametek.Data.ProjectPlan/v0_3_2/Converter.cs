using AutoMapper;

namespace Zametek.Data.ProjectPlan.v0_3_2
{
    public static class Converter
    {
        public static ProjectModel Upgrade(
            IMapper mapper,
            v0_3_1.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(project);

            return new ProjectModel
            {
                ProjectStart = project.ProjectStart,
                DependentActivities = mapper.Map<List<v0_3_0.DependentActivityModel>, List<DependentActivityModel>>(project.DependentActivities),
                ArrowGraphSettings = project.ArrowGraphSettings ?? new v0_1_0.ArrowGraphSettingsModel(),
                ResourceSettings = mapper.Map<v0_3_1.ResourceSettingsModel, ResourceSettingsModel>(project.ResourceSettings ?? new v0_3_1.ResourceSettingsModel()),
                WorkStreamSettings = new WorkStreamSettingsModel(),
                GraphCompilation = mapper.Map<v0_3_1.GraphCompilationModel, GraphCompilationModel>(project.GraphCompilation ?? new v0_3_1.GraphCompilationModel()),
                ArrowGraph = mapper.Map<v0_3_0.ArrowGraphModel, ArrowGraphModel>(project.ArrowGraph ?? new v0_3_0.ArrowGraphModel()),
                HasStaleOutputs = project.HasStaleOutputs,
            };
        }
    }
}
