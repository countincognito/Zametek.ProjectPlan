namespace Zametek.Data.ProjectPlan.v0_3_0
{
    [Serializable]
    public record EventEdgeModel
    {
        public v0_1_0.EventModel Content { get; init; } = new v0_1_0.EventModel();
    }
}
