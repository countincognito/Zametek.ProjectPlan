using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IVertexGraphSerializer
    {
        byte[] BuildVertexGraphSvgData(VertexGraphModel vertexGraph, GraphSettingsModel graphSettings, BaseTheme baseTheme, bool viewNames);

        byte[] BuildVertexGraphMLData(VertexGraphModel vertexGraph, GraphSettingsModel graphSettings, bool viewNames);

        byte[] BuildVertexGraphVizData(VertexGraphModel vertexGraph, GraphSettingsModel graphSettings, bool viewNames);
    }
}
