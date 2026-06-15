namespace Zametek.Graphs.ProjectPlan
{
    [Serializable]
    public record GraphEdgeLayoutModel
    {
        public int Id { get; init; }

        public int SourceId { get; init; }

        public int TargetId { get; init; }

        public double StrokeThickness { get; init; }

        public bool IsDashed { get; init; }

        // Arrow-graph edges carry the activity colour (e.g. the critical path); vertex-graph edges
        // leave this null and are drawn in the neutral base colour.
        public string? ForegroundColorHexCode { get; init; }

        // Arrow-graph edges carry the activity label (id/duration/slack); vertex-graph edges
        // leave this empty and ShowLabel false.
        public string? Label { get; init; }

        public bool ShowLabel { get; init; }

        // Arrow-graph edges represent activities, so they carry the same rich hover tooltip the
        // vertex graph puts on its activity nodes; vertex-graph edges leave this null.
        public string? Tooltip { get; init; }
    }
}
