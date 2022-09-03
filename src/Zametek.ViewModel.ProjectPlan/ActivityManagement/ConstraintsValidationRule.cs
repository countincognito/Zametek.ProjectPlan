namespace Zametek.ViewModel.ProjectPlan
{
    public class ConstraintsValidationRule
    {
        public static string? Validate(
            int? minimumFreeSlack,
            int? minimumEarliestStartTime,
            int? maximumLatestFinishTime,
            int duration)
        {
            if (minimumFreeSlack.HasValue && maximumLatestFinishTime.HasValue)
            {
                return Resource.ProjectPlan.Labels.Label_CannotSetMinimumFreeSlackAndMaximumLatestFinishTimeAtSameTime;
            }

            if (minimumEarliestStartTime.HasValue && maximumLatestFinishTime.HasValue
                && (maximumLatestFinishTime.Value - minimumEarliestStartTime.Value) < duration)
            {
                return Resource.ProjectPlan.Labels.Label_MinimumEarliestStartTimeToMaximumLatestFinishTimeMustBeGreaterThanOrEqualToDuration;
            }

            return null;
        }
    }
}
