using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;
using System;
using System.Linq;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    // Persists a DataGrid's vertical scroll position (the top visible item) in memory via
    // IDataGridScrollManager, keyed by grid name, so it survives the view being re-materialised
    // on a tab change. Column layout is handled separately by DataGridPersistLayoutBehavior.
    // Mechanics are adapted from the former DataGridCacheScrollBehavior; only the storage
    // location differs (the singleton manager rather than a per-behavior field).
    public class DataGridPersistScrollBehavior
        : Behavior<DataGrid>
    {
        private string m_GridName;
        private readonly IDataGridScrollManager m_DataGridManager;
        private DataGrid? m_DataGrid;

        private ScrollBar? m_VerticalScrollBar;
        private bool m_IsRestoringScroll;
        private const double c_RowHeightCorrection = 1.0;
        private const double c_RowScrollThreshold = 0.5;

        public DataGridPersistScrollBehavior(IDataGridScrollManager dataGridManager)
        {
            m_DataGridManager = dataGridManager ?? throw new ArgumentNullException(nameof(dataGridManager));
            m_GridName = string.Empty;
            m_DataGrid = null;
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            m_DataGrid = AssociatedObject;
            m_GridName = m_DataGrid?.Name ?? string.Empty;

            if (m_DataGrid is null
                || string.IsNullOrEmpty(m_GridName))
            {
                return;
            }

            // Restore the cached scroll position when the DataGrid is loaded
            m_DataGrid.Loaded += OnLoaded;

            // Record the scroll position as the layout changes
            m_DataGrid.LayoutUpdated += OnLayoutUpdated;

            // Capture the vertical scrollbar from the template for scroll persistence
            m_DataGrid.TemplateApplied += OnTemplateApplied;
        }

        protected override void OnDetaching()
        {
            if (m_DataGrid is not null)
            {
                m_DataGrid.Loaded -= OnLoaded;
                m_DataGrid.LayoutUpdated -= OnLayoutUpdated;
                m_DataGrid.TemplateApplied -= OnTemplateApplied;
            }
            m_VerticalScrollBar = null;
            m_GridName = string.Empty;
            base.OnDetaching();
        }

        private void OnLoaded(
            object? sender,
            RoutedEventArgs e)
        {
            RestoreScrollPosition();
        }

        private void OnLayoutUpdated(
            object? sender,
            EventArgs e)
        {
            SaveScrollPosition();
        }

        private void OnTemplateApplied(
            object? sender,
            TemplateAppliedEventArgs e)
        {
            m_VerticalScrollBar = e.NameScope.Find<ScrollBar>(@"PART_VerticalScrollbar");
        }

        // Records the item currently at the top of the viewport so it can be restored when
        // the grid is rebuilt on a tab change. Skipped while a restore is in flight so the
        // freshly-loaded (top) position does not overwrite the value we are about to apply.
        private void SaveScrollPosition()
        {
            if (m_IsRestoringScroll
                || m_DataGrid is null
                || m_VerticalScrollBar is null
                || !m_DataGrid.IsLoaded
                || string.IsNullOrEmpty(m_GridName)
                || m_DataGrid.ItemsSource is null)
            {
                return;
            }

            double rowHeight = m_DataGrid.RowHeight + c_RowHeightCorrection;

            if (rowHeight <= 0.0)
            {
                return;
            }

            double scrollBarValue = m_VerticalScrollBar.Value;
            object? topItem = null;
            double scrollValue = 0.0;

            foreach (object item in m_DataGrid.ItemsSource)
            {
                // Cache to this row if the scroll position is within the threshold of it,
                // otherwise to the next row once the running offset passes the scroll value.
                if ((scrollBarValue - scrollValue) / rowHeight < c_RowScrollThreshold)
                {
                    topItem = item;
                    break;
                }
                else if (scrollValue >= scrollBarValue)
                {
                    topItem = item;
                    break;
                }

                scrollValue += rowHeight;
            }

            m_DataGridManager.SetScrollItem(m_GridName, topItem);
        }

        // Restores the cached top item. The actual scroll is deferred to Background priority
        // so it runs after the grid has realised its rows; a guard flag suppresses interim
        // saves until the restore has settled.
        private void RestoreScrollPosition()
        {
            if (m_DataGrid is null
                || string.IsNullOrEmpty(m_GridName))
            {
                return;
            }

            object? topItem = m_DataGridManager.GetScrollItem(m_GridName);

            if (topItem is null)
            {
                return;
            }

            m_IsRestoringScroll = true;

            Dispatcher.UIThread.Post(() =>
            {
                if (m_DataGrid is not null
                    && m_DataGrid.ItemsSource is not null)
                {
                    object? lastItem = m_DataGrid.ItemsSource.Cast<object>().LastOrDefault();

                    // Scroll to the last item first, then to the target, so the target
                    // settles near the top of the viewport rather than just into view.
                    if (lastItem is not null)
                    {
                        m_DataGrid.ScrollIntoView(lastItem, null);
                    }

                    m_DataGrid.ScrollIntoView(topItem, null);
                }
            }, DispatcherPriority.Background);

            // Lift the guard after the restore above has run (same priority, queued later).
            Dispatcher.UIThread.Post(() => m_IsRestoringScroll = false, DispatcherPriority.Background);
        }
    }
}
