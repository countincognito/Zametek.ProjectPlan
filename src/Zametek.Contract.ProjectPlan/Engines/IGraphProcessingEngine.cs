using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IGraphProcessingEngine
    {
        byte[] ExportArrowGraphToDiagram(DiagramArrowGraphDto diagramArrowGraphDto);
    }
}
