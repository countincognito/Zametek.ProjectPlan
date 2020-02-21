using System.Collections.ObjectModel;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IArrowGraphSettingsManagerViewModel
    {
        ObservableCollection<ManagedActivitySeverityViewModel> ActivitySeverities { get; }
    }
}
