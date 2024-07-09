namespace Zametek.Data.ProjectPlan.v0_2_1
{
    [Serializable]
    public record GraphCompilationModel
    {
        public List<DependentActivityModel> DependentActivities { get; init; } = [];

        public List<ResourceScheduleModel> ResourceSchedules { get; init; } = [];

        public GraphCompilationErrorsModel? Errors { get; init; }

        public int CyclomaticComplexity { get; init; }

        public int Duration { get; init; }
    }
}
