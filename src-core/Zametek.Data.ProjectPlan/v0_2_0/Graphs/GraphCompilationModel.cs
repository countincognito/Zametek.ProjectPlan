using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_2_0
{
    [Serializable]
    public class GraphCompilationModel
    {
        public List<v0_1_0.DependentActivityModel> DependentActivities { get; set; }
        public List<v0_1_0.ResourceScheduleModel> ResourceSchedules { get; set; }
        public GraphCompilationErrorsModel Errors { get; set; }
        public int CyclomaticComplexity { get; set; }
        public int Duration { get; set; }
    }
}
