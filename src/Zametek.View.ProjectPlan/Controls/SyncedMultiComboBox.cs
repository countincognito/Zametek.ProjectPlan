using Avalonia.Controls;
using System;
using Ursa.Controls;

namespace Zametek.View.ProjectPlan
{
    /// <summary>
    /// A MultiComboBox in which SelectedItems is the only authority on what is selected.
    /// </summary>
    /// <remarks>
    /// In Ursa 2.2.0 an item of the dropdown keeps its own IsSelected, and when it attaches to the logical tree it adds its data
    /// to SelectedItems if IsSelected is set. The state of the item can be old: the control reuses the item of a data object, it
    /// does not tell an item that is out of the panel about a change of SelectedItems, and it stops listening to SelectedItems
    /// for good when it unloads. So after a view model cleared SelectedItems, the next dropdown put the old selection back, as if
    /// the user had clicked it. For the chart filters, this wrote the filter of one scenario into another (ChartFilterTests).
    /// </remarks>
    public class SyncedMultiComboBox
        : MultiComboBox
    {
        protected override Type StyleKeyOverride => typeof(MultiComboBox);

        protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
            new SyncedMultiComboBoxItem();
    }
}
