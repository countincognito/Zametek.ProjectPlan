using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_2_1
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Object is a DTO")]
    public class ResourceScheduleModel
    {
        public ResourceModel Resource { get; set; }

        public List<ScheduledActivityModel> ScheduledActivities { get; set; }

        public int FinishTime { get; set; }
    }
}
