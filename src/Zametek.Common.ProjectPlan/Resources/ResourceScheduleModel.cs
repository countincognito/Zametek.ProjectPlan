namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ResourceScheduleModel
    {
        public ResourceModel Resource { get; init; } = new ResourceModel();

        public List<ScheduledActivityModel> ScheduledActivities { get; init; } = [];

        public List<bool> ActivityAllocation { get; init; } = [];

        public int FinishTime { get; init; }
    }
}
