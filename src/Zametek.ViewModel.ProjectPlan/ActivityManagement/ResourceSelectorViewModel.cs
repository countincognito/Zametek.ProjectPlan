using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ResourceSelectorViewModel
        : BindableBase
    {
        #region Fields

        private readonly object m_Lock;

        #endregion

        #region Ctors

        public ResourceSelectorViewModel()
        {
            m_Lock = new object();
            TargetResources = new ObservableCollection<SelectableResourceViewModel>();
        }

        #endregion

        #region Properties

        public ObservableCollection<SelectableResourceViewModel> TargetResources
        {
            get;
        }

        public string TargetResourcesString
        {
            get
            {
                lock (m_Lock)
                {
                    return string.Join(
                        DependenciesStringValidationRule.Separator,
                        TargetResources.Where(x => x.IsSelected).Select(x => x.DisplayName));
                }
            }
        }

        public IList<int> SelectedResourceIds
        {
            get
            {
                lock (m_Lock)
                {
                    return TargetResources
                        .Where(x => x.IsSelected)
                        .Select(x => x.Id)
                        .ToList();
                }
            }
        }

        #endregion

        #region Public Methods

        public void SetTargetResources(IEnumerable<ResourceModel> targetResources, HashSet<int> selectedTargetResources)
        {
            if (targetResources == null)
            {
                throw new ArgumentNullException(nameof(targetResources));
            }
            if (selectedTargetResources == null)
            {
                throw new ArgumentNullException(nameof(selectedTargetResources));
            }

            lock (m_Lock)
            {
                TargetResources.Clear();
                foreach (ResourceModel targetResource in targetResources)
                {
                    TargetResources.Add(
                        new SelectableResourceViewModel(
                            targetResource.Id,
                            targetResource.Name,
                            selectedTargetResources.Contains(targetResource.Id)));
                }
            }
            RaisePropertyChanged(nameof(TargetResourcesString));
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return TargetResourcesString;
        }

        #endregion
    }
}
