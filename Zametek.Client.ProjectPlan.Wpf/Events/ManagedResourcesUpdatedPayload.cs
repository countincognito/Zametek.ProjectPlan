using System;
using System.Collections.Generic;
using System.Linq;
using Zametek.Common.Project;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class ManagedResourcesUpdatedPayload
    {
        #region Ctors

        public ManagedResourcesUpdatedPayload(IEnumerable<ResourceDto> resources)
        {
            if (resources == null)
            {
                throw new ArgumentNullException(nameof(resources));
            }
            Resources = resources.ToList();
        }

        #endregion

        #region Properties

        public IList<ResourceDto> Resources
        {
            get;
        }

        #endregion
    }
}
