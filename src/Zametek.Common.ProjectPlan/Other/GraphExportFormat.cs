namespace Zametek.Common.ProjectPlan
{
    // The formats the arrow and vertex graphs can be written in: four image formats, and the GraphML and GraphViz (Dot)
    // data formats.
    [Serializable]
    public enum GraphExportFormat
    {
        Jpeg,
        Png,
        Pdf,
        Svg,
        GraphML,
        GraphViz
    }
}
