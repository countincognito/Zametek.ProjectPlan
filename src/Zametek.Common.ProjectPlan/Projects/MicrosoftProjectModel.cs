using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class MicrosoftProjectModel
    {
        public DateTime ProjectStart { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<DependentActivityModel> DependentActivities { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<ResourceModel> Resources { get; set; }
    }
}
