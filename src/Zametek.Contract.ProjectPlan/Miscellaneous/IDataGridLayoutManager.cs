using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    // Persists DataGrid column layout (display order and width) to the settings file, keyed by
    // grid name. Kept separate from IDataGridScrollManager: the column layout is flushed to
    // settings and survives across sessions, whereas scroll positions are in-memory and per-session.
    public interface IDataGridLayoutManager
        : IDisposable
    {
        IList<Action> ResetActions { get; }

        DataGridModel GetDataGridModel(string name);

        void SetDataGridModel(DataGridModel dataGridModel);

        void SaveDataGridModels();

        void ResetDataGridModels();
    }
}
