using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactions.DragAndDrop;
using System;
using System.Collections.ObjectModel;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    // https://wieslawsoltes.github.io/Xaml.Behaviors/articles/drag-and-drop-datagrid/datagrid-drag-and-drop-overview.html
    public class ManagedActivityDataGridDropHandler
        : BaseDataGridDropHandler<IManagedActivityViewModel>
    {
        protected override bool Validate(DataGrid dg, DragEventArgs e, object? sourceContext, object? targetContext, bool execute)
        {
            // Validate that we are dragging an ItemViewModel and dropping onto an ObservableCollection
            if (sourceContext is IManagedActivityViewModel sourceItem
                && dg.ItemsSource is ObservableCollection<IManagedActivityViewModel> items)
            {
                // If we are just validating (execute=false), return true to indicate drop is allowed
                if (!execute) return true;

                // If executing, perform the move
                // targetContext is the item we are dropping onto (or null if empty/not on a row)
                //var targetItem = targetContext as IManagedActivityViewModel;

                if (e is null
                    || (e.Source as Control)?.DataContext is not IManagedActivityViewModel targetItem)
                {
                    return false;
                }

                // Helper method from BaseDataGridDropHandler to handle Move/Copy logic
                // It calculates indices and moves the item in the collection
                bool isValid = RunDropAction(dg, e, execute, sourceItem, targetItem, items);

                if (isValid)
                {
                    targetItem.IsCompiled = false;
                }

                return isValid;
            }
            return false;
        }

        protected override IManagedActivityViewModel MakeCopy(
            ObservableCollection<IManagedActivityViewModel> parentCollection,
            IManagedActivityViewModel item)
        {
            throw new NotImplementedException();
        }
    }
}
