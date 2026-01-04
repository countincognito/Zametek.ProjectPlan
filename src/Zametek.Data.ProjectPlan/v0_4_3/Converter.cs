using AutoMapper;

namespace Zametek.Data.ProjectPlan.v0_4_3
{
    public static class Converter
    {
        public static ProjectModel Upgrade(
            IMapper mapper,
            v0_4_2.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(project);

            List<DependentActivityModel> activities = mapper.Map<List<v0_4_2.DependentActivityModel>, List<DependentActivityModel>>(project.DependentActivities);

            for (int i = 0; i < activities.Count; i++)
            {
                activities[i].PlanningDependencies.Clear();
                activities[i].PlanningDependencies.AddRange(project.DependentActivities[i].ManualDependencies);
            }

            return new ProjectModel
            {
                ProjectStart = project.ProjectStart,
                Today = project.ProjectStart,
                DependentActivities = activities,
                ArrowGraphSettings = project.ArrowGraphSettings ?? new(),
                ResourceSettings = project.ResourceSettings ?? new(),
                WorkStreamSettings = project.WorkStreamSettings ?? new(),
                DisplaySettings = project.DisplaySettings ?? new(),
                GraphCompilation = mapper.Map<v0_4_2.GraphCompilationModel, GraphCompilationModel>(project.GraphCompilation ?? new v0_4_2.GraphCompilationModel()),
                ArrowGraph = project.ArrowGraph ?? new(),
                HasStaleOutputs = project.HasStaleOutputs,
            };
        }
    }
}
