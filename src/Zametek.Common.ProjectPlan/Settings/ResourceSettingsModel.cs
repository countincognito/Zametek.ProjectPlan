using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Object is a DTO")]
    public class ResourceSettingsModel
    {
        public List<ResourceModel> Resources { get; set; }

        public double DefaultUnitCost { get; set; }

        public bool AreDisabled { get; set; }
    }
}
