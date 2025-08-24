using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record UpdateResourceModel
    {
        public int Id { get; init; } = default;

        public string Name { get; init; } = string.Empty;
        public bool IsNameEdited { get; init; } = false;

        public bool IsExplicitTarget { get; init; }
        public bool IsIsExplicitTargetEdited { get; init; } = false;

        public bool IsInactive { get; init; }
        public bool IsIsInactiveEdited { get; init; } = false;

        public InterActivityAllocationType InterActivityAllocationType { get; init; }
        public bool IsInterActivityAllocationTypeEdited { get; init; } = false;

        public double UnitCost { get; init; }
        public bool IsUnitCostEdited { get; init; } = false;

        public double UnitBilling { get; init; }
        public bool IsUnitBillingEdited { get; init; } = false;

        public double FixedCost { get; init; }
        public bool IsFixedCostEdited { get; init; } = false;

        public double FixedBilling { get; init; }
        public bool IsFixedBillingEdited { get; init; } = false;

        public List<int> InterActivityPhases { get; init; } = [];
        public bool IsInterActivityPhasesEdited { get; init; } = false;
    }
}
