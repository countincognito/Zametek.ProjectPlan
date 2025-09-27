using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IVertexGraphSerializer
    {
        byte[] BuildVertexGraphSvgData(VertexGraphModel vertexGraph, ArrowGraphSettingsModel graphSettings, BaseTheme baseTheme, bool viewNames);

        byte[] BuildVertexGraphMLData(VertexGraphModel vertexGraph, ArrowGraphSettingsModel graphSettings, bool viewNames);

        byte[] BuildVertexGraphVizData(VertexGraphModel vertexGraph, ArrowGraphSettingsModel graphSettings, bool viewNames);
    }
}
