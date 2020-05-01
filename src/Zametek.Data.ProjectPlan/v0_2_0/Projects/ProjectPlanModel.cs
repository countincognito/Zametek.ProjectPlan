using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_2_0
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Object is a DTO")]
    public class ProjectPlanModel
    {
        public string Version { get; } = Versions.v0_2_0;

        public DateTime ProjectStart { get; set; }

        public List<v0_1_0.DependentActivityModel> DependentActivities { get; set; }

        public v0_1_0.ArrowGraphSettingsModel ArrowGraphSettings { get; set; }

        public v0_1_0.ResourceSettingsModel ResourceSettings { get; set; }

        public GraphCompilationModel GraphCompilation { get; set; }

        public v0_1_0.ArrowGraphModel ArrowGraph { get; set; }

        public bool HasStaleOutputs { get; set; }
    }
}
