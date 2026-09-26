using Zametek.Common.ProjectPlan;

namespace Zametek.ProjectPlan.Engine
{
    // A chart to produce, and its size in pixels.
    public record ChartOutputRequest(
        ChartImageFormat Format,
        int Width,
        int Height);
}
