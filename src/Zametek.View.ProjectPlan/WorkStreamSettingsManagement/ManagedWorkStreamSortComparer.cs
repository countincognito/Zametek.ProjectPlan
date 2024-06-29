using Avalonia;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class ManagedWorkStreamSortComparer
        : CustomSortComparer<IManagedWorkStreamViewModel>
    {
        public static readonly StyledProperty<string> SortMemberPathProperty =
            AvaloniaProperty.Register<ManagedWorkStreamSortComparer, string>(nameof(SortMemberPath));

        public override string SortMemberPath
        {
            get { return GetValue(SortMemberPathProperty); }
            set { SetValue(SortMemberPathProperty, value); }
        }
    }
}
