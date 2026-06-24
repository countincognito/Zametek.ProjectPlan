namespace Zametek.Graphs.Avalonia
{
    // Serialises a library-neutral DiagramGraphModel to an interchange format (GraphML / GraphViz).
    // Framework-neutral and stateless. The layout/render outputs (the interactive GraphLayoutModel and
    // the fixed-layout SVG) live on the separate IGraphLayoutEngine seam, so this contract carries no
    // configuration and nothing coupled to a layout library.
    public interface IGraphSerializer
    {
        byte[] BuildGraphMLData(DiagramGraphModel diagramGraph);

        byte[] BuildGraphVizData(DiagramGraphModel diagramGraph);
    }
}
