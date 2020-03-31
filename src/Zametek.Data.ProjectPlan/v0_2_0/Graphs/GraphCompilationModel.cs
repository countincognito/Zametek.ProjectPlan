using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_2_0
{
    [Serializable]
    public class GraphCompilationModel
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<v0_1_0.DependentActivityModel> DependentActivities { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<v0_1_0.ResourceScheduleModel> ResourceSchedules { get; set; }

        public GraphCompilationErrorsModel Errors { get; set; }

        public int CyclomaticComplexity { get; set; }

        public int Duration { get; set; }
    }
}
