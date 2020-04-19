using System;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public class ScheduledActivityModel
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public int Duration { get; set; }

        public int StartTime { get; set; }

        public int FinishTime { get; set; }
    }
}
