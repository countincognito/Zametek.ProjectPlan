using System.ComponentModel;

namespace Zametek.Contract.ProjectPlan
{
    /// <summary>
    /// A single editable day cell in a resource's timesheet row: the
    /// percentage of that day the resource worked on the row's activity.
    /// Null means no booking at all (distinct from an explicit zero).
    /// </summary>
    public interface ITimesheetCellViewModel
        : INotifyPropertyChanged
    {
        int DayOffset { get; }

        int? PercentageWorked { get; set; }
    }
}
