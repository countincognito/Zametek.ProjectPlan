using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class MicrosoftProjectDto
    {
        public DateTime ProjectStart { get; set; }
        public List<Common.Project.v0_1_0.DependentActivityDto> DependentActivities { get; set; }
        public List<Common.Project.v0_1_0.ResourceDto> Resources { get; set; }
    }
}
