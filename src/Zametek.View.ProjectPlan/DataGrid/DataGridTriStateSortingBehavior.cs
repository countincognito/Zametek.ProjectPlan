using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Zametek.View.ProjectPlan
{
    public class DataGridTriStateSortingBehavior
        : Behavior<DataGrid>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject?.Sorting += OnSorting;
        }

        protected override void OnDetaching()
        {
            AssociatedObject?.Sorting -= OnSorting;
            base.OnDetaching();
        }

        private void OnSorting(object? sender, DataGridColumnEventArgs e)
        {
            IDataGridCollectionView? collectionView = AssociatedObject?.CollectionView;

            if (collectionView is null)
            {
                return;
            }

            DataGridColumn column = e.Column;
            IComparer sortComparer = column.CustomSortComparer;

            if (sortComparer is null)
            {
                return;
            }

            e.Handled = true; // Block default 2-state toggle


            if (collectionView.SortDescriptions.Count == 0)
            {
                collectionView.SortDescriptions.Add(new DataGridComparerSortDescription(sortComparer, ListSortDirection.Ascending));
                return;
            }






            //// Cycle: Null -> Ascending -> Descending -> Null
            //if (collectionView.SortDescriptions.Count == 0
            //    || collectionView.SortDescriptions.)
            //{
            //    collectionView.SortDescriptions.Clear();
            //    collectionView.SortDescriptions.Add(new DataGridComparerSortDescription(sortComparer, ListSortDirection.Ascending));
            //}
            ////else if (column.SortDirection == ListSortDirection.Ascending)
            ////{
            ////    column.SortDirection = ListSortDirection.Descending;
            ////    ApplySort(collectionView, sortComparer, ListSortDirection.Descending);
            ////collectionView.SortDescriptions.Add(new DataGridComparerSortDescription(sortComparer, ListSortDirection.Descending));
            ////}
            //else
            //{
            //    //column.SortDirection = null;
            //    collectionView.SortDescriptions.Clear();
            //}
        }
    }
}
