namespace Zametek.Contract.ProjectPlan
{
    // Holds DataGrid scroll positions in memory only (per session, keyed by grid name). They
    // survive view re-materialisation between tab changes but are cleared whenever a project or
    // project scenario is loaded or reset. Kept separate from the persisted column layout in
    // IDataGridLayoutManager.
    public interface IDataGridScrollManager
        : IDisposable
    {
        object? GetScrollItem(string name);

        void SetScrollItem(string name, object? item);

        void ClearScrollItems();
    }
}
