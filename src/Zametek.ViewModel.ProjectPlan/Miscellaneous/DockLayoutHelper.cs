using Dock.Model.Controls;
using Dock.Model.Core;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class DockLayoutHelper
    {
        /// <summary>
        /// Collects the Ids of every tool dockable reachable from the given
        /// layout, including tools that are pinned, hidden, or floated into
        /// separate windows.
        /// </summary>
        public static HashSet<string> CollectToolIds(IDockable? layout)
        {
            HashSet<string> ids = new(StringComparer.Ordinal);
            Collect(layout, ids);
            return ids;
        }

        private static void Collect(
            IDockable? dockable,
            HashSet<string> ids)
        {
            if (dockable is null)
            {
                return;
            }

            if (dockable is ITool tool)
            {
                ids.Add(tool.Id);
                return;
            }

            if (dockable is IRootDock rootDock)
            {
                Collect(rootDock.PinnedDock, ids);

                foreach (IDockable hiddenDockable in rootDock.HiddenDockables ?? [])
                {
                    Collect(hiddenDockable, ids);
                }
                foreach (IDockable pinnedDockable in rootDock.LeftPinnedDockables ?? [])
                {
                    Collect(pinnedDockable, ids);
                }
                foreach (IDockable pinnedDockable in rootDock.RightPinnedDockables ?? [])
                {
                    Collect(pinnedDockable, ids);
                }
                foreach (IDockable pinnedDockable in rootDock.TopPinnedDockables ?? [])
                {
                    Collect(pinnedDockable, ids);
                }
                foreach (IDockable pinnedDockable in rootDock.BottomPinnedDockables ?? [])
                {
                    Collect(pinnedDockable, ids);
                }
                foreach (IDockWindow window in rootDock.Windows ?? [])
                {
                    Collect(window.Layout, ids);
                }
            }

            if (dockable is IDock dock)
            {
                foreach (IDockable child in dock.VisibleDockables ?? [])
                {
                    Collect(child, ids);
                }
            }
        }
    }
}
