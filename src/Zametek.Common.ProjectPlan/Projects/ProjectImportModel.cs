using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Object is a DTO")]
    public class ProjectImportModel
    {
        public DateTime ProjectStart { get; set; }

        public List<DependentActivityModel> DependentActivities { get; set; }

        public List<ResourceModel> Resources { get; set; }
    }
}
