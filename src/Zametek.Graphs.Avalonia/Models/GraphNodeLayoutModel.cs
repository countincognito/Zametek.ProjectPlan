namespace Zametek.Graphs.Avalonia
{
    [Serializable]
    public record GraphNodeLayoutModel
    {
        public int Id { get; init; }

        public double X { get; init; }

        public double Y { get; init; }

        public double Width { get; init; }

        public double Height { get; init; }

        public string Label { get; init; } = string.Empty;

        public string? Name { get; init; }

        public string? Tooltip { get; init; }

        public string? FillColorHexCode { get; init; }

        public string? BorderColorHexCode { get; init; }

        public double BorderThickness { get; init; }

        public bool IsDashed { get; init; }
    }
}
