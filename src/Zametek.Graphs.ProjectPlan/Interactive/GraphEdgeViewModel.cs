using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;
using ReactiveUI;

namespace Zametek.Graphs.ProjectPlan
{
    // A directed graph edge, drawn as one or more contiguous cubic-bezier segments clipped to the
    // source and target node borders, with an arrowhead at the target and an optional label at the
    // path midpoint. The GraphEdgeRoutingMode chooses the shape (spline modes draw a smooth horizontal
    // connector, straight modes a line, rectilinear modes an orthogonal right-angle path); the
    // geometry is computed client-side (see GraphEdgeGeometry) and updates live as nodes are dragged.
    // The base colour is the supplied foreground colour (so e.g. the critical path shows
    // through), defaulting to grey; selection overrides it with the highlight colour. The label is
    // used by the arrow graph (activity edges) and left empty by the vertex graph.
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
        private readonly GraphEdgeRoutingMode m_RoutingMode;
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
            GraphTheme theme,
            GraphEdgeRoutingMode routingMode)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);
            Id = id;
            m_Source = source;
            m_Target = target;
            m_BaseThickness = strokeThickness <= 0.0 ? 1.0 : strokeThickness;
            m_BaseBrush = ToBrush(foregroundColorHexCode, s_DefaultBrush);
            m_RoutingMode = routingMode;
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

        // The edge's shape as contiguous cubic-bezier segments, chosen by the routing mode. Exposed so
        // the export renderer can rebuild the same path in SkiaSharp.
        internal IReadOnlyList<GraphEdgeSegment> EdgeSegments => GraphEdgeGeometry.BuildSegments(m_RoutingMode, StartPoint, EndPoint);

        // The drawn edge: the bezier segments stitched into one open figure (a straight line for
        // non-spline modes, a right-angle path for rectilinear modes). Bound by the view's <Path>.
        public Geometry EdgeGeometry
        {
            get
            {
                IReadOnlyList<GraphEdgeSegment> segments = EdgeSegments;
                var geometry = new StreamGeometry();
                using (StreamGeometryContext context = geometry.Open())
                {
                    context.BeginFigure(segments[0].Start, isFilled: false);
                    foreach (GraphEdgeSegment segment in segments)
                    {
                        context.CubicBezierTo(segment.Control1, segment.Control2, segment.End);
                    }
                    context.EndFigure(isClosed: false);
                }
                return geometry;
            }
        }

        public IList<Point> ArrowPoints => BuildArrowPoints();

        public AvaloniaList<double>? StrokeDashArray { get; }

        public string Label { get; }

        public bool ShowLabel { get; }

        public string? Tooltip { get; }

        public IBrush LabelBrush { get; }

        // Top-left anchor for the label: the curve midpoint, lifted perpendicular to the chord so it
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
            this.RaisePropertyChanged(nameof(EdgeGeometry));
            this.RaisePropertyChanged(nameof(ArrowPoints));
            this.RaisePropertyChanged(nameof(LabelX));
            this.RaisePropertyChanged(nameof(LabelY));
        }

        private Point LabelAnchor
        {
            get
            {
                Point mid = GraphEdgeGeometry.Midpoint(EdgeSegments);

                double dx = EndPoint.X - StartPoint.X;
                double dy = EndPoint.Y - StartPoint.Y;
                double length = Math.Sqrt((dx * dx) + (dy * dy));
                if (length < 1e-6)
                {
                    return mid;
                }

                // Perpendicular unit vector, used to lift the label off the line.
                double perpX = -dy / length;
                double perpY = dx / length;
                return new Point(mid.X + (perpX * c_LabelOffset), mid.Y + (perpY * c_LabelOffset));
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
            GraphEdgeSegment last = EdgeSegments[^1];
            Point tip = last.End;

            // Tangent at the tip = direction from the final segment's last control point to the tip, so
            // the arrowhead follows the curve. (For a straight run the control point is on the chord,
            // so this is the chord direction.)
            double dx = tip.X - last.Control2.X;
            double dy = tip.Y - last.Control2.Y;
            double length = Math.Sqrt((dx * dx) + (dy * dy));

            if (length < 1e-6)
            {
                // Degenerate control point; fall back to the chord direction.
                dx = tip.X - StartPoint.X;
                dy = tip.Y - StartPoint.Y;
                length = Math.Sqrt((dx * dx) + (dy * dy));
                if (length < 1e-6)
                {
                    return [];
                }
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
