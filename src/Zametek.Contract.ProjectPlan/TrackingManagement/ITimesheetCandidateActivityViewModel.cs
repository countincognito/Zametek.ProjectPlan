namespace Zametek.Contract.ProjectPlan
{
    /// <summary>
    /// An activity that can be added as a new row to a resource's timesheet
    /// section (i.e. one the section does not already have a row for).
    /// </summary>
    public interface ITimesheetCandidateActivityViewModel
    {
        int Id { get; }

        string DisplayName { get; }
    }
}
