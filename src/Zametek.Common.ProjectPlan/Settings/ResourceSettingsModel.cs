using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class ResourceSettingsModel
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<ResourceModel> Resources { get; set; }

        public double DefaultUnitCost { get; set; }

        public bool AreDisabled { get; set; }
    }
}
