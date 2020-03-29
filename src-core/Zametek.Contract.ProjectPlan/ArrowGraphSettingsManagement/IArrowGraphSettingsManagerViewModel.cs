using System.Collections.ObjectModel;

namespace Zametek.Contract.ProjectPlan
{
    public interface IArrowGraphSettingsManagerViewModel
    {
        ObservableCollection<IManagedActivitySeverityViewModel> ActivitySeverities { get; }
    }
}
