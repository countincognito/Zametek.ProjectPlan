using AutoMapper;
using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_2_1
{
    public static class Converter
    {
        public static ProjectPlanModel Upgrade(IMapper mapper, v0_2_0.ProjectPlanModel projectPlan)
        {
            if (mapper == null)
            {
                throw new ArgumentNullException(nameof(mapper));
            }
            if (projectPlan == null)
            {
                throw new ArgumentNullException(nameof(projectPlan));
            }

            GraphCompilationErrorsModel errors = null;

            if (projectPlan.GraphCompilation.Errors != null)
            {
                errors = new GraphCompilationErrorsModel
                {
                    AllResourcesExplicitTargetsButNotAllActivitiesTargeted = projectPlan.GraphCompilation.Errors.AllResourcesExplicitTargetsButNotAllActivitiesTargeted,
                    CircularDependencies = projectPlan.GraphCompilation.Errors.CircularDependencies,
                    MissingDependencies = projectPlan.GraphCompilation.Errors.MissingDependencies,
                    InvalidConstraints = new List<int>(),
                };
            }

            return new ProjectPlanModel
            {
                ProjectStart = projectPlan.ProjectStart,
                DependentActivities = mapper.Map<List<v0_1_0.DependentActivityModel>, List<DependentActivityModel>>(projectPlan.DependentActivities),
                ArrowGraphSettings = projectPlan.ArrowGraphSettings,
                ResourceSettings = projectPlan.ResourceSettings,
                GraphCompilation = new GraphCompilationModel
                {
                    DependentActivities = mapper.Map<List<v0_1_0.DependentActivityModel>, List<DependentActivityModel>>(projectPlan.GraphCompilation.DependentActivities),
                    ResourceSchedules = mapper.Map<List<v0_1_0.ResourceScheduleModel>, List<ResourceScheduleModel>>(projectPlan.GraphCompilation.ResourceSchedules),
                    Errors = errors,
                    CyclomaticComplexity = projectPlan.GraphCompilation.CyclomaticComplexity,
                    Duration = projectPlan.GraphCompilation.Duration,
                },
                ArrowGraph = mapper.Map<v0_1_0.ArrowGraphModel, ArrowGraphModel>(projectPlan.ArrowGraph),
                HasStaleOutputs = projectPlan.HasStaleOutputs,
            };
        }
    }
}
