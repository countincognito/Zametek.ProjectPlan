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

        // The shortest duration that a drag of the end of the bar can give (EstimateHelper.ShortestDurationOfADrag), and
        // where the bar ends then. A bar with no end of its own to drag keeps 1, and no end.
        public int MinimumDuration { get; set; } = 1;

        public double? MinimumValue { get; set; }
    }
}
