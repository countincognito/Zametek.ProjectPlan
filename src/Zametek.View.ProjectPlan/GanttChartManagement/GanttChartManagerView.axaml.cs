namespace Zametek.View.ProjectPlan
{
    public partial class GanttChartManagerView
        : ScottPlotUserControl
    {
        public GanttChartManagerView()
        {
            InitializeComponent();
            InitializePlotContainer(scottplot);
        }
    }
}
