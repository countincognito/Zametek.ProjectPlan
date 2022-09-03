namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record EventEdgeModel
    {
        public EventModel Content { get; init; } = new EventModel();
    }
}
