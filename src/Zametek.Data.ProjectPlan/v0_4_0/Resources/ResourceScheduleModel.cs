namespace Zametek.Data.ProjectPlan.v0_4_0
{
    [Serializable]
    public record ResourceScheduleModel
    {
        public v0_3_2.ResourceModel Resource { get; init; } = new v0_3_2.ResourceModel();

        public List<v0_2_1.ScheduledActivityModel> ScheduledActivities { get; init; } = [];

        public int FinishTime { get; init; }

        public List<ResourceTrackerModel> Trackers { get; init; } = [];
    }
}
