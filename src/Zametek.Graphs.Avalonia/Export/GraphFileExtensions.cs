namespace Zametek.Graphs.Avalonia
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

        // The format a file name's extension names. Used by the Save-As path, which has only the name the user picked.
        public static GraphFileFormat GetFileFormat(string filename)
        {
            return Path.GetExtension(filename) switch
            {
                $".{Jpeg}" => GraphFileFormat.Jpeg,
                $".{Png}" => GraphFileFormat.Png,
                $".{Pdf}" => GraphFileFormat.Pdf,
                $".{Svg}" => GraphFileFormat.Svg,
                $".{GraphML}" => GraphFileFormat.GraphML,
                $".{GraphViz}" => GraphFileFormat.GraphViz,
                _ => throw new ArgumentOutOfRangeException(nameof(filename), @$"{Graphs_Messages.Message_UnableToSaveFile} {filename}"),
            };
        }
    }
}
