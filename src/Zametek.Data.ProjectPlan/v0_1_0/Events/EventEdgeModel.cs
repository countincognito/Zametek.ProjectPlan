namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public record EventEdgeModel
    {
        public EventModel? Content { get; init; }
    }
}
