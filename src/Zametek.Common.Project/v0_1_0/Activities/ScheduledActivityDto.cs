using System;

namespace Zametek.Common.Project.v0_1_0
{
    [Serializable]
    public class ScheduledActivityDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Duration { get; set; }
        public int StartTime { get; set; }
        public int FinishTime { get; set; }
    }
}
