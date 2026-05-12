using ScottPlot;

namespace Zametek.ViewModel.ProjectPlan
{
    public class AnnotatedBar
        : Bar
    {
        public string Annotation { get; set; } = string.Empty;

        public int ActivityId { get; set; }

        public int? StartTime { get; set; }

        public int Duration { get; set; }
    }
}
