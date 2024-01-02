using Avalonia;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class ManagedResourceSortComparer
        : CustomSortComparer<IManagedResourceViewModel>
    {
        public static readonly StyledProperty<string> SortMemberPathProperty =
            AvaloniaProperty.Register<ManagedResourceSortComparer, string>(nameof(SortMemberPath));

        public override string SortMemberPath
        {
            get { return GetValue(SortMemberPathProperty); }
            set { SetValue(SortMemberPathProperty, value); }
        }
    }
}
