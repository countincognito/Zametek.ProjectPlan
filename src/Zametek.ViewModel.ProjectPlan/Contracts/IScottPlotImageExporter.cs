using ScottPlot;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public interface IScottPlotImageExporter
    {
        // Write the plot to the stream in the given format. The caller owns the stream, which is left open.
        Task WritePlotImageAsync(Plot plot, Stream stream, ChartImageFormat format, int width, int height);

        // Render the plot to PNG bytes (used by the clipboard-copy path, which needs bytes not a file).
        Task<byte[]> RenderPlotImageAsync(Plot plot, int width, int height);
    }
}
