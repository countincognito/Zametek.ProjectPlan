using ScottPlot;

namespace Zametek.ViewModel.ProjectPlan
{
    public class AnnotatedBar
        : Bar
    {
        public string Annotation { get; set; } = string.Empty;

        /// <summary>
        /// The ID of the activity this bar represents. Zero means no activity (e.g. padding bars).
        /// </summary>
        public int ActivityId { get; set; } = 0;

        /// <summary>
        /// The start time (in project days) of the activity, used to compute duration from drag.
        /// </summary>
        public int ActivityStartTime { get; set; } = 0;

        /// <summary>
        /// The original duration (in project days) of the activity.
        /// </summary>
        public int ActivityDuration { get; set; } = 0;
    }
}
