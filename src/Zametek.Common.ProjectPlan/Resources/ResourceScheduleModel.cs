using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Object is a DTO")]
    public class ResourceScheduleModel
    {
        public ResourceModel Resource { get; set; }

        public List<ScheduledActivityModel> ScheduledActivities { get; set; }

        public List<bool> ActivityAllocation { get; set; }

        public int FinishTime { get; set; }
    }
}
