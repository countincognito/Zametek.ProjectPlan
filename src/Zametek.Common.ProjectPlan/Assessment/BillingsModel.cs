namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record BillingsModel
    {
        public double? Direct { get; init; }

        public double? Indirect { get; init; }

        public double? Other { get; init; }
    }
}
