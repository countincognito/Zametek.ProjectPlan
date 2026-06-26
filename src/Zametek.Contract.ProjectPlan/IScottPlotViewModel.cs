namespace Zametek.Contract.ProjectPlan
{
    public interface IScottPlotViewModel
    {
        Task<byte[]?> RenderChartImageAsync();

        Task ReportErrorAsync(string message);
    }
}
