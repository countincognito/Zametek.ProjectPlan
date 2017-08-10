using System;
using System.Collections.Generic;

namespace Zametek.Common.Project
{
    [Serializable]
    public class ResourceScheduleDto
    {
        public ResourceDto Resource { get; set; }
        public List<ScheduledActivityDto> ScheduledActivities { get; set; }
        public int FinishTime { get; set; }
    }
}
