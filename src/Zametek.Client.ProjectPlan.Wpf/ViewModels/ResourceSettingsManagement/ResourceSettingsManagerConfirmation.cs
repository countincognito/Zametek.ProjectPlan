using Prism.Interactivity.InteractionRequest;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class ResourceSettingsManagerConfirmation
        : Confirmation
    {
        #region Ctors

        public ResourceSettingsManagerConfirmation(Common.Project.v0_1_0.ResourceSettingsDto resourceSettings)
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

        public IEnumerable<Common.Project.v0_1_0.ResourceDto> ResourceDtos
        {
            get
            {
                return Resources.Select(x => x.ResourceDto);
            }
        }

        public Common.Project.v0_1_0.ResourceSettingsDto ResourceSettingsDto
        {
            get
            {
                return new Common.Project.v0_1_0.ResourceSettingsDto
                {
                    Resources = ResourceDtos.ToList(),
                    DefaultUnitCost = DefaultUnitCost,
                    AreDisabled = AreDisabled
                };
            }
        }

        #endregion

        #region Private Methods

        private void SetManagedResources(IEnumerable<Common.Project.v0_1_0.ResourceDto> resources)
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
