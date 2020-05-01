using System;
using System.Collections.Generic;
using Zametek.Maths.Graphs;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Object is a DTO")]
    public class ActivityModel
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public List<int> TargetResources { get; set; }

        public LogicalOperator TargetResourceOperator { get; set; }

        public List<int> AllocatedToResources { get; set; }

        public bool CanBeRemoved { get; set; }

        public int Duration { get; set; }

        public int? FreeSlack { get; set; }

        public int? EarliestStartTime { get; set; }

        public int? LatestFinishTime { get; set; }

        public int? MinimumFreeSlack { get; set; }

        public int? MinimumEarliestStartTime { get; set; }

        public DateTime? MinimumEarliestStartDateTime { get; set; }
    }
}
