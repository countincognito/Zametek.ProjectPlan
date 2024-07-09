namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public record ResourceScheduleModel
    {
        public ResourceModel? Resource { get; init; }

        public List<ScheduledActivityModel> ScheduledActivities { get; init; } = [];

        public int FinishTime { get; init; }
    }
}
