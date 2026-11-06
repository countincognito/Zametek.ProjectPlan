namespace Zametek.Graphs.Avalonia
{
    // The formats a graph can be written in (IInteractiveGraph.WriteImageAsync): four image formats, and two data
    // formats for other graph tools.
    public enum GraphFileFormat
    {
        Jpeg,
        Png,
        Pdf,
        Svg,

        // GraphML, for yEd and other GraphML readers.
        GraphML,

        // GraphViz's Dot language.
        GraphViz
    }
}
