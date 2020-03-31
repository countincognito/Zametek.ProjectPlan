using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public class ArrowGraphSettingsModel
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<ActivitySeverityModel> ActivitySeverities { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO property")]
        public List<EdgeTypeFormatModel> EdgeTypeFormats { get; set; }
    }
}
