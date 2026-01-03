namespace Zametek.Data.ProjectPlan.v0_5_0
{
    [Serializable]
    public record CostsModel
    {
        public double? Direct { get; init; }

        public double? Indirect { get; init; }

        public double? Other { get; init; }

        public double? Total { get; init; }
    }
}
