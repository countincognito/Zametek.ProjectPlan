using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Object is a DTO")]
    public class GraphCompilationErrorsModel
    {
        public bool AllResourcesExplicitTargetsButNotAllActivitiesTargeted { get; set; }

        public List<CircularDependencyModel> CircularDependencies { get; set; }

        public List<int> MissingDependencies { get; set; }

        public List<int> InvalidConstraints { get; set; }
    }
}
