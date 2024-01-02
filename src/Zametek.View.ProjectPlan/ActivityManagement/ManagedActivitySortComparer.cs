using Avalonia;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class ManagedActivitySortComparer
        : CustomSortComparer<IManagedActivityViewModel>
    {
        public static readonly StyledProperty<string> SortMemberPathProperty =
            AvaloniaProperty.Register<ManagedActivitySortComparer, string>(nameof(SortMemberPath));

        public override string SortMemberPath
        {
            get { return GetValue(SortMemberPathProperty); }
            set { SetValue(SortMemberPathProperty, value); }
        }
    }
}
