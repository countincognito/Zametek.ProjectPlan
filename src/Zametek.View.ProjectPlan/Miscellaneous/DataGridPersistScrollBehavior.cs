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
    //
    // SEAMLESS RESTORE: Avalonia's DataGrid exposes no public API to set the vertical offset
    // before its first paint (only ScrollIntoView, which needs realised rows, so it runs a frame
    // late and produces a visible "row 1 -> jump" flash). Rather than reflect into the private
    // offset, we get the same result with public APIs: the grid is hidden (via ViewFade) from the
    // moment it attaches - before its first paint - while the ScrollIntoView restore runs, then
    // faded in once the target row is in place. So the grid never appears at the wrong position;
    // it simply fades in already scrolled. This dovetails with FadeInBehavior, which fades the whole
    // tab view in on activation: an unscrolled grid is not hidden here and simply fades in with its
    // view; a scrolled grid is held hidden until the restore settles, then fades in under the view.
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

        //// Whether we hid the grid on attach (so it must be revealed once the restore settles).
        //private bool m_HidForRestore;

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

            //// If this grid has a scroll position to restore, hide it now - before its first paint,
            //// since OnAttached runs during view construction - so the user never sees it at row 1
            //// before the (deferred) ScrollIntoView restore takes effect. It is revealed with a quick
            //// fade once the restore settles (RevealIfHidden). Grids with nothing to restore are left
            //// visible and simply fade in with their view (see FadeInBehavior).
            //if (m_DataGridManager.GetScrollItem(m_GridName) is not null)
            //{
            //    ViewFade.Hide(m_DataGrid);
            //    m_HidForRestore = true;
            //}
        }

        protected override void OnDetaching()
        {
            if (m_DataGrid is not null)
            {
                m_DataGrid.Loaded -= OnLoaded;
                m_DataGrid.LayoutUpdated -= OnLayoutUpdated;
                m_DataGrid.TemplateApplied -= OnTemplateApplied;
                //// Never leave a detached grid hidden.
                //ViewFade.Reveal(m_DataGrid);
            }
            m_VerticalScrollBar = null;
            //m_HidForRestore = false;
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

        // Records the item currently at the top of the viewport so it can be restored when the grid
        // is rebuilt on a tab change. When the grid is at (or within half a row of) the top there is
        // nothing to restore, so the stored value is cleared - that keeps unscrolled grids off the
        // hide path so they simply fade in with their view rather than being held hidden. Skipped
        // while a restore is in flight so the freshly-loaded (top) position does not overwrite the
        // value we are about to apply.
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

            //// At (or effectively at) the top there is nothing worth restoring: clear the stored
            //// position so the grid is not hidden on the next load.
            //if (scrollBarValue < rowHeight * c_RowScrollThreshold)
            //{
            //    m_DataGridManager.SetScrollItem(m_GridName, null);
            //    return;
            //}

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

        // Restores the cached top item. The actual scroll is deferred to Background priority so it
        // runs after the grid has realised its rows; a guard flag suppresses interim saves until the
        // restore has settled, and (if the grid was hidden on attach) the reveal fade runs once it has.
        private void RestoreScrollPosition()
        {
            if (m_DataGrid is null
                || string.IsNullOrEmpty(m_GridName))
            {
                //RevealIfHidden();
                return;
            }

            object? topItem = m_DataGridManager.GetScrollItem(m_GridName);

            if (topItem is null)
            {
                //RevealIfHidden();
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

            // Lift the guard after the restore above has run (same priority, queued later), and fade
            // the grid back in now that it is sitting at the restored position.
            Dispatcher.UIThread.Post(
                () =>
                {
                    m_IsRestoringScroll = false;
                    //RevealIfHidden();
                },
                DispatcherPriority.Background);
        }

        //// Fade the grid back in if it was hidden on attach for a seamless restore. Idempotent: only
        //// the first call (while m_HidForRestore is set) animates; later calls are no-ops.
        //private void RevealIfHidden()
        //{
        //    if (!m_HidForRestore
        //        || m_DataGrid is null)
        //    {
        //        return;
        //    }

        //    m_HidForRestore = false;
        //    // The transition installed by ViewFade.Hide animates this 0 -> 1 change into a quick fade.
        //    ViewFade.Reveal(m_DataGrid);
        //}
    }
}
