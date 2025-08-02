using Zametek.Maths.Graphs;

namespace Zametek.Data.ProjectPlan.v0_4_4
{
    [Serializable]
    public record ResourceModel
    {
        public int Id { get; init; }

        public string? Name { get; init; }

        public bool IsExplicitTarget { get; init; }

        public bool IsInactive { get; init; }

        public InterActivityAllocationType InterActivityAllocationType { get; init; }

        public List<int> InterActivityPhases { get; init; } = [];

        public double UnitCost { get; init; }

        public double UnitBilling { get; init; }

        public int DisplayOrder { get; init; }

        public v0_1_0.ColorFormatModel ColorFormat { get; init; } = new v0_1_0.ColorFormatModel();

        public List<v0_4_0.ResourceTrackerModel> Trackers { get; init; } = [];
    }
}
