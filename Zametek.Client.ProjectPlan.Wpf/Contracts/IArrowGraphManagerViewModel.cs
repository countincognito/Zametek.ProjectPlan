using System.Windows.Input;
using Zametek.Common.Project;
using Zametek.Common.ProjectPlan;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IArrowGraphManagerViewModel
    {
        bool HasStaleArrowGraph
        {
            get;
        }

        ArrowGraphSettingsDto ArrowGraphSettingsDto
        {
            get;
        }

        ArrowGraphData ArrowGraphData
        {
            get;
        }

        ArrowGraphDto ArrowGraphDto
        {
            get;
        }

        ICommand GenerateArrowGraphCommand
        {
            get;
        }

        byte[] ExportArrowGraphToDiagram(DiagramArrowGraphDto diagramArrowGraphDto);
    }
}
