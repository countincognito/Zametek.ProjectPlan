namespace Zametek.Graphs.ProjectPlan
{
    // The graph layout/render engine. One implementation (GraphSerializer), configured per graph via
    // a GraphConfiguration, serves both the arrow and vertex graphs.
    public interface IGraphSerializer
    {
        // The live configuration driving the layout/render (per-graph tuning + the edge routing mode).
        // Settable so a consumer can swap the whole record at runtime - e.g. the interactive graph
        // changing the routing mode from its context menu - and have the next build follow it.
        GraphConfiguration Configuration { get; set; }

        byte[] BuildGraphSvgData(DiagramGraphModel diagramGraph, GraphTheme theme);

        GraphLayoutModel BuildGraphLayout(DiagramGraphModel diagramGraph, GraphTheme theme);

        byte[] BuildGraphMLData(DiagramGraphModel diagramGraph);

        byte[] BuildGraphVizData(DiagramGraphModel diagramGraph);
    }
}
