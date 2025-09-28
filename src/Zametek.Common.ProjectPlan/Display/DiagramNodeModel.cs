namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record DiagramNodeModel
    {
        public int Id { get; init; }

        public double X { get; init; }

        public double Y { get; init; }

        public double Height { get; init; }

        public double Width { get; init; }

        public string? FillColorHexCode { get; init; }

        public string? BorderColorHexCode { get; init; }

        public NodeBorderDashStyle BorderDashStyle { get; init; }

        public double BorderThickness { get; init; }

        public string? Text { get; init; }

        public string? Name { get; init; }
    }
}
