using System.ComponentModel;
using Avalonia.Collections;
using Avalonia.Media;

namespace Zametek.Graphs.Avalonia
{
    // The bindable contract a node body template draws against, implemented by GraphNodeViewModel (the
    // library's concrete node). Exposed so a graph hosted outside this library can supply its own
    // InteractiveGraphView.NodeTemplate and bind against a stable surface (x:DataType) instead of the
    // concrete view-model. Node positioning, dragging, the selection ring and dimming are owned by the
    // control, so a replacement template only needs to draw the node body from these members.
    public interface IGraphNodeViewModel
        : INotifyPropertyChanged
    {
        int Id { get; }

        double X { get; set; }

        double Y { get; set; }

        double Width { get; }

        double Height { get; }

        string Label { get; }

        string? Tooltip { get; }

        IBrush FillBrush { get; }

        IBrush BorderBrush { get; }

        double BorderThickness { get; }

        AvaloniaList<double>? StrokeDashArray { get; }

        // Themed presentation (resolved from GraphAppearance): the node body corner radius, label font
        // and brush, and the selection-ring brush. Exposed per node so a template binds them from the
        // one item context alongside the per-node data above.
        double CornerRadius { get; }

        FontFamily LabelFontFamily { get; }

        double LabelFontSize { get; }

        IBrush LabelBrush { get; }

        IBrush SelectionBrush { get; }

        bool IsSelected { get; set; }

        bool IsDimmed { get; set; }

        double NodeOpacity { get; }
    }
}
