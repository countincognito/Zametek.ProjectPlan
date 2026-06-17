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
        // How far back along the curve (in workspace pixels) the arrowhead's direction is measured, so
        // a tiny final control leg cannot flip it sideways. ~1.5x the arrow length: long enough to
        // absorb such a leg, short enough to still follow real curvature.
        private const double c_ArrowTangentSpan = 14.0;
        // Hysteresis for the hybrid connection-axis choice: keep the pre-drag (MSAGL-chosen) axis until
        // the current arrangement exceeds this ratio against it, then fall back to the dominant axis.
        // Above 1 so there is a dead-band around 45 degrees that stops the orientation flip-flopping.
        private const double c_AxisFlipRatio = 1.5;
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
        private GraphEdgeRoutingMode m_RoutingMode;
        // The axes the most recent exact MSAGL route used to leave the source / enter the target (null
        // until the first route is captured). They survive an endpoint move - so a drag keeps the
        // pre-drag sides as a guide - and are cleared only when the routing mode changes.
        private GraphConnectionAxis? m_SourceExitAxis;
        private GraphConnectionAxis? m_TargetEntryAxis;
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

            // An endpoint moving invalidates any exact MSAGL-routed geometry (it was routed for the old
            // position), so the edge falls back to the live approximation until the next reroute.
            m_SourceSub = m_Source
                .WhenAnyValue(x => x.X, x => x.Y)
                .Subscribe(_ => InvalidateRoutedGeometry());
            m_TargetSub = m_Target
                .WhenAnyValue(x => x.X, x => x.Y)
                .Subscribe(_ => InvalidateRoutedGeometry());
        }

        public int Id { get; }

        public int SourceId => m_Source.Id;

        public int TargetId => m_Target.Id;

        // Line endpoints, clipped to each node's rectangular border so the arrowhead is visible.
        public Point StartPoint => ClipToBorder(m_Source, m_Target);

        public Point EndPoint => ClipToBorder(m_Target, m_Source);

        // The routing strategy that shapes this edge. Settable so the interactive graph can switch the
        // mode live (from its context menu) without a re-layout. Changing it discards any exact routed
        // geometry (which was for the old mode) and re-raises, so the edge shows the new mode's
        // approximation until the next reroute upgrades it.
        public GraphEdgeRoutingMode RoutingMode
        {
            get => m_RoutingMode;
            set
            {
                if (m_RoutingMode == value)
                {
                    return;
                }
                m_RoutingMode = value;
                m_RoutedSegments = null;
                // The captured sides were for the old mode's route; drop them so the approximation
                // falls back to the dominant axis until the new mode's reroute captures fresh ones.
                m_SourceExitAxis = null;
                m_TargetEntryAxis = null;
                RaiseGeometryChanged();
            }
        }

        // Exact MSAGL-routed geometry for the current positions/mode, applied as an override of the
        // client-side approximation (see SetRoutedSegments). Null = use the approximation, which is the
        // default and the live fallback while an endpoint is being dragged.
        private IReadOnlyList<GraphEdgeSegment>? m_RoutedSegments;

        // The edge's shape as contiguous cubic-bezier segments: the exact MSAGL route when one is in
        // effect, otherwise the client-side approximation (built around the resolved per-endpoint
        // connection axes, so a vertically-stacked arrangement connects top-to-bottom). Exposed so the
        // export renderer can rebuild the same path in SkiaSharp.
        internal IReadOnlyList<GraphEdgeSegment> EdgeSegments
        {
            get
            {
                if (m_RoutedSegments is not null)
                {
                    return m_RoutedSegments;
                }
                Point start = StartPoint;
                Point end = EndPoint;
                (GraphConnectionAxis sourceAxis, GraphConnectionAxis targetAxis) = ResolveConnectionAxes(start, end);
                return GraphEdgeGeometry.BuildSegments(m_RoutingMode, start, end, sourceAxis, targetAxis);
            }
        }

        // Apply (non-null) or drop (null) exact MSAGL-routed geometry as an override of the
        // approximation. Called by the interactive view-model after an off-thread reroute settles. A
        // non-empty route also refreshes the captured connection axes (the sides MSAGL chose), which
        // then guide the approximation through the next drag.
        internal void SetRoutedSegments(IReadOnlyList<GraphEdgeSegment>? segments)
        {
            m_RoutedSegments = segments;
            if (segments is { Count: > 0 })
            {
                m_SourceExitAxis = ExitAxis(segments[0]);
                m_TargetEntryAxis = EntryAxis(segments[^1]);
            }
            RaiseGeometryChanged();
        }

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

        // An endpoint moved: the exact routed geometry is now stale, so drop it (reverting to the live
        // approximation) and re-raise.
        private void InvalidateRoutedGeometry()
        {
            m_RoutedSegments = null;
            RaiseGeometryChanged();
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

        // Resolve the per-endpoint connection axes for the approximation: the hybrid of the pre-drag
        // MSAGL-chosen sides and the current arrangement. Each endpoint keeps its captured axis until
        // the arrangement clearly contradicts it, then falls back to the dominant axis; with nothing
        // captured yet, the dominant axis is used outright.
        private (GraphConnectionAxis Source, GraphConnectionAxis Target) ResolveConnectionAxes(Point start, Point end)
        {
            double dx = Math.Abs(end.X - start.X);
            double dy = Math.Abs(end.Y - start.Y);
            GraphConnectionAxis dominant = dy > dx ? GraphConnectionAxis.Vertical : GraphConnectionAxis.Horizontal;
            return (
                ResolveAxis(m_SourceExitAxis, dominant, dx, dy),
                ResolveAxis(m_TargetEntryAxis, dominant, dx, dy));
        }

        // Keep the captured (pre-drag) axis unless the current arrangement exceeds the flip ratio
        // against it, in which case fall back to the dominant axis. Nothing captured -> dominant axis.
        private static GraphConnectionAxis ResolveAxis(GraphConnectionAxis? captured, GraphConnectionAxis dominant, double dx, double dy)
        {
            if (captured is not GraphConnectionAxis axis)
            {
                return dominant;
            }
            if (axis == GraphConnectionAxis.Horizontal && dy > c_AxisFlipRatio * dx)
            {
                return GraphConnectionAxis.Vertical;
            }
            if (axis == GraphConnectionAxis.Vertical && dx > c_AxisFlipRatio * dy)
            {
                return GraphConnectionAxis.Horizontal;
            }
            return axis;
        }

        // The axis a routed edge leaves its source along, from the first segment's start tangent
        // (Control1 - Start), falling back to the segment chord if that is degenerate.
        private static GraphConnectionAxis ExitAxis(GraphEdgeSegment first)
        {
            double dx = Math.Abs(first.Control1.X - first.Start.X);
            double dy = Math.Abs(first.Control1.Y - first.Start.Y);
            if (dx < 1e-6 && dy < 1e-6)
            {
                dx = Math.Abs(first.End.X - first.Start.X);
                dy = Math.Abs(first.End.Y - first.Start.Y);
            }
            return dy > dx ? GraphConnectionAxis.Vertical : GraphConnectionAxis.Horizontal;
        }

        // The axis a routed edge enters its target along, from the last segment's end tangent
        // (End - Control2), falling back to the segment chord if that is degenerate.
        private static GraphConnectionAxis EntryAxis(GraphEdgeSegment last)
        {
            double dx = Math.Abs(last.End.X - last.Control2.X);
            double dy = Math.Abs(last.End.Y - last.Control2.Y);
            if (dx < 1e-6 && dy < 1e-6)
            {
                dx = Math.Abs(last.End.X - last.Start.X);
                dy = Math.Abs(last.End.Y - last.Start.Y);
            }
            return dy > dx ? GraphConnectionAxis.Vertical : GraphConnectionAxis.Horizontal;
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
            IReadOnlyList<GraphEdgeSegment> segments = EdgeSegments;
            Point tip = segments[^1].End;

            // Aim the head along the curve measured over the last c_ArrowTangentSpan pixels of travel,
            // not the final control leg: that leg can be a tiny, horizontally-pinned nub (where a
            // near-vertical spline/rectilinear edge meets the node), which would otherwise snap the head
            // sideways. Walking a meaningful span back keeps the head glued to the visible line for any
            // orientation, and for an exact MSAGL bezier it tracks the true tip tangent.
            Point anchor = GraphEdgeGeometry.AnchorBeforeEnd(segments, c_ArrowTangentSpan);
            double dx = tip.X - anchor.X;
            double dy = tip.Y - anchor.Y;
            double length = Math.Sqrt((dx * dx) + (dy * dy));

            if (length < 1e-6)
            {
                // Degenerate (zero-length path); fall back to the chord direction.
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
