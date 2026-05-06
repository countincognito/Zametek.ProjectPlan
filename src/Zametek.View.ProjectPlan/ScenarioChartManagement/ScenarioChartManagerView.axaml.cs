namespace Zametek.View.ProjectPlan
{
    public partial class ScenarioChartManagerView
        : ScottPlotUserControl
    {
        public ScenarioChartManagerView()
        {
            InitializeComponent();
            InitializePlotContainer(scottplot);
        }
    }
}
