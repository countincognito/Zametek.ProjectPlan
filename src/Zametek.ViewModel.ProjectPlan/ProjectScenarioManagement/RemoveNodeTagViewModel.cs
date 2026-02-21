using DynamicData;
using ReactiveUI;
using System.Collections.ObjectModel;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class RemoveNodeTagViewModel
        : ViewModelBase, IRemoveNodeTagViewModel
    {
        #region Ctors

        public RemoveNodeTagViewModel(IEnumerable<ProjectScenarioTagModel> projectScenarioTagModels)
        {
            m_Tags = [];
            m_ReadOnlyTags = new(m_Tags);
            m_Tags.AddRange(projectScenarioTagModels);
            m_SelectedTag = m_ReadOnlyTags.FirstOrDefault() ?? new();
        }

        #endregion

        #region Private Members

        #endregion

        #region IRemoveNodeTagViewModel Members

        private readonly ObservableUniqueCollection<ProjectScenarioTagModel> m_Tags;
        private readonly ReadOnlyObservableCollection<ProjectScenarioTagModel> m_ReadOnlyTags;
        public ReadOnlyObservableCollection<ProjectScenarioTagModel> Tags => m_ReadOnlyTags;

        private ProjectScenarioTagModel m_SelectedTag;
        public ProjectScenarioTagModel SelectedTag
        {
            get => m_SelectedTag;
            set
            {
                m_SelectedTag = value;
                this.RaisePropertyChanged();
            }
        }

        #endregion
    }
}
