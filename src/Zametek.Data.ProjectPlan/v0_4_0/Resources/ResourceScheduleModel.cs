namespace Zametek.Data.ProjectPlan.v0_4_0
{
    [Serializable]
    public record ResourceScheduleModel
    {
        public ResourceModel Resource { get; init; } = new ResourceModel();

        public List<v0_2_1.ScheduledActivityModel> ScheduledActivities { get; init; } = [];

        public List<bool> ActivityAllocation { get; init; } = [];

        public int FinishTime { get; init; }
    }
}
