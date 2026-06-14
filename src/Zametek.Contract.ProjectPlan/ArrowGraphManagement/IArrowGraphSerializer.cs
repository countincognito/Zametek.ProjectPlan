using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IArrowGraphSerializer
    {
        byte[] BuildArrowGraphSvgData(ArrowGraphModel arrowGraph, BaseTheme baseTheme, bool viewNames);

        byte[] BuildArrowGraphMLData(ArrowGraphModel arrowGraph, bool viewNames);

        byte[] BuildArrowGraphVizData(ArrowGraphModel arrowGraph, bool viewNames);
    }
}
