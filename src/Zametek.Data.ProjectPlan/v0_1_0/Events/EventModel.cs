using System;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public class EventModel
    {
        public int Id { get; set; }

        public int? EarliestFinishTime { get; set; }

        public int? LatestFinishTime { get; set; }
    }
}
