using System;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class EventModel
    {
        public int Id { get; set; }

        public int? EarliestFinishTime { get; set; }

        public int? LatestFinishTime { get; set; }
    }
}
