namespace Zametek.Data.ProjectPlan.v0_6_1
{
    [Serializable]
    public record ResourceMarginsModel
    {
        public double? Direct { get; init; }

        public double? Indirect { get; init; }

        public double? Other { get; init; }

        public double? Total { get; init; }

        public double? DirectAbsolute { get; init; }

        public double? IndirectAbsolute { get; init; }

        public double? OtherAbsolute { get; init; }

        public double? TotalAbsolute { get; init; }
    }
}
