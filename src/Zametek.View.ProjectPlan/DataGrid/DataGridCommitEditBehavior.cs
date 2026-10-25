using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using Splat;
using System;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    // Registers a commit action for its DataGrid with the ICommitEditHandler
    // singleton, so a project scenario switch commits any open cell edit on
    // the grids actually on screen (docked or floated). Registration follows
    // the same OnAttached/OnDetaching pattern as DataGridPersistLayoutBehavior:
    // dock tabs re-materialise their views, so the singleton must never keep a
    // discarded grid alive through a stale action.
    public class DataGridCommitEditBehavior
        : Behavior<DataGrid>
    {
        private ICommitEditHandler? m_CommitEditHandler;

        // For markup attachment: the effort timesheet grids are built from a
        // data template (one per resource section), where constructor injection
        // is unavailable, so the handler is resolved from the service locator
        // on attach instead. Outside the running app (e.g. the previewer) the
        // resolution yields null and the behavior stays inert.
        public DataGridCommitEditBehavior()
        {
        }

        public DataGridCommitEditBehavior(ICommitEditHandler commitEditHandler)
        {
            m_CommitEditHandler = commitEditHandler ?? throw new ArgumentNullException(nameof(commitEditHandler));
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            m_CommitEditHandler ??= Locator.Current.GetService<ICommitEditHandler>();

            if (AssociatedObject is null
                || m_CommitEditHandler is null)
            {
                return;
            }

            m_CommitEditHandler.CommitActions.Add(CommitDataGridEdit);
        }

        protected override void OnDetaching()
        {
            m_CommitEditHandler?.CommitActions.Remove(CommitDataGridEdit);
            base.OnDetaching();
        }

        private void CommitDataGridEdit()
        {
            AssociatedObject?.CommitEdit();
        }
    }
}
