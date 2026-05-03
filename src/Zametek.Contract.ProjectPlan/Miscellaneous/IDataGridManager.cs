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
    }
}
