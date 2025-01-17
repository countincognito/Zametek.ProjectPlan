using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record UpdateDependentActivityModel
    {
        // TODO clean up

        public int Id { get; init; } = default;

        //public List<int> Dependencies { get; init; } = [];
        //public bool IsDependenciesEdited { get; init; } = false;

        public string Name { get; init; } = string.Empty;
        public bool IsNameEdited { get; init; } = false;

        public string Notes { get; init; } = string.Empty;
        public bool IsNotesEdited { get; init; } = false;

        public List<int> TargetWorkStreams { get; init; } = [];
        public bool IsTargetWorkStreamsEdited { get; init; } = false;

        public List<int> TargetResources { get; init; } = [];
        public bool IsTargetResourcesEdited { get; init; } = false;

        public LogicalOperator TargetResourceOperator { get; init; } = LogicalOperator.AND;
        public bool IsTargetResourceOperatorEdited { get; init; } = false;

        public bool HasNoCost { get; init; } = default;
        public bool IsHasNoCostEdited { get; init; } = false;
    }
}
