using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public class ProjectPlanModel
    {
        public string Version { get; } = Versions.v0_1_0;

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
