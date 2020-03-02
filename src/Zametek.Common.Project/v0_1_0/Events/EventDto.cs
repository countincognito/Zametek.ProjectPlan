using System;

namespace Zametek.Common.Project.v0_1_0
{
    [Serializable]
    public class EventDto
    {
        public int Id { get; set; }
        public int? EarliestFinishTime { get; set; }
        public int? LatestFinishTime { get; set; }
    }
}
