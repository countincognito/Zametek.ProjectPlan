using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;
using ReactiveUI;

namespace Zametek.Graphs.ProjectPlan
{
    // A directed graph edge: a straight line clipped to the source/target node borders with an
    // arrowhead at the target, plus an optional label placed at the edge midpoint. Endpoints and
    // label position are derived from the node positions and update live as nodes are dragged.
    // The base colour is the supplied foreground colour (so e.g. the critical path shows through),
    // defaulting to grey; selection overrides it with the highlight colour. The label is used by the
    // arrow graph (activity edges) and left empty by the vertex graph. (Replaces the parallel
    // ArrowGraphEdgeViewModel/VertexGraphEdgeViewModel - the arrow one was a superset of the vertex one.)
    public class GraphEdgeViewModel
        : ReactiveObject, IDisposable
    {
        private const double c_DimmedOpacity = 0.15;
        private const double c_HighlightThickness = 2.5;
        private const double c_ArrowLength = 9.0;
        private const double c_ArrowHalfWidth = 4.5;
        // Lift the label clear of the line so it reads against the canvas, not the edge.
        private const double c_LabelOffset = 9.0;
        private static readonly IBrush s_DefaultBrush = new SolidColorBrush(Colors.Gray);
        private static readonly IBrush s_HighlightBrush = new SolidColorBrush(Color.Parse(@"#0078D4"));
        private static readonly IBrush s_LightLabelBrush = new SolidColorBrush(Colors.Black);
        private static readonly IBrush s_DarkLabelBrush = new SolidColorBrush(Colors.White);

        private readonly GraphNodeViewModel m_Source;
        private readonly GraphNodeViewModel m_Target;
        private readonly double m_BaseThickness;
        private readonly IBrush m_BaseBrush;
        private readonly IDisposable m_SourceSub;
        private readonly IDisposable m_TargetSub;

        public GraphEdgeViewModel(
            int id,
            GraphNodeViewModel source,
            GraphNodeViewModel target,
            double strokeThickness,
            bool isDashed,
            string? foregroundColorHexCode,
            string? label,
            bool showLabel,
            string? tooltip,
            GraphTheme theme)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);
            Id = id;
            m_Source = source;
            m_Target = target;
            m_BaseThickness = strokeThickness <= 0.0 ? 1.0 : strokeThickness;
            m_BaseBrush = ToBrush(foregroundColorHexCode, s_DefaultBrush);
            StrokeDashArray = isDashed ? [3.0, 2.0] : null;
            Label = label ?? string.Empty;
            ShowLabel = showLabel && !string.IsNullOrEmpty(label);
            Tooltip = tooltip;
            LabelBrush = theme == GraphTheme.Dark ? s_DarkLabelBrush : s_LightLabelBrush;

