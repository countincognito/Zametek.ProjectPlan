using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Object is a DTO")]
    public class ResourceSeriesSetModel
    {
        public List<ResourceSeriesModel> Scheduled { get; set; }

        public List<ResourceSeriesModel> Unscheduled { get; set; }

        public List<ResourceSeriesModel> Combined { get; set; }
    }
}
