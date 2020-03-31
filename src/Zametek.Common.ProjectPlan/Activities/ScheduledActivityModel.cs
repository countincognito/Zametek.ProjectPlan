using System;

namespace Zametek.Common.ProjectPlan
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
