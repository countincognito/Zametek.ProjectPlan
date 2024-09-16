namespace Zametek.Data.ProjectPlan.v0_3_2
{
    [Serializable]
    public record ActivityEdgeModel
    {
        public ActivityModel Content { get; init; } = new ActivityModel();
    }
}
