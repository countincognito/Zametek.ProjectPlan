using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public class GraphCompilationModel
    {
        public bool AllResourcesExplicitTargetsButNotAllActivitiesTargeted { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<CircularDependencyModel> CircularDependencies { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<int> MissingDependencies { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<DependentActivityModel> DependentActivities { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<ResourceScheduleModel> ResourceSchedules { get; set; }

        public int CyclomaticComplexity { get; set; }

        public int Duration { get; set; }
    }
}
