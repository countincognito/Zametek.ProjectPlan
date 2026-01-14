using System.Collections.ObjectModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IRemovePlanTagViewModel
    {
        ReadOnlyObservableCollection<ProjectPlanTagModel> Tags { get; }

        ProjectPlanTagModel SelectedTag { get; set; }
    }
}
