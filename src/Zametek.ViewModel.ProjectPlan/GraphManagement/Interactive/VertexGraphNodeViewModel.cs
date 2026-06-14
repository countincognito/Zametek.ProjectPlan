using Avalonia;
using Avalonia.Media;
using ReactiveUI;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    // Spike: a single draggable, selectable activity node in the interactive vertex graph.
    // Selection visuals (border, opacity) are computed here and bound as plain values, so
    // they survive without colliding with style setters.
    public class VertexGraphNodeViewModel
        : ReactiveObject
    {
        private const double c_DimmedOpacity = 0.25;
        private const double c_SelectedBorderThickness = 3.0;
        private static readonly IBrush s_SelectionBrush = new SolidColorBrush(Color.Parse(@"#0078D4"));

        private readonly IBrush m_BaseBorderBrush;
        private readonly Thickness m_BaseBorderThickness;

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
            m_BaseBorderBrush = ToBrush(layout.BorderColorHexCode, Colors.Black);
            m_BaseBorderThickness = new Thickness(layout.BorderThickness <= 0.0 ? 1.0 : layout.BorderThickness);
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

        public IBrush BorderBrush => IsSelected ? s_SelectionBrush : m_BaseBorderBrush;

        public Thickness BorderThickness => IsSelected ? new Thickness(c_SelectedBorderThickness) : m_BaseBorderThickness;

        public double NodeOpacity => IsDimmed ? c_DimmedOpacity : 1.0;

        private bool m_IsSelected;
        public bool IsSelected
        {
            get => m_IsSelected;
            set
            {
                this.RaiseAndSetIfChanged(ref m_IsSelected, value);
                this.RaisePropertyChanged(nameof(BorderBrush));
                this.RaisePropertyChanged(nameof(BorderThickness));
            }
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

        private static IBrush ToBrush(string? hexCode, Color fallback)
        {
            Color color = fallback;
            if (!string.IsNullOrWhiteSpace(hexCode))
            {
                ColorFormatModel colorFormat = ColorHelper.HtmlHexCodeToColorFormat(hexCode);
                color = ColorHelper.ColorFormatToAvaloniaColor(colorFormat);
            }
            return new SolidColorBrush(color);
        }
    }
}
