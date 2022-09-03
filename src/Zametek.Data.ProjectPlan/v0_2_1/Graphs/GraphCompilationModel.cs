namespace Zametek.Data.ProjectPlan.v0_2_1
{
    [Serializable]
    public record GraphCompilationModel
    {
        public List<DependentActivityModel> DependentActivities { get; init; } = new List<DependentActivityModel>();

        public List<ResourceScheduleModel> ResourceSchedules { get; init; } = new List<ResourceScheduleModel>();

        public GraphCompilationErrorsModel? Errors { get; init; }

        public int CyclomaticComplexity { get; init; }

        public int Duration { get; init; }
    }
}
