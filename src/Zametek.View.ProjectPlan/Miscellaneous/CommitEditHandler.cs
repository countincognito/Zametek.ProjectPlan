using Avalonia.Threading;
using System;
using System.Collections.Generic;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class CommitEditHandler
        : ICommitEditHandler
    {
        // Live grids register a commit action while they are attached to the
        // visual tree (DataGridCommitEditBehavior), so the handler always
        // addresses the grids actually on screen - docked or floated - and
        // the template-built effort timesheet grids as well. Constructor
        // injection of the views themselves would only pin throwaway copies:
        // docked views are transient, so instances resolved here would never
        // be the ones the dock materialises.
        public IList<Action> CommitActions { get; } = [];

        // This is to handle the commitment of all datagrids when changing
        // project scenarios. It helps prevent thread locking if a datagrid
        // is still in edit mode while a new scenario is selected.
        public void CommitEdit()
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                // Snapshot, so a commit that re-enters grid code cannot
                // invalidate the enumeration by adjusting the registrations.
                List<Action> commitActions = [.. CommitActions];

                foreach (Action commitAction in commitActions)
                {
                    commitAction();
                }
            });
        }
    }
}
