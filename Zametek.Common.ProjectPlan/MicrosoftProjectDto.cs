using System;
using System.Collections.Generic;
using Zametek.Common.Project;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class MicrosoftProjectDto
    {
        public DateTime ProjectStart { get; set; }
        public List<DependentActivityDto> DependentActivities { get; set; }
        public List<ResourceDto> Resources { get; set; }
    }
}
