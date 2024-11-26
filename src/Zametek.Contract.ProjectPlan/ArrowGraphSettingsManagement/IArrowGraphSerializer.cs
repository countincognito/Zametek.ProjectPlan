using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IArrowGraphSerializer
    {
        byte[] BuildArrowGraphSvgData(ArrowGraphModel arrowGraph, ArrowGraphSettingsModel arrowGraphSettings, BaseTheme baseTheme);

        byte[] BuildArrowGraphMLData(ArrowGraphModel arrowGraph, ArrowGraphSettingsModel arrowGraphSettings);

        byte[] BuildArrowGraphVizData(ArrowGraphModel arrowGraph, ArrowGraphSettingsModel arrowGraphSettings);
    }
}
