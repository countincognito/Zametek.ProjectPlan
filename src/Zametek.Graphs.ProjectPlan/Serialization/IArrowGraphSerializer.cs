namespace Zametek.Graphs.ProjectPlan
{
    public interface IArrowGraphSerializer
    {
        byte[] BuildArrowGraphSvgData(DiagramGraphModel diagramGraph, GraphTheme theme);

        GraphLayoutModel BuildArrowGraphLayout(DiagramGraphModel diagramGraph, GraphTheme theme);

        byte[] BuildArrowGraphMLData(DiagramGraphModel diagramGraph);

        byte[] BuildArrowGraphVizData(DiagramGraphModel diagramGraph);
    }
}
