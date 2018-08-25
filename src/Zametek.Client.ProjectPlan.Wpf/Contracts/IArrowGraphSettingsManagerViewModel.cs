using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IArrowGraphSettingsManagerViewModel
    {
        ObservableCollection<ManagedActivitySeverityViewModel> ActivitySeverities
        {
            get;
        }
    }
}
