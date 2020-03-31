using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class DependentActivityModel
    {
        public ActivityModel Activity { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<int> Dependencies { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<int> ResourceDependencies { get; set; }
    }
}
