using System;
using System.Collections.Generic;

namespace Zametek.Common.Project.v0_1_0
{
    [Serializable]
    public class ArrowGraphSettingsDto
    {
        public List<ActivitySeverityDto> ActivitySeverities { get; set; }
        public List<EdgeTypeFormatDto> EdgeTypeFormats { get; set; }
    }
}
