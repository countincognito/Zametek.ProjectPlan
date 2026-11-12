using ScottPlot;
using ScottPlot.Plottables;

namespace Zametek.ViewModel.ProjectPlan
{
    public class AnnotatedMarker
        : Marker
    {
        public string Annotation { get; set; } = string.Empty;

        // ScottPlot draws every plottable with one paint, and a dashed line leaves its dash on it. The fill of the next
        // marker then carries the dash. A raster canvas ignores a dash on a fill, but the SVG canvas of Skia writes
        // "Unsupported path effect in addPaint." to the standard error of the process. In the scenario chart, the markers of
        // the second axis come after the dashed curve of the first axis. A marker has no dash, so it clears the one that it
        // finds. ScottPlot declares the property as not nullable, but null is no path effect, as after a solid line.
        public override void Render(RenderPack rp)
        {
            ArgumentNullException.ThrowIfNull(rp);
            rp.Paint.SKPathEffect = null!;
            base.Render(rp);
        }
    }
}
