using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class ArrowGraphSettingsModel
    {
        public List<ActivitySeverityModel> ActivitySeverities { get; set; }
        public List<EdgeTypeFormatModel> EdgeTypeFormats { get; set; }
    }
}
