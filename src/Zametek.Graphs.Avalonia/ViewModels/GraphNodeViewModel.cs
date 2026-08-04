using Avalonia.Collections;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using ReactiveUI;

namespace Zametek.Graphs.Avalonia
{
    // Interactive, draggable, selectable graph node (an activity node in the vertex graph, an event
    // node in the arrow graph). The slack/override border colour and critical/dummy dash style are
    // preserved; selection is shown via a separate overlay ring (in the view) so it does not clobber
    // the underlying colour. Dimming is driven by opacity. (Replaces the parallel
    // ArrowGraphNodeViewModel/VertexGraphNodeViewModel, which were identical.)
    public class GraphNodeViewModel
        : ReactiveObject, IGraphNodeViewModel
    {
        private readonly double m_DimmedOpacity;

        public GraphNodeViewModel(GraphNodeLayoutModel layout, GraphAppearance appearance)
        {
            ArgumentNullException.ThrowIfNull(layout);
            ArgumentNullException.ThrowIfNull(appearance);
            Id = layout.Id;
            m_X = layout.X;
            m_Y = layout.Y;
            Width = layout.Width;
            Height = layout.Height;
            Label = layout.Label;
            Name = layout.Name;
            Tooltip = layout.Tooltip;
            FillBrush = ToBrush(layout.FillColorHexCode, appearance.NodeFillFallbackBrush);
            BorderBrush = ToBrush(layout.BorderColorHexCode, appearance.NodeBorderFallbackBrush);
            BorderThickness = layout.BorderThickness <= 0.0 ? appearance.DefaultNodeBorderThickness : layout.BorderThickness;
            StrokeDashArray = layout.IsDashed ? [.. appearance.DashPattern] : null;
            CornerRadius = appearance.NodeCornerRadius;
            LabelFontFamily = appearance.NodeLabelFontFamily;
            LabelFontSize = appearance.NodeLabelFontSize;
            LabelBrush = appearance.NodeLabelBrush;
            SelectionBrush = appearance.SelectionBrush;
            m_DimmedOpacity = appearance.NodeDimmedOpacity;
        }

        public int Id { get; }

        private double m_X;
        public double X
        {
            get => m_X;
            set => this.RaiseAndSetIfChanged(ref m_X, value);
        }

        private double m_Y;
        public double Y
        {
            get => m_Y;
            set => this.RaiseAndSetIfChanged(ref m_Y, value);
        }

        public double Width { get; }

        public double Height { get; }

        public double CentreX => X + (Width / 2.0);

        public double CentreY => Y + (Height / 2.0);

        public string Label { get; }

        public string? Name { get; }

        public string? Tooltip { get; }

        public IBrush FillBrush { get; }

        public IBrush BorderBrush { get; }

        public double BorderThickness { get; }

        public AvaloniaList<double>? StrokeDashArray { get; }

        // Themed presentation resolved from GraphAppearance (global, but exposed per node so the default
        // node template - and any host-supplied NodeTemplate - can bind everything from this one context).
        public double CornerRadius { get; }

        public FontFamily LabelFontFamily { get; }

        public double LabelFontSize { get; }

        public IBrush LabelBrush { get; }

        public IBrush SelectionBrush { get; }

        private bool m_IsSelected;
        public bool IsSelected
        {
            get => m_IsSelected;
            set => this.RaiseAndSetIfChanged(ref m_IsSelected, value);
        }

        private bool m_IsDimmed;
        public bool IsDimmed
        {
            get => m_IsDimmed;
            set
            {
                this.RaiseAndSetIfChanged(ref m_IsDimmed, value);
                this.RaisePropertyChanged(nameof(NodeOpacity));
            }
        }

        public double NodeOpacity => IsDimmed ? m_DimmedOpacity : 1.0;

        private static IBrush ToBrush(string? hexCode, IBrush fallback)
        {
            if (string.IsNullOrWhiteSpace(hexCode))
            {
                return fallback;
            }
            // Immutable so the brush stays renderable no matter which thread builds the view-model
            // (see the THREADING note on GraphAppearance).
            return new ImmutableSolidColorBrush(ColorHelper.HtmlHexCodeToColor(hexCode));
        }
    }
}
