namespace Zametek.Data.ProjectPlan.v0_6_1
{
    [Serializable]
    public record ResourceCostsModel
    {
        public double? Direct { get; init; }

        public double? Indirect { get; init; }

        public double? Other { get; init; }

        public double? Total { get; init; }
    }
}
