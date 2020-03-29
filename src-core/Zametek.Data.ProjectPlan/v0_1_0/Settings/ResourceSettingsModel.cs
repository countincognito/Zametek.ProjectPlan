using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public class ResourceSettingsModel
    {
        public List<ResourceModel> Resources { get; set; }
        public double DefaultUnitCost { get; set; }
        public bool AreDisabled { get; set; }
    }
}
