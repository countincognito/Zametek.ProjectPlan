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

        public ResourceSettingsManagerConfirmation(ResourceSettingsDto resourceSettings)
        {
            if (resourceSettings == null)
            {
                throw new ArgumentNullException(nameof(resourceSettings));
            }
            DefaultUnitCost = resourceSettings.DefaultUnitCost;
            AreDisabled = resourceSettings.AreDisabled;
            Resources = new ObservableCollection<ManagedResourceViewModel>();
            SetManagedResources(resourceSettings.Resources);
        }

        #endregion

        #region Properties

        public ObservableCollection<ManagedResourceViewModel> Resources
        {
            get;
        }

        public double DefaultUnitCost
        {
            get;
            set;
        }

        public bool AreDisabled
        {
            get;
            set;
        }

        public IEnumerable<ResourceDto> ResourceDtos
        {
            get
            {
                return Resources.Select(x => x.ResourceDto);
            }
        }

        public ResourceSettingsDto ResourceSettingsDto
        {
            get
            {
                return new ResourceSettingsDto
                {
                    Resources = ResourceDtos.ToList(),
                    DefaultUnitCost = DefaultUnitCost,
                    AreDisabled = AreDisabled
                };
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
