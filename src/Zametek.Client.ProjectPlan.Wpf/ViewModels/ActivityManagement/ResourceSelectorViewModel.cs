using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Zametek.Client.ProjectPlan.Wpf
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
                        DependenciesStringValidationRule.Separator.ToString(),
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

        public void SetTargetResources(IEnumerable<Common.Project.v0_1_0.ResourceDto> targetResources, HashSet<int> selectedTargetResources)
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
                foreach (Common.Project.v0_1_0.ResourceDto targetResource in targetResources)
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

        //public void AddTargetResource(ResourceDto targetResource, bool isSelected)
        //{
        //    lock (m_Lock)
        //    {
        //        if (!m_TargetResources.Contains(targetResource))
        //        {
        //            m_TargetResources.Add(targetResource);
        //            TargetResources.Add(
        //                new SelectableResourceViewModel(
        //                    targetResource.Id,
        //                    targetResource.Name,
        //                    isSelected));
        //        }
        //    }
        //    RaisePropertyChanged(nameof(TargetResourcesString));
        //}

        //public void RemoveTargetResource(ResourceDto targetResource)
        //{
        //    lock (m_Lock)
        //    {
        //        if (m_TargetResources.Contains(targetResource))
        //        {
        //            m_TargetResources.Remove(targetResource);
        //            SelectableResourceViewModel vm = TargetResources.FirstOrDefault(x => x.Id == targetResource.Id);
        //            TargetResources.Remove(vm);
        //        }
        //    }
        //    RaisePropertyChanged(nameof(TargetResourcesString));
        //}

        //public void UpdateTargetResource(ResourceDto targetResource)
        //{
        //    lock (m_Lock)
        //    {
        //        if (m_TargetResources.Contains(targetResource))
        //        {
        //            SelectableResourceViewModel vm = TargetResources.FirstOrDefault(x => x.Id == targetResource.Id);
        //            if (vm != null)
        //            {
        //                vm.Name = targetResource.Name;
        //            }
        //        }
        //    }
        //    RaisePropertyChanged(nameof(TargetResourcesString));
        //}

        #endregion

        #region Overrides

        public override string ToString()
        {
            return TargetResourcesString;
        }

        #endregion
    }
}
