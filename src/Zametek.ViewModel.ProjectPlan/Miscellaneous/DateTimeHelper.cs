namespace Zametek.ViewModel.ProjectPlan
{
    public static class DateTimeHelper
    {
        public static bool IsBeforeOrOn(this DateOnly current, DateOnly toCompareWith) => current <= toCompareWith;

        public static bool IsAfterOrOn(this DateOnly current, DateOnly toCompareWith) => current >= toCompareWith;

        public static bool IsBeforeOrOn(this DateTime current, DateTime toCompareWith) => current <= toCompareWith;

        public static bool IsAfterOrOn(this DateTime current, DateTime toCompareWith) => current >= toCompareWith;

        public static bool IsBefore(this DateTimeOffset current, DateTimeOffset toCompareWith) => current < toCompareWith;

        public static bool IsAfter(this DateTimeOffset current, DateTimeOffset toCompareWith) => current > toCompareWith;

        public static bool IsAfterOrOn(this DateTimeOffset current, DateTimeOffset toCompareWith) => current >= toCompareWith;
    }
}
