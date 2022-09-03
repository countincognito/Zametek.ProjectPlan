namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record GraphCompilationModel
    {
        public List<DependentActivityModel> DependentActivities { get; init; } = new List<DependentActivityModel>();

        public List<ResourceScheduleModel> ResourceSchedules { get; init; } = new List<ResourceScheduleModel>();

        public List<GraphCompilationErrorModel> CompilationErrors { get; init; } = new List<GraphCompilationErrorModel>();

        public int CyclomaticComplexity { get; init; }

        public int Duration { get; init; }
    }
}
