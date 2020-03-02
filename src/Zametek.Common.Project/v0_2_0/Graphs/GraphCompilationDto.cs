using System;
using System.Collections.Generic;

namespace Zametek.Common.Project.v0_2_0
{
    [Serializable]
    public class GraphCompilationDto
    {
        public List<v0_1_0.DependentActivityDto> DependentActivities { get; set; }
        public List<v0_1_0.ResourceScheduleDto> ResourceSchedules { get; set; }
        public GraphCompilationErrorsDto Errors { get; set; }
        public int CyclomaticComplexity { get; set; }
        public int Duration { get; set; }
    }
}
