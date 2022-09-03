namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record DiagramEdgeModel
    {
        public int Id { get; init; }

        public string? Name { get; init; }

        public int SourceId { get; init; }

        public int TargetId { get; init; }

        public EdgeDashStyle DashStyle { get; init; }

        public string? ForegroundColorHexCode { get; init; }

        public double StrokeThickness { get; init; }

        public string? Label { get; init; }

        public bool ShowLabel { get; init; }
    }
}
