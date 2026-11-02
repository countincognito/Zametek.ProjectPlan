using Avalonia.LogicalTree;
using System;
using Ursa.Controls;

namespace Zametek.View.ProjectPlan
{
    /// <summary>
    /// An item of a <see cref="SyncedMultiComboBox"/>. It does not add its data to SelectedItems when it attaches.
    /// </summary>
    public class SyncedMultiComboBoxItem
        : MultiComboBoxItem
    {
        protected override Type StyleKeyOverride => typeof(MultiComboBoxItem);

        protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            // Before the base class reads IsSelected. This changes nothing in SelectedItems, because the data is not in it.
            if (IsSelected
                && this.FindLogicalAncestorOfType<MultiComboBox>()?.SelectedItems?.Contains(DataContext) == false)
            {
                SetCurrentValue(IsSelectedProperty, false);
            }

            base.OnAttachedToLogicalTree(e);
        }
    }
}
