namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record EffortsModel
    {
        public double? Direct { get; init; }

        public double? Indirect { get; init; }

        public double? Other { get; init; }

        public double? Activity { get; init; }
    }
}
