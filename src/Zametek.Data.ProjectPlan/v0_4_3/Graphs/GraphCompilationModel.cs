﻿namespace Zametek.Data.ProjectPlan.v0_4_3
{
    [Serializable]
    public record GraphCompilationModel
    {
        public List<v0_4_2.DependentActivityModel> DependentActivities { get; init; } = [];

        public List<ResourceScheduleModel> ResourceSchedules { get; init; } = [];

        public List<v0_3_0.GraphCompilationErrorModel> CompilationErrors { get; init; } = [];

        public int CyclomaticComplexity { get; init; }

        public int Duration { get; init; }
    }
}
