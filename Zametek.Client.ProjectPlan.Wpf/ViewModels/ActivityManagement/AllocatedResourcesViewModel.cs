using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using Zametek.Common.Project;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class AllocatedResourcesViewModel
        : BindableBase
    {
        #region Fields

        private readonly object m_Lock;
        private IList<SelectableResourceViewModel> m_AllocatedResources;

        #endregion

        #region Ctors

        public AllocatedResourcesViewModel()
        {
            m_Lock = new object();
            m_AllocatedResources = new List<SelectableResourceViewModel>();
        }

        #endregion

        #region Properties

        public string AllocatedResourcesString
        {
            get
            {
                lock (m_Lock)
                {
                    return string.Join(
                        DependenciesStringValidationRule.Separator.ToString(),
                        m_AllocatedResources.Where(x => x.IsSelected).Select(x => x.DisplayName));
                }
            }
        }

        #endregion

        #region Public Methods

        public void SetAllocatedResources(IEnumerable<ResourceDto> targetResources, HashSet<int> selectedTargetResources)
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
                m_AllocatedResources.Clear();
                foreach (ResourceDto targetResource in targetResources)
                {
                    m_AllocatedResources.Add(
                        new SelectableResourceViewModel(
                            targetResource.Id,
                            targetResource.Name,
                            selectedTargetResources.Contains(targetResource.Id)));
                }
            }
            RaisePropertyChanged(nameof(AllocatedResourcesString));
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return AllocatedResourcesString;
        }

        #endregion
    }
}
