using System;
using System.Collections.Generic;
using System.Linq;
using Zametek.Maths.Graphs;

namespace Zametek.Common.Project.v0_2_0
{
    public static class DtoConverter
    {
        public static GraphCompilation<int, IDependentActivity<int>> FromDto(GraphCompilationDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            if (dto.Errors == null)
            {
                return new GraphCompilation<int, IDependentActivity<int>>(
                    dto.DependentActivities.Select(x => v0_1_0.DtoConverter.FromDto(x)),
                    dto.ResourceSchedules.Select(x => v0_1_0.DtoConverter.FromDto(x)));
            }
            else
            {
                return new GraphCompilation<int, IDependentActivity<int>>(
                    dto.DependentActivities.Select(x => v0_1_0.DtoConverter.FromDto(x)),
                    dto.ResourceSchedules.Select(x => v0_1_0.DtoConverter.FromDto(x)),
                    FromDto(dto.Errors));
            }
        }

        public static GraphCompilationErrors<int> FromDto(GraphCompilationErrorsDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }
            return new GraphCompilationErrors<int>(
                dto.AllResourcesExplicitTargetsButNotAllActivitiesTargeted,
                dto.CircularDependencies.Select(x => v0_1_0.DtoConverter.FromDto(x)),
                dto.MissingDependencies);
        }

        public static GraphCompilationDto ToDto(
            GraphCompilation<int, IDependentActivity<int>> graphCompilation,
            int cyclomaticComplexity,
            int duration)
        {
            if (graphCompilation == null)
            {
                throw new ArgumentNullException(nameof(graphCompilation));
            }

            if (graphCompilation.Errors == null)
            {
                return new GraphCompilationDto
                {
                    DependentActivities = graphCompilation.DependentActivities != null ? graphCompilation.DependentActivities.Select(x => v0_1_0.DtoConverter.ToDto(x)).ToList() : new List<v0_1_0.DependentActivityDto>(),
                    ResourceSchedules = graphCompilation.ResourceSchedules != null ? graphCompilation.ResourceSchedules.Select(x => v0_1_0.DtoConverter.ToDto(x)).ToList() : new List<v0_1_0.ResourceScheduleDto>(),
                    CyclomaticComplexity = cyclomaticComplexity,
                    Duration = duration
                };
            }
            else
            {
                return new GraphCompilationDto
                {
                    DependentActivities = graphCompilation.DependentActivities != null ? graphCompilation.DependentActivities.Select(x => v0_1_0.DtoConverter.ToDto(x)).ToList() : new List<v0_1_0.DependentActivityDto>(),
                    ResourceSchedules = graphCompilation.ResourceSchedules != null ? graphCompilation.ResourceSchedules.Select(x => v0_1_0.DtoConverter.ToDto(x)).ToList() : new List<v0_1_0.ResourceScheduleDto>(),
                    Errors = ToDto(graphCompilation.Errors),
                    CyclomaticComplexity = cyclomaticComplexity,
                    Duration = duration
                };
            }
        }

        public static GraphCompilationErrorsDto ToDto(GraphCompilationErrors<int> graphCompilationErrors)
        {
            if (graphCompilationErrors == null)
            {
                throw new ArgumentNullException(nameof(graphCompilationErrors));
            }
            return new GraphCompilationErrorsDto
            {
                AllResourcesExplicitTargetsButNotAllActivitiesTargeted = graphCompilationErrors.AllResourcesExplicitTargetsButNotAllActivitiesTargeted,
                CircularDependencies = graphCompilationErrors.CircularDependencies != null ? graphCompilationErrors.CircularDependencies.Select(x => v0_1_0.DtoConverter.ToDto(x)).ToList() : new List<v0_1_0.CircularDependencyDto>(),
                MissingDependencies = graphCompilationErrors.MissingDependencies != null ? graphCompilationErrors.MissingDependencies.ToList() : new List<int>(),
            };
        }

        public static ProjectPlanDto Upgrade(v0_1_0.ProjectPlanDto projectPlanDto)
        {
            if (projectPlanDto == null)
            {
                throw new ArgumentNullException(nameof(projectPlanDto));
            }
            return new ProjectPlanDto
            {
                ProjectStart = projectPlanDto.ProjectStart,
                DependentActivities = projectPlanDto.DependentActivities,
                ArrowGraphSettings = projectPlanDto.ArrowGraphSettings,
                ResourceSettings = projectPlanDto.ResourceSettings,
                GraphCompilation = ToDto(
                    v0_1_0.DtoConverter.FromDto(projectPlanDto.GraphCompilation),
                    projectPlanDto.GraphCompilation.CyclomaticComplexity,
                    projectPlanDto.GraphCompilation.Duration),
                ArrowGraph = projectPlanDto.ArrowGraph,
                HasStaleOutputs = projectPlanDto.HasStaleOutputs,
            };
        }
    }
}
