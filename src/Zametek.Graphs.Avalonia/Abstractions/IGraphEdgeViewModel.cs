using System.ComponentModel;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;

namespace Zametek.Graphs.Avalonia
{
    // The bindable contract an edge appearance template draws against, implemented by
    // GraphEdgeViewModel. Exposed so a graph hosted outside this library can supply its own
    // InteractiveGraphView.EdgeTemplate and bind against a stable surface (x:DataType) instead of the
    // concrete view-model. The wide invisible hit area and its tooltip are owned by the control, so a
    // replacement template only needs to draw the line, arrowhead and optional label from these members.
    public interface IGraphEdgeViewModel
        : INotifyPropertyChanged
    {
        int Id { get; }

        Geometry EdgeGeometry { get; }

        IBrush Stroke { get; }

        double StrokeThickness { get; }

        AvaloniaList<double>? StrokeDashArray { get; }

        double EdgeOpacity { get; }

        IList<Point> ArrowPoints { get; }

        string Label { get; }

        bool ShowLabel { get; }

        IBrush LabelBrush { get; }

        // Themed edge-label font (resolved from GraphAppearance).
        FontFamily LabelFontFamily { get; }

        double LabelFontSize { get; }

        double LabelX { get; }

        double LabelY { get; }

        string? Tooltip { get; }
    }
}
