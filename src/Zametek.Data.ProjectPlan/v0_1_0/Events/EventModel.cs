namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public record EventModel
    {
        public int Id { get; init; }

        public int? EarliestFinishTime { get; init; }

        public int? LatestFinishTime { get; init; }
    }
}
