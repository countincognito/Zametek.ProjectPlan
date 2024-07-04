namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record GraphCompilationModel
    {
        public List<DependentActivityModel> DependentActivities { get; init; } = [];

        public List<ResourceScheduleModel> ResourceSchedules { get; init; } = [];

        public List<WorkStreamModel> WorkStreams { get; init; } = [];

        public List<GraphCompilationErrorModel> CompilationErrors { get; init; } = [];

        public int CyclomaticComplexity { get; init; }

        public int Duration { get; init; }
    }
}
