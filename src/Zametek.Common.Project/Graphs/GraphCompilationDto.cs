using System;
using System.Collections.Generic;

namespace Zametek.Common.Project
{
    [Serializable]
    public class GraphCompilationDto
    {
        public bool AllResourcesExplicitTargetsButNotAllActivitiesTargeted { get; set; }
        public List<CircularDependencyDto> CircularDependencies { get; set; }
        public List<int> MissingDependencies { get; set; }
        public List<DependentActivityDto> DependentActivities { get; set; }
        public List<ResourceScheduleDto> ResourceSchedules { get; set; }
        public int CyclomaticComplexity { get; set; }
        public int Duration { get; set; }
    }
}
