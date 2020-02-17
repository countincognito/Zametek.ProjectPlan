using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using Zametek.Common.Project;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class AllocatedToResourcesViewModel
        : BindableBase
    {
        #region Fields

        private readonly object m_Lock;
        private readonly IList<SelectableResourceViewModel> m_AllocatedToResources;

        #endregion

        #region Ctors

        public AllocatedToResourcesViewModel()
        {
            m_Lock = new object();
            m_AllocatedToResources = new List<SelectableResourceViewModel>();
        }

        #endregion

        #region Properties

        public string AllocatedToResourcesString
        {
            get
            {
                lock (m_Lock)
                {
                    return string.Join(
                        DependenciesStringValidationRule.Separator.ToString(),
                        m_AllocatedToResources.Where(x => x.IsSelected).Select(x => x.DisplayName));
                }
            }
        }

        #endregion

        #region Public Methods

        public void SetAllocatedToResources(IEnumerable<ResourceDto> targetResources, HashSet<int> selectedTargetResources)
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
                m_AllocatedToResources.Clear();
                foreach (ResourceDto targetResource in targetResources)
                {
                    m_AllocatedToResources.Add(
                        new SelectableResourceViewModel(
                            targetResource.Id,
                            targetResource.Name,
                            selectedTargetResources.Contains(targetResource.Id)));
                }
            }
            RaisePropertyChanged(nameof(AllocatedToResourcesString));
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return AllocatedToResourcesString;
        }

        #endregion
    }
}
