using Avalonia.Interactivity;

namespace Zametek.View.ProjectPlan
{
    public partial class ScheduleSimulationManagerView
        : ScottPlotUserControl
    {
        public ScheduleSimulationManagerView()
        {
            InitializeComponent();
            InitializePlotContainer(scottplot);
        }

        // Copy the chart image to the clipboard. The view-model renders the bytes (whole chart, the same sizing as
        // Save-As); the base ScottPlotUserControl does the defensive clipboard write.
        private async void CopyImage_Click(object? sender, RoutedEventArgs e)
        {
            await CopyImageToClipboardAsync();
        }
    }
}
