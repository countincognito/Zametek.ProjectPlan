using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class ResourceScheduleModel
    {
        public ResourceModel Resource { get; set; }
        public List<ScheduledActivityModel> ScheduledActivities { get; set; }
        public int FinishTime { get; set; }
    }
}
