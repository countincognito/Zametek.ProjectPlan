using Zametek.Common.ProjectPlan;

namespace Zametek.Graphs.ProjectPlan
{
    public interface IVertexGraphSerializer
    {
        byte[] BuildVertexGraphSvgData(VertexGraphModel vertexGraph, BaseTheme baseTheme, bool viewNames);

        GraphLayoutModel BuildVertexGraphLayout(VertexGraphModel vertexGraph, BaseTheme baseTheme, bool viewNames);

        byte[] BuildVertexGraphMLData(VertexGraphModel vertexGraph, bool viewNames);

        byte[] BuildVertexGraphVizData(VertexGraphModel vertexGraph, bool viewNames);
    }
}
