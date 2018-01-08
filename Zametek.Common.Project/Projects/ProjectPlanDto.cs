using System;
using System.Collections.Generic;

namespace Zametek.Common.Project
{
    [Serializable]
    public class ProjectPlanDto
    {
        //public string Title { get; set; }
        public DateTime ProjectStart { get; set; }

        public List<DependentActivityDto> DependentActivities { get; set; }

        public ArrowGraphSettingsDto ArrowGraphSettings { get; set; }

        public ResourceSettingsDto ResourceSettings { get; set; }

        public GraphCompilationDto GraphCompilation { get; set; }

        public ArrowGraphDto ArrowGraph { get; set; }

        public bool HasStaleOutputs { get; set; }
    }
}
