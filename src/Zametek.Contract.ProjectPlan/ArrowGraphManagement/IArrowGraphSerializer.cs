using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IArrowGraphSerializer
    {
        byte[] BuildArrowGraphSvgData(ArrowGraphModel arrowGraph, GraphSettingsModel graphSettings, BaseTheme baseTheme, bool viewNames);

        byte[] BuildArrowGraphMLData(ArrowGraphModel arrowGraph, GraphSettingsModel graphSettings, bool viewNames);

        byte[] BuildArrowGraphVizData(ArrowGraphModel arrowGraph, GraphSettingsModel graphSettings, bool viewNames);
    }
}
