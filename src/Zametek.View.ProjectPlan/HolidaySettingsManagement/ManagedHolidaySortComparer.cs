using Avalonia;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class ManagedHolidaySortComparer
        : CustomSortComparer<IManagedHolidayViewModel>
    {
        public static readonly StyledProperty<string> SortMemberPathProperty =
            AvaloniaProperty.Register<ManagedHolidaySortComparer, string>(nameof(SortMemberPath));

        public override string SortMemberPath
        {
            get { return GetValue(SortMemberPathProperty); }
            set { SetValue(SortMemberPathProperty, value); }
        }
    }
}
