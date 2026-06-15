namespace Zametek.Graphs.ProjectPlan
{
    public interface IVertexGraphSerializer
    {
        byte[] BuildVertexGraphSvgData(DiagramGraphModel diagramGraph, GraphTheme theme);

        GraphLayoutModel BuildVertexGraphLayout(DiagramGraphModel diagramGraph, GraphTheme theme);

        byte[] BuildVertexGraphMLData(DiagramGraphModel diagramGraph);

        byte[] BuildVertexGraphVizData(DiagramGraphModel diagramGraph);
    }
}
