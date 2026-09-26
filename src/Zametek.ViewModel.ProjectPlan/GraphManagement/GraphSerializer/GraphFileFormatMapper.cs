using Zametek.Common.ProjectPlan;
using Zametek.Graphs.Avalonia;

namespace Zametek.ViewModel.ProjectPlan
{
    // Maps the application's GraphExportFormat onto the Graphs library's own GraphFileFormat at the boundary, so the
    // contracts carry no dependency on the graph library. The mapping is by name and total; the `_` arm only guards
    // against an out-of-range cast, and throws, since writing a format other than the one asked for would be wrong.
    internal static class GraphFileFormatMapper
    {
        public static GraphFileFormat ToGraphFileFormat(this GraphExportFormat format)
        {
            return format switch
            {
                GraphExportFormat.Jpeg => GraphFileFormat.Jpeg,
                GraphExportFormat.Png => GraphFileFormat.Png,
                GraphExportFormat.Pdf => GraphFileFormat.Pdf,
                GraphExportFormat.Svg => GraphFileFormat.Svg,
                GraphExportFormat.GraphML => GraphFileFormat.GraphML,
                GraphExportFormat.GraphViz => GraphFileFormat.GraphViz,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
            };
        }
    }
}
