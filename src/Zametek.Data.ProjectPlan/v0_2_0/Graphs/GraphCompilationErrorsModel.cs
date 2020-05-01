using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_2_0
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Object is a DTO")]
    public class GraphCompilationErrorsModel
    {
        public bool AllResourcesExplicitTargetsButNotAllActivitiesTargeted { get; set; }

        public List<v0_1_0.CircularDependencyModel> CircularDependencies { get; set; }

        public List<int> MissingDependencies { get; set; }
    }
}
