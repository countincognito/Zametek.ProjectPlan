using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IDataGridManager
        : IDisposable
    {
        List<Action> ResetActions { get; }

        DataGridModel GetDataGridModel(string name);

        void SetDataGridModel(DataGridModel dataGridModel);

        void SaveDataGridModels();

        void ResetDataGridModels();
    }
}
