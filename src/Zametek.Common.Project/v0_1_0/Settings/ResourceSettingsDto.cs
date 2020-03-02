using System;
using System.Collections.Generic;

namespace Zametek.Common.Project.v0_1_0
{
    [Serializable]
    public class ResourceSettingsDto
    {
        public List<ResourceDto> Resources { get; set; }
        public double DefaultUnitCost { get; set; }
        public bool AreDisabled { get; set; }
    }
}
