using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_2_1
{
    [Serializable]
    public class ResourceScheduleModel
    {
        public ResourceModel Resource { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<ScheduledActivityModel> ScheduledActivities { get; set; }

        public int FinishTime { get; set; }
    }
}
