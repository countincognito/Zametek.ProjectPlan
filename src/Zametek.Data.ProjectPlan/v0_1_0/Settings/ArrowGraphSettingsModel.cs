using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public class ArrowGraphSettingsModel
    {
        public List<ActivitySeverityModel> ActivitySeverities { get; set; }
        public List<EdgeTypeFormatModel> EdgeTypeFormats { get; set; }
    }
}
