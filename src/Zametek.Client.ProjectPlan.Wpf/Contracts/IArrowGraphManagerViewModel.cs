using Prism.Interactivity.InteractionRequest;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IArrowGraphManagerViewModel
    {
        IInteractionRequest NotificationInteractionRequest { get; }

        bool IsBusy { get; }

        bool HasStaleArrowGraph { get; }

        ArrowGraphData ArrowGraphData { get; }

        ICommand GenerateArrowGraphCommand { get; }

        byte[] ExportArrowGraphToDiagram(DiagramArrowGraphDto diagramArrowGraphDto);
    }
}
