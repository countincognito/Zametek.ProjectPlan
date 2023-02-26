namespace Zametek.Data.ProjectPlan.v0_3_1
{
    [Serializable]
    public record GraphCompilationModel
    {
        public List<v0_3_0.DependentActivityModel> DependentActivities { get; init; } = new List<v0_3_0.DependentActivityModel>();

        public List<ResourceScheduleModel> ResourceSchedules { get; init; } = new List<ResourceScheduleModel>();

        public List<v0_3_0.GraphCompilationErrorModel> CompilationErrors { get; init; } = new List<v0_3_0.GraphCompilationErrorModel>();

        public int CyclomaticComplexity { get; init; }

        public int Duration { get; init; }
    }
}
