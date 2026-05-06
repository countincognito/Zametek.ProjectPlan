namespace Zametek.View.ProjectPlan
{
    public partial class EarnedValueChartManagerView
        : ScottPlotUserControl
    {
        public EarnedValueChartManagerView()
        {
            InitializeComponent();
            InitializePlotContainer(scottplot);
        }
    }
}
