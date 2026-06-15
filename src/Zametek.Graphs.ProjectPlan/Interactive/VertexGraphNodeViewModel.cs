using Avalonia.Collections;
using Avalonia.Media;
using ReactiveUI;

namespace Zametek.Graphs.ProjectPlan
{
    // Interactive, draggable, selectable activity node. The slack/override border colour and
    // critical/dummy dash style are preserved; selection is shown via a separate overlay ring
    // (in the view) so it does not clobber the underlying colour. Dimming is driven by opacity.
    public class VertexGraphNodeViewModel
        : ReactiveObject
    {
        private const double c_DimmedOpacity = 0.25;

        public VertexGraphNodeViewModel(GraphNodeLayoutModel layout)
        {
            ArgumentNullException.ThrowIfNull(layout);
            Id = layout.Id;
            m_X = layout.X;
            m_Y = layout.Y;
            Width = layout.Width;
            Height = layout.Height;
            Label = layout.Label;
            Name = layout.Name;
            Tooltip = layout.Tooltip;
            FillBrush = ToBrush(layout.FillColorHexCode, Colors.LightGray);
            BorderBrush = ToBrush(layout.BorderColorHexCode, Colors.Black);
            BorderThickness = layout.BorderThickness <= 0.0 ? 1.0 : layout.BorderThickness;
            StrokeDashArray = layout.IsDashed ? [3.0, 2.0] : null;
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

        public double NodeOpacity => IsDimmed ? c_DimmedOpacity : 1.0;

        private static IBrush ToBrush(string? hexCode, Color fallback)
        {
            Color color = fallback;
            if (!string.IsNullOrWhiteSpace(hexCode))
            {
                color = ColorHelper.HtmlHexCodeToColor(hexCode);
            }
            return new SolidColorBrush(color);
        }
    }
}
