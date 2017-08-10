using Prism.Interactivity.InteractionRequest;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Zametek.Common.Project;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class ResourceSettingsManagerConfirmation
        : Confirmation
    {
        #region Ctors

        public ResourceSettingsManagerConfirmation(
            bool disableResources,
            IEnumerable<ResourceDto> resources)
        {
            if (resources == null)
            {
                throw new ArgumentNullException(nameof(resources));
            }
            DisableResources = disableResources;
            Resources = new ObservableCollection<ManagedResourceViewModel>();
            SetManagedResources(resources);
        }

        #endregion

        #region Properties

        public bool DisableResources
        {
            get;
            set;
        }

        public ObservableCollection<ManagedResourceViewModel> Resources
        {
            get;
        }

        public IEnumerable<ResourceDto> ResourceDtos
        {
            get
            {
                return Resources.Select(x => x.ResourceDto);
            }
        }

        #endregion

        #region Private Methods

        private void SetManagedResources(IEnumerable<ResourceDto> resources)
        {
            if (resources == null)
            {
                throw new ArgumentNullException(nameof(resources));
            }
            Resources.Clear();
            Resources.AddRange(resources.Select(x => new ManagedResourceViewModel(x)));
        }

        #endregion
    }
}