            m_SourceSub = m_Source
                .WhenAnyValue(x => x.X, x => x.Y)
                .Subscribe(_ => RaiseGeometryChanged());
            m_TargetSub = m_Target
                .WhenAnyValue(x => x.X, x => x.Y)
                .Subscribe(_ => RaiseGeometryChanged());
        }

        public int Id { get; }

        public int SourceId => m_Source.Id;

        public int TargetId => m_Target.Id;

        // Line endpoints, clipped to each node's rectangular border so the arrowhead is visible.
        public Point StartPoint => ClipToBorder(m_Source, m_Target);

        public Point EndPoint => ClipToBorder(m_Target, m_Source);

        public IList<Point> ArrowPoints => BuildArrowPoints();

        public AvaloniaList<double>? StrokeDashArray { get; }

        public string Label { get; }

        public bool ShowLabel { get; }

        public string? Tooltip { get; }

        public IBrush LabelBrush { get; }

        // Top-left anchor for the label: the edge midpoint, lifted perpendicular to the line so it
        // sits just off the edge rather than on top of it.
        public double LabelX => LabelAnchor.X;

        public double LabelY => LabelAnchor.Y;

        public IBrush Stroke => IsHighlighted ? s_HighlightBrush : m_BaseBrush;

        public double StrokeThickness => IsHighlighted ? c_HighlightThickness : m_BaseThickness;

        // Neutral (unselected, undimmed) appearance, used when exporting the graph image so the
        // export does not depend on the current selection/highlight state.
        public IBrush BaseStroke => m_BaseBrush;

        public double BaseStrokeThickness => m_BaseThickness;

        public double EdgeOpacity => IsDimmed ? c_DimmedOpacity : 1.0;

        private bool m_IsHighlighted;
        public bool IsHighlighted
        {
            get => m_IsHighlighted;
            set
            {
                this.RaiseAndSetIfChanged(ref m_IsHighlighted, value);
                this.RaisePropertyChanged(nameof(Stroke));
                this.RaisePropertyChanged(nameof(StrokeThickness));
            }
        }

        private bool m_IsDimmed;
        public bool IsDimmed
        {
            get => m_IsDimmed;
            set
            {
                this.RaiseAndSetIfChanged(ref m_IsDimmed, value);
                this.RaisePropertyChanged(nameof(EdgeOpacity));
            }
        }

        private void RaiseGeometryChanged()
        {
            this.RaisePropertyChanged(nameof(StartPoint));
            this.RaisePropertyChanged(nameof(EndPoint));
            this.RaisePropertyChanged(nameof(ArrowPoints));
            this.RaisePropertyChanged(nameof(LabelX));
            this.RaisePropertyChanged(nameof(LabelY));
        }

        private Point LabelAnchor
        {
            get
            {
                Point start = StartPoint;
                Point end = EndPoint;
                double midX = (start.X + end.X) / 2.0;
                double midY = (start.Y + end.Y) / 2.0;

                double dx = end.X - start.X;
                double dy = end.Y - start.Y;
                double length = Math.Sqrt((dx * dx) + (dy * dy));
                if (length < 1e-6)
                {
                    return new Point(midX, midY);
                }

                // Perpendicular unit vector, used to lift the label off the line.
                double perpX = -dy / length;
                double perpY = dx / length;
                return new Point(midX + (perpX * c_LabelOffset), midY + (perpY * c_LabelOffset));
            }
        }

        // Intersection of the centre-to-centre line with 'node's axis-aligned border rectangle.
        private static Point ClipToBorder(GraphNodeViewModel node, GraphNodeViewModel toward)
        {
            double centreX = node.CentreX;
            double centreY = node.CentreY;
            double dx = toward.CentreX - centreX;
            double dy = toward.CentreY - centreY;

            if (dx == 0.0 && dy == 0.0)
            {
                return new Point(centreX, centreY);
            }

            double halfWidth = node.Width / 2.0;
            double halfHeight = node.Height / 2.0;
            double scaleX = dx != 0.0 ? halfWidth / Math.Abs(dx) : double.PositiveInfinity;
            double scaleY = dy != 0.0 ? halfHeight / Math.Abs(dy) : double.PositiveInfinity;
            double scale = Math.Min(scaleX, scaleY);

            return new Point(centreX + (dx * scale), centreY + (dy * scale));
        }

        private IList<Point> BuildArrowPoints()
        {
            Point tip = EndPoint;
            Point tail = StartPoint;
            double dx = tip.X - tail.X;
            double dy = tip.Y - tail.Y;
            double length = Math.Sqrt((dx * dx) + (dy * dy));

            if (length < 1e-6)
            {
                return [];
            }

            double unitX = dx / length;
            double unitY = dy / length;
            double baseX = tip.X - (unitX * c_ArrowLength);
            double baseY = tip.Y - (unitY * c_ArrowLength);
            double perpX = -unitY;
            double perpY = unitX;

            return
            [
                tip,
                new Point(baseX + (perpX * c_ArrowHalfWidth), baseY + (perpY * c_ArrowHalfWidth)),
                new Point(baseX - (perpX * c_ArrowHalfWidth), baseY - (perpY * c_ArrowHalfWidth)),
            ];
        }

        private static IBrush ToBrush(string? hexCode, IBrush fallback)
        {
            if (string.IsNullOrWhiteSpace(hexCode))
            {
                return fallback;
            }
            return new SolidColorBrush(ColorHelper.HtmlHexCodeToColor(hexCode));
        }

        public void Dispose()
        {
            m_SourceSub.Dispose();
            m_TargetSub.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
