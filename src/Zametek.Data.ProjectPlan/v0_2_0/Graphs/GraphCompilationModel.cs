namespace Zametek.Data.ProjectPlan.v0_2_0
{
    [Serializable]
    public record GraphCompilationModel
    {
        public List<v0_1_0.DependentActivityModel> DependentActivities { get; init; } = new List<v0_1_0.DependentActivityModel>();

        public List<v0_1_0.ResourceScheduleModel> ResourceSchedules { get; init; } = new List<v0_1_0.ResourceScheduleModel>();

        public GraphCompilationErrorsModel? Errors { get; init; }

        public int CyclomaticComplexity { get; init; }

        public int Duration { get; init; }
    }
}
