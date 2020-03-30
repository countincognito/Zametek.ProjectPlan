using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public class ResourceScheduleModel
    {
        public ResourceModel Resource { get; set; }
        public List<ScheduledActivityModel> ScheduledActivities { get; set; }
        public int FinishTime { get; set; }
    }
}
