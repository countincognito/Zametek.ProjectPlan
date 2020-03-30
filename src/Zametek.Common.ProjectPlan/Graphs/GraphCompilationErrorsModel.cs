using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class GraphCompilationErrorsModel
    {
        public bool AllResourcesExplicitTargetsButNotAllActivitiesTargeted { get; set; }
        public List<CircularDependencyModel> CircularDependencies { get; set; }
        public List<int> MissingDependencies { get; set; }
    }
}
