namespace Zametek.View.ProjectPlan
{
    public partial class ResourceChartManagerView
        : ScottPlotUserControl
    {
        public ResourceChartManagerView()
        {
            InitializeComponent();
            InitializePlotContainer(scottplot);
        }
    }
}
