using ScottPlot;

namespace Zametek.ViewModel.ProjectPlan
{
    public interface IScottPlotImageExporter
    {
        Task SavePlotImageAsync(Plot plot, string filename, int width, int height);

        // Render the plot to PNG bytes (used by the clipboard-copy path, which needs bytes not a file).
        Task<byte[]> RenderPlotImageAsync(Plot plot, int width, int height);
    }
}
