using Avalonia.Controls;
using OxyPlot;

namespace Zametek.View.ProjectPlan
{
    public partial class EarnedValueChartManagerView
        : UserControl
    {
        public EarnedValueChartManagerView()
        {
            InitializeComponent();
            oxyplot.ActualController.UnbindMouseDown(OxyMouseButton.Right);
            oxyplot.ActualController.UnbindMouseWheel();
        }
    }
}
