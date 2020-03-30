using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class ResourceSettingsModel
    {
        public List<ResourceModel> Resources { get; set; }
        public double DefaultUnitCost { get; set; }
        public bool AreDisabled { get; set; }
    }
}
