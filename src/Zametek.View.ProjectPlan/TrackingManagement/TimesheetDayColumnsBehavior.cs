using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    // Appends the editable day columns to a resource timesheet grid as each
    // one is created (the grids come from a data template, one per resource
    // section, so they are not reachable from the view's code-behind).
    public class TimesheetDayColumnsBehavior
        : Behavior<DataGrid>
    {
        // The static columns declared in markup: Id, Name, Find. The day
        // columns are appended after them.
        private const int c_StaticColumnCount = 3;

        protected override void OnAttached()
        {
            base.OnAttached();

            if (AssociatedObject is null
                || AssociatedObject.Columns.Count != c_StaticColumnCount)
            {
                return;
            }

            for (int i = 0; i < TimesheetHelper.DayCount; i++)
            {
                AssociatedObject.Columns.Add(new DataGridTimesheetDayColumn(i));
            }
        }
    }
}
