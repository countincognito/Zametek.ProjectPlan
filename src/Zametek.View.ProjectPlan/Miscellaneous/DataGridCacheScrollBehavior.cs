using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.View.ProjectPlan
{
    /// <summary>
    /// Restores the scroll position of a DataGrid when it is reloaded.
    /// Original recommendation taken from here.
    /// https://www.reddit.com/r/AvaloniaUI/comments/1fc5qiw/datagrid_scroll
    /// </summary>
    public class DataGridCacheScrollBehavior
        : Behavior<DataGrid>
    {
        private ScrollBar? m_ScrollBar;
        private IEnumerable<ReactiveObject>? m_Items;
        private ReactiveObject? m_LastItem;
        private ReactiveObject? m_IndexItem;

        private double m_ScrollBarValue = 0.0;
        private double m_RowHeight = 0.0;

        private const double c_RowScrollThreshold = 0.5;
        private const double c_RowHeightCorrection = 1.0;

        protected override void OnAttached()
        {
            base.OnAttached();

            if (AssociatedObject is not null)
            {
                AssociatedObject.TemplateApplied += AssociatedObjectOnTemplateApplied;
            }
        }

        private void AssociatedObjectOnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
        {
            m_ScrollBar = e.NameScope.Find<ScrollBar>(@"PART_VerticalScrollbar");

            if (m_ScrollBar is not null
                && AssociatedObject is not null)
            {
                m_Items = AssociatedObject.ItemsSource.Cast<ReactiveObject>();
                AssociatedObject.LayoutUpdated += AssociatedObject_LayoutUpdated;
                AssociatedObject.Loaded += AssociatedObject_Loaded;
            }
        }

        private void AssociatedObject_LayoutUpdated(object? sender, EventArgs? e)
        {
            if (m_ScrollBar is not null
                && AssociatedObject is not null
                && AssociatedObject.IsLoaded)
            {
                m_ScrollBarValue = m_ScrollBar.Value;
                m_RowHeight = AssociatedObject.RowHeight + c_RowHeightCorrection;

                if (m_Items is not null
                    && m_RowHeight > 0.0)
                {
                    m_LastItem = m_Items.LastOrDefault();
                    m_IndexItem = null;
                    double scrollValue = 0.0;

                    foreach (ReactiveObject item in m_Items)
                    {
                        // Cache to a specific row if the scroll position
                        // is less than percentage threshold of the row height.
                        if ((m_ScrollBarValue - scrollValue) / m_RowHeight < c_RowScrollThreshold)
                        {
                            m_IndexItem = item;
                            break;
                        }
                        // Otherwise, cache to the next row.
                        else if (scrollValue >= m_ScrollBarValue)
                        {
                            m_IndexItem = item;
                            break;
                        }

                        scrollValue += m_RowHeight;
                    }
                }
            }
        }

        private void AssociatedObject_Loaded(object? sender, RoutedEventArgs e)
        {
            if (m_LastItem is not null
                && m_IndexItem is not null)
            {
                // Scroll to the last item, then to the index item.
                // This ensures that the index item appears near the top
                // of the datagrid.
                AssociatedObject?.ScrollIntoView(m_LastItem, null);
                AssociatedObject?.ScrollIntoView(m_IndexItem, null);
            }
        }
    }
}
