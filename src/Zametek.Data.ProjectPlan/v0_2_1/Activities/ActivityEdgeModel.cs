namespace Zametek.Data.ProjectPlan.v0_2_1
{
    [Serializable]
    public record ActivityEdgeModel
    {
        public ActivityModel? Content { get; init; }
    }
}
