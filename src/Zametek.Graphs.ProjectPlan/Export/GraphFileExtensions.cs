namespace Zametek.Graphs.ProjectPlan
{
    // The file extensions the graph export understands, kept here (rather than referencing the
    // application's resource strings) so the control library stays self-contained. These mirror the
    // values the application still uses to build its save-file dialog filters.
    internal static class GraphFileExtensions
    {
        public const string Jpeg = "jpeg";
        public const string Png = "png";
        public const string Pdf = "pdf";
        public const string Svg = "svg";
        public const string GraphML = "graphml";
        public const string GraphViz = "dot";
    }
}
