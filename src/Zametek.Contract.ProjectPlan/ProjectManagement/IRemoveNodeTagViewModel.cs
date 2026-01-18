using System.Collections.ObjectModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IRemoveNodeTagViewModel
    {
        //IReadOnlyList<ProjectPlanTagModel> RawTags { get; }

        ReadOnlyObservableCollection<ProjectPlanTagModel> Tags { get; }

        ProjectPlanTagModel SelectedTag { get; set; }
    }
}
