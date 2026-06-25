using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IDataGridManager
        : IDisposable
    {
        IList<Action> ResetActions { get; }

        DataGridModel GetDataGridModel(string name);

        void SetDataGridModel(DataGridModel dataGridModel);

        void SaveDataGridModels();

        void ResetDataGridModels();

        // Scroll positions are held in memory only (per session, keyed by grid name)
        // and are intentionally separate from the persisted column layout above.
        // They survive view re-materialisation between tab changes but are cleared
        // whenever a project or project scenario is loaded or reset.

        object? GetScrollItem(string name);

        void SetScrollItem(string name, object? item);

        void ClearScrollItems();
    }
}
