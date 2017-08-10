using System;

namespace Zametek.Common.Project
{
    [Serializable]
    public class EventDto
    {
        public int Id { get; set; }
        public int? EarliestFinishTime { get; set; }
        public int? LatestFinishTime { get; set; }
    }
}
