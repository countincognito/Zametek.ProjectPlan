namespace Zametek.Contract.ProjectPlan
{
    // Commits any open DataGrid cell edits before a project scenario switch.
    // Each live grid registers a commit action while it is attached to the
    // visual tree (via DataGridCommitEditBehavior), mirroring the
    // IDataGridLayoutManager.ResetActions pattern, so the handler always
    // addresses the grids actually on screen - docked or floated.
    public interface ICommitEditHandler
    {
        IList<Action> CommitActions { get; }

        void CommitEdit();
    }
}
