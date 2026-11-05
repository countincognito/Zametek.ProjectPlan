using System.Globalization;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// How a list shows an activity. The Show Connections list of the Gantt chart and the lists of the Effort Tracking tab use these
    /// two rules, and so they show an activity in the same way.
    /// </summary>
    public static class ActivityLabelHelper
    {
        /// <summary>
        /// The name of the activity. The number of the activity stands for a name that is blank.
        /// </summary>
        public static string BuildDisplayName(
            int activityId,
            string? activityName)
        {
            return string.IsNullOrWhiteSpace(activityName) ? activityId.ToString(CultureInfo.InvariantCulture) : activityName;
        }

        /// <summary>
        /// The number of the activity, a dash and the display name, e.g. "12 - Backend". An activity with a blank name shows its
        /// number two times: "12 - 12".
        /// </summary>
        public static string BuildLabel(
            int activityId,
            string? activityName)
        {
            return $@"{activityId.ToString(CultureInfo.InvariantCulture)} - {BuildDisplayName(activityId, activityName)}";
        }
    }
}
