using OxyPlot;
using System.Windows.Input;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IEarnedValueChartsManagerViewModel
    {        
        PlotModel EarnedValueChartPlotModel
        {
            get;
        }        
    }
}
