using ScottPlot.Plottables;

namespace Zametek.ViewModel.ProjectPlan
{
    public class AnnotatedRectangle
        : Rectangle
    {
        public string Annotation { get; set; } = string.Empty;
    }
}
