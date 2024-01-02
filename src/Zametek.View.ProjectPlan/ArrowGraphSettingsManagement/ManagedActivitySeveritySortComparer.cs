using Avalonia;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class ManagedActivitySeveritySortComparer
        : CustomSortComparer<IManagedActivitySeverityViewModel>
    {
        public static readonly StyledProperty<string> SortMemberPathProperty =
            AvaloniaProperty.Register<ManagedActivitySeveritySortComparer, string>(nameof(SortMemberPath));

        public override string SortMemberPath
        {
            get { return GetValue(SortMemberPathProperty); }
            set { SetValue(SortMemberPathProperty, value); }
        }
    }
}
