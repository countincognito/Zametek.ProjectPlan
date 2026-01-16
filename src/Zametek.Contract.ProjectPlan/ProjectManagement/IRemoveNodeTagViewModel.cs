using System.Collections.ObjectModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IRemoveNodeTagViewModel
    {
        ReadOnlyObservableCollection<ProjectPlanTagModel> Tags { get; }

        ProjectPlanTagModel SelectedTag { get; set; }
    }
}
