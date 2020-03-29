using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_2_0
{
    [Serializable]
    public class GraphCompilationErrorsModel
    {
        public bool AllResourcesExplicitTargetsButNotAllActivitiesTargeted { get; set; }
        public List<v0_1_0.CircularDependencyModel> CircularDependencies { get; set; }
        public List<int> MissingDependencies { get; set; }
    }
}
