namespace Zametek.ViewModel.ProjectPlan
{
    public class ConstraintsValidationRule
    {
        public static string? ValidateMinimumFreeSlack(
            int? minimumFreeSlack,
            int? minimumEarliestStartTime,
            int? maximumLatestFinishTime)
        {
            if (minimumFreeSlack.HasValue && maximumLatestFinishTime.HasValue)
            {
                return Resource.ProjectPlan.Labels.Label_CannotSetMinimumFreeSlackAndMaximumLatestFinishTimeAtSameTime;
            }

            return null;
        }

        public static string? ValidateDuration(
            int? minimumEarliestStartTime,
            int? maximumLatestFinishTime,
            int duration)
        {
            if (minimumEarliestStartTime.HasValue && maximumLatestFinishTime.HasValue
                && (maximumLatestFinishTime.Value - minimumEarliestStartTime.Value) < duration)
            {
                return Resource.ProjectPlan.Labels.Label_MinimumEarliestStartTimeToMaximumLatestFinishTimeMustBeGreaterThanOrEqualToDuration;
            }

            return null;
        }
    }
}
