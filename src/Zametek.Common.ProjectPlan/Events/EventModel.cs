namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record EventModel
    {
        public int Id { get; init; }

        public int? EarliestFinishTime { get; init; }

        public int? LatestFinishTime { get; init; }
    }
}
