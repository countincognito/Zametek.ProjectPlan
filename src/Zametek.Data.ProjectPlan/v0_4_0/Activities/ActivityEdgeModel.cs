namespace Zametek.Data.ProjectPlan.v0_4_0
{
    [Serializable]
    public record ActivityEdgeModel
    {
        public ActivityModel Content { get; init; } = new ActivityModel();
    }
}
