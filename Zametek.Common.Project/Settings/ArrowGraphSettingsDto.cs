using System;
using System.Collections.Generic;

namespace Zametek.Common.Project
{
    [Serializable]
    public class ArrowGraphSettingsDto
    {
        public IList<ActivitySeverityDto> ActivitySeverities { get; set; }
        public IList<EdgeTypeFormatDto> EdgeTypeFormats { get; set; }
    }
}
