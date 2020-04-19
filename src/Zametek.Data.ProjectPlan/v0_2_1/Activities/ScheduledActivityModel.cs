using System;

namespace Zametek.Data.ProjectPlan.v0_2_1
{
    [Serializable]
    public class ScheduledActivityModel
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public bool HasNoCost { get; set; }

        public int Duration { get; set; }

        public int StartTime { get; set; }

        public int FinishTime { get; set; }
    }
}
