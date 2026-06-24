namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ActivityNodeModel
    {
        public Maths.Graphs.NodeType NodeType { get; init; }

        public ActivityModel Content { get; init; } = new ActivityModel();

        public List<int> IncomingEdges { get; init; } = [];

        public List<int> OutgoingEdges { get; init; } = [];

        // Presentation resolved by the application (from graph settings + activity state) before
        // the model is handed to the graph serializer. BorderWeight is the raw stroke weight; the
        // serializer applies its own size-correction factor.
        public string? BorderColorHexCode { get; init; }

        public NodeBorderDashStyle BorderDashStyle { get; init; }

        public double BorderWeight { get; init; }
    }
}
