using Zametek.Common.ProjectPlan;

namespace Zametek.Engine.ProjectPlan
{
    // A chart to produce, and its size in pixels.
    public record ChartOutputRequest(
        ChartImageFormat Format,
        int Width,
        int Height);
}
