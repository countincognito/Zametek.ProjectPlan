using System;
using System.Collections.Generic;

namespace Zametek.Common.Project
{
    [Serializable]
    public class ArrowGraphSettingsDto
    {
        public List<ActivitySeverityDto> ActivitySeverities { get; set; }
        public List<EdgeTypeFormatDto> EdgeTypeFormats { get; set; }
    }
}
