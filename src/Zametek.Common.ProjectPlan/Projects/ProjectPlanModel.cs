using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class ProjectPlanModel
    {
        public string Version { get; set; }

        public DateTime ProjectStart { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<DependentActivityModel> DependentActivities { get; set; }

        public ArrowGraphSettingsModel ArrowGraphSettings { get; set; }

        public ResourceSettingsModel ResourceSettings { get; set; }

        public GraphCompilationModel GraphCompilation { get; set; }

        public ArrowGraphModel ArrowGraph { get; set; }

        public bool HasStaleOutputs { get; set; }
    }
}
