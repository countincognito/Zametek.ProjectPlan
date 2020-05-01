using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Object is a DTO")]
    public class DependentActivityModel
    {
        public ActivityModel Activity { get; set; }

        public List<int> Dependencies { get; set; }

        public List<int> ResourceDependencies { get; set; }
    }
}
