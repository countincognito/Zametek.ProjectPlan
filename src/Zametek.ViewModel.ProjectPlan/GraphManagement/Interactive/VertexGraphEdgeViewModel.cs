using Avalonia;
using Avalonia.Media;
using ReactiveUI;

namespace Zametek.ViewModel.ProjectPlan
{
    // Spike: an edge drawn as a straight line between two node centres. The endpoints
    // are derived from the node positions and update live as nodes are dragged, so the
    // line follows. (MSAGL's routed splines are not used here, which is the trade-off
    // that makes dragging trivially correct.)
    public class VertexGraphEdgeViewModel
        : ReactiveObject, IDisposable
    {
        private const double c_DimmedOpacity = 0.15;
        private const double c_HighlightThickness = 2.5;
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
            double strokeThickness)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);
            Id = id;
            m_Source = source;
            m_Target = target;
            m_BaseThickness = strokeThickness <= 0.0 ? 1.0 : strokeThickness;

            m_SourceSub = m_Source
                .WhenAnyValue(x => x.X, x => x.Y)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(StartPoint)));
            m_TargetSub = m_Target
                .WhenAnyValue(x => x.X, x => x.Y)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(EndPoint)));
        }

        public int Id { get; }

        public int SourceId => m_Source.Id;

        public int TargetId => m_Target.Id;

        public Point StartPoint => new(m_Source.CentreX, m_Source.CentreY);

        public Point EndPoint => new(m_Target.CentreX, m_Target.CentreY);

        public IBrush Stroke => IsHighlighted ? s_HighlightBrush : s_BaseBrush;

        public double StrokeThickness => IsHighlighted ? c_HighlightThickness : m_BaseThickness;

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

        public void Dispose()
        {
            m_SourceSub.Dispose();
            m_TargetSub.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
