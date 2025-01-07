using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record UpdateActivityModel
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;
        public bool IsNameEdited { get; init; } = false;

        public string Notes { get; init; } = string.Empty;
        public bool IsNotesEdited { get; init; } = false;

        public List<int> TargetWorkStreams { get; init; } = [];
        public bool IsTargetWorkStreamsEdited { get; init; } = false;

        public List<int> TargetResources { get; init; } = [];
        public bool IsTargetResourcesEdited { get; init; } = false;

        public LogicalOperator TargetResourceOperator { get; init; }
        public bool IsTargetResourceOperatorEdited { get; init; } = false;
    }
}
