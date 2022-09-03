namespace Zametek.Contract.ProjectPlan
{
    public interface ITrackerViewModel
    {
        int Index { get; }

        int Time { get; }

        int ActivityId { get; }

        string DisplayName { get; }

        bool IsUpdated { get; set; }

        bool IsIncluded { get; set; }

        int PercentageComplete { get; set; }
    }
}
