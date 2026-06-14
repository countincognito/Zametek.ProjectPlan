using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;
using ReactiveUI;

namespace Zametek.Graphs.ProjectPlan
{
    // A directed edge drawn as a straight line clipped to the source and target node borders,
    // with an arrowhead at the target. Endpoints are derived from the node positions and update
    // live as nodes are dragged, so the line and arrow follow. (MSAGL's routed splines are not
    // used here, which is the trade-off that makes dragging trivially correct.)
    public class VertexGraphEdgeViewModel
        : ReactiveObject, IDisposable
    {
        private const double c_DimmedOpacity = 0.15;
        private const double c_HighlightThickness = 2.5;
        private const double c_ArrowLength = 9.0;
        private const double c_ArrowHalfWidth = 4.5;
        private static readonly IBrush s_BaseBrush = new SolidColorBrush(Colors.Gray);
        private static readonly IBrush s_HighlightBrush = new SolidColorBrush(Color.Parse(@"#0078D4"));

        private readonly VertexGraphNodeViewModel m_Source;
        private readonly VertexGraphNodeViewModel m_Target;
        private readonly double m_BaseThickness;
        private readonly IDisposable m_SourceSub;
        private readonly IDisposable m_TargetSub;

        public VertexGraphEdgeViewModel(
            int id,
            VertexGraphNodeViewModel source,
            VertexGraphNodeViewModel target,
            double strokeThickness,
            bool isDashed = false)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);
            Id = id;
            m_Source = source;
            m_Target = target;
            m_BaseThickness = strokeThickness <= 0.0 ? 1.0 : strokeThickness;
            StrokeDashArray = isDashed ? [3.0, 2.0] : null;

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

        public IBrush Stroke => IsHighlighted ? s_HighlightBrush : s_BaseBrush;

        public double StrokeThickness => IsHighlighted ? c_HighlightThickness : m_BaseThickness;

        // Neutral (unselected, undimmed) appearance, used when exporting the graph image so the
        // export does not depend on the current selection/highlight state.
        public IBrush BaseStroke => s_BaseBrush;

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
        }

        // Intersection of the centre-to-centre line with 'node's axis-aligned border rectangle.
        private static Point ClipToBorder(VertexGraphNodeViewModel node, VertexGraphNodeViewModel toward)
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

        public void Dispose()
        {
            m_SourceSub.Dispose();
            m_TargetSub.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
