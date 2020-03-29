using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public class GraphCompilationModel
    {
        public bool AllResourcesExplicitTargetsButNotAllActivitiesTargeted { get; set; }
        public List<CircularDependencyModel> CircularDependencies { get; set; }
        public List<int> MissingDependencies { get; set; }
        public List<DependentActivityModel> DependentActivities { get; set; }
        public List<ResourceScheduleModel> ResourceSchedules { get; set; }
        public int CyclomaticComplexity { get; set; }
        public int Duration { get; set; }
    }
}
