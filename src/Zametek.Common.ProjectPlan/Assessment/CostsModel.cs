namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record CostsModel
    {
        public double? Direct { get; init; }

        public double? Indirect { get; init; }

        public double? Other { get; init; }
    }
}
