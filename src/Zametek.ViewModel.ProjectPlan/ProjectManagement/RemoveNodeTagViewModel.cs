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

        public RemoveNodeTagViewModel(IEnumerable<ProjectPlanTagModel> projectPlanTagModels)
        {
            m_Tags = [];
            m_ReadOnlyTags = new(m_Tags);
            m_Tags.AddRange(projectPlanTagModels);
            m_SelectedTag = m_ReadOnlyTags.FirstOrDefault() ?? new();
        }

        #endregion

        #region Private Members

        #endregion

        #region IRemovePlanTagViewModel Members

        private readonly ObservableUniqueCollection<ProjectPlanTagModel> m_Tags;
        private readonly ReadOnlyObservableCollection<ProjectPlanTagModel> m_ReadOnlyTags;
        public ReadOnlyObservableCollection<ProjectPlanTagModel> Tags => m_ReadOnlyTags;

        private ProjectPlanTagModel m_SelectedTag;
        public ProjectPlanTagModel SelectedTag
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
