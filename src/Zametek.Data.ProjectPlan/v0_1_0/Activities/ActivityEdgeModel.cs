namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public record ActivityEdgeModel
    {
        public ActivityModel? Content { get; init; }
    }
}
