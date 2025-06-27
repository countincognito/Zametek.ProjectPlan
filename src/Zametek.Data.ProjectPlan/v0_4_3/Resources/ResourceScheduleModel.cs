namespace Zametek.Data.ProjectPlan.v0_4_3
{
    [Serializable]
    public record ResourceScheduleModel
    {
        public v0_4_0.ResourceModel Resource { get; init; } = new v0_4_0.ResourceModel();

        public List<v0_4_0.ScheduledActivityModel> ScheduledActivities { get; init; } = [];

        public List<bool> ActivityAllocation { get; init; } = [];

        public List<bool> CostAllocation { get; init; } = [];

        public List<bool> EffortAllocation { get; init; } = [];

        public int StartTime { get; init; }

        public int FinishTime { get; init; }
    }
}
