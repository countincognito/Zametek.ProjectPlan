namespace Zametek.ProjectPlan.CommandLine
{
    // A chart a job asks for: its format, as zpp's --*-format gives it, and its size in pixels, as --*-size does.
    public record ChartOptions
    {
        public PlotExport Format { get; init; } = PlotExport.Jpeg;

        public required int Width { get; init; }

        public required int Height { get; init; }
    }
}
