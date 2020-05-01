using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Object is a DTO")]
    public class ArrowGraphSettingsModel
    {
        public List<ActivitySeverityModel> ActivitySeverities { get; set; }

        public List<EdgeTypeFormatModel> EdgeTypeFormats { get; set; }
    }
}
