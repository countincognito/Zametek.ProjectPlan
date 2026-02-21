using System.Collections.ObjectModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IRemoveNodeTagViewModel
    {
        //IReadOnlyList<ProjectScenarioTagModel> RawTags { get; }

        ReadOnlyObservableCollection<ProjectScenarioTagModel> Tags { get; }

        ProjectScenarioTagModel SelectedTag { get; set; }
    }
}
