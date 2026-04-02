namespace Zametek.ViewModel.ProjectPlan
{
    public static class DateTimeHelper
    {
        public static bool IsBefore(this DateTime current, DateTime toCompareWith) => current < toCompareWith;

        public static bool IsAfter(this DateTime current, DateTime toCompareWith) => current > toCompareWith;

        public static bool IsBefore(this DateTimeOffset current, DateTimeOffset toCompareWith) => current < toCompareWith;

        public static bool IsAfter(this DateTimeOffset current, DateTimeOffset toCompareWith) => current > toCompareWith;
    }
}
