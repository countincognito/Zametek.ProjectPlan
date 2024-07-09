namespace Zametek.Data.ProjectPlan.v0_2_1
{
    [Serializable]
    public record ResourceScheduleModel
    {
        public ResourceModel? Resource { get; init; }

        public List<ScheduledActivityModel> ScheduledActivities { get; init; } = [];

        public int FinishTime { get; init; }
    }
}
