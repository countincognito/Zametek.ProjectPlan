namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ResourceScheduleModel
    {
        public ResourceModel Resource { get; init; } = new ResourceModel();

        public List<ScheduledActivityModel> ScheduledActivities { get; init; } = new List<ScheduledActivityModel>();

        public List<bool> ActivityAllocation { get; init; } = new List<bool>();

        public int FinishTime { get; init; }
    }
}
