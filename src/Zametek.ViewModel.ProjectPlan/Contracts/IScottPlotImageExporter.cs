using ScottPlot;

namespace Zametek.ViewModel.ProjectPlan
{
    public interface IScottPlotImageExporter
    {
        Task SavePlotImageAsync(Plot plot, string filename, int width, int height);
    }
}
