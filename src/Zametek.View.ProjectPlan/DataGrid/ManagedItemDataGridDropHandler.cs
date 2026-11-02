using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactions.DragAndDrop;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Zametek.View.ProjectPlan
{
    // https://wieslawsoltes.github.io/Xaml.Behaviors/articles/drag-and-drop-datagrid/datagrid-drag-and-drop-overview.html
    public class ManagedItemDataGridDropHandler<T>
        :  BaseDataGridDropHandler<T> where T : class
    {
        protected override bool Validate(
            DataGrid dg,
            DragEventArgs e,
            object? sourceContext,
            object? targetContext,
            bool execute)
        {
            // Only move/reorder is supported: ContextDragBehavior also offers
            // Copy (Ctrl) and Link (Alt) drags, and accepting those would route
            // the drop to MakeCopy, which cannot mint a meaningful duplicate of
            // a managed item (IDs are unique). Rejecting here shows the no-drop
            // cursor instead.
            if (e is null
                || !e.DragEffects.HasFlag(DragDropEffects.Move))
            {
                return false;
            }

            // Validate that we are dragging an ItemViewModel and dropping onto an ObservableCollection
            if (sourceContext is T sourceItem
                && dg.ItemsSource is ObservableCollection<T> items)
            {
                // If we are just validating (execute=false), return true to indicate drop is allowed
                if (!execute)
                {
                    return true;
                }

                // If executing, perform the move
                // targetContext is the item we are dropping onto (or null if empty/not on a row)

                if ((e.Source as Control)?.DataContext is not T targetItem)
                {
                    return false;
                }

                if (targetItem is IEditableObject editable)
                {
                    // Helper method from BaseDataGridDropHandler to handle Move/Copy logic
                    // It calculates indices and moves the item in the collection
                    bool isValid = RunDropAction(dg, e, execute, sourceItem, targetItem, items);

                    if (isValid)
                    {
                        editable.BeginEdit();
                        editable.EndEdit();
                    }

                    return isValid;
                }
            }
            return false;
        }

        protected override T MakeCopy(
            ObservableCollection<T> parentCollection,
            T item)
        {
            // Unreachable: Validate rejects every drag that is not a plain
            // Move, so the base handler can never route a Copy drop here.
            throw new NotSupportedException();
        }
    }
}
