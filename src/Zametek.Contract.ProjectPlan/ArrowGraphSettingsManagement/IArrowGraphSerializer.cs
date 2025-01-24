using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IArrowGraphSerializer
    {
        byte[] BuildArrowGraphSvgData(ArrowGraphModel arrowGraph, ArrowGraphSettingsModel arrowGraphSettings, BaseTheme baseTheme, bool viewNames);

        byte[] BuildArrowGraphMLData(ArrowGraphModel arrowGraph, ArrowGraphSettingsModel arrowGraphSettings, bool viewNames);

        byte[] BuildArrowGraphVizData(ArrowGraphModel arrowGraph, ArrowGraphSettingsModel arrowGraphSettings, bool viewNames);
    }
}
