using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ResourceModel
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public bool IsExplicitTarget { get; init; }

        public bool IsInactive { get; init; }

        public InterActivityAllocationType InterActivityAllocationType { get; init; }

        public double UnitCost { get; init; }

        public int DisplayOrder { get; init; }

        public int AllocationOrder { get; init; }

        public List<int> InterActivityPhases { get; init; } = [];

        public ColorFormatModel ColorFormat { get; init; } = new ColorFormatModel();

        public List<ResourceTrackerModel> Trackers { get; init; } = [];
    }
}
