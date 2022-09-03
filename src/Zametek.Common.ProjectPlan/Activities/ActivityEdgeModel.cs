namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ActivityEdgeModel
    {
        public ActivityModel Content { get; init; } = new ActivityModel();
    }
}
