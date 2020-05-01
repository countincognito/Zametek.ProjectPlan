using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_2_1
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Object is a DTO")]
    public class GraphCompilationModel
    {
        public List<DependentActivityModel> DependentActivities { get; set; }

        public List<ResourceScheduleModel> ResourceSchedules { get; set; }

        public GraphCompilationErrorsModel Errors { get; set; }

        public int CyclomaticComplexity { get; set; }

        public int Duration { get; set; }
    }
}
