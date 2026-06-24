namespace Zametek.Graphs.ProjectPlan
{
    // The graph layout/render engine. One implementation (GraphSerializer), configured per graph via
    // a GraphConfiguration, serves both the arrow and vertex graphs.
    public interface IGraphSerializer
    {
        byte[] BuildGraphSvgData(DiagramGraphModel diagramGraph, GraphTheme theme);

        GraphLayoutModel BuildGraphLayout(DiagramGraphModel diagramGraph, GraphTheme theme);

        byte[] BuildGraphMLData(DiagramGraphModel diagramGraph);

        byte[] BuildGraphVizData(DiagramGraphModel diagramGraph);
    }
}
