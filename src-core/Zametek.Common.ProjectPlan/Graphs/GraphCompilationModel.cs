using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class GraphCompilationModel
    {
        public List<DependentActivityModel> DependentActivities { get; set; }
        public List<ResourceScheduleModel> ResourceSchedules { get; set; }
        public GraphCompilationErrorsModel Errors { get; set; }
        public int CyclomaticComplexity { get; set; }
        public int Duration { get; set; }
    }
}
