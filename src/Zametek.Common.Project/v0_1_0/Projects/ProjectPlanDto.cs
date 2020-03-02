using System;
using System.Collections.Generic;

namespace Zametek.Common.Project.v0_1_0
{
    [Serializable]
    public class ProjectPlanDto
        : IHaveVersion
    {
        public string Version { get; set; } = Versions.v0_1_0;

        public DateTime ProjectStart { get; set; }

        public List<DependentActivityDto> DependentActivities { get; set; }

        public ArrowGraphSettingsDto ArrowGraphSettings { get; set; }

        public ResourceSettingsDto ResourceSettings { get; set; }

        public GraphCompilationDto GraphCompilation { get; set; }

        public ArrowGraphDto ArrowGraph { get; set; }

        public bool HasStaleOutputs { get; set; }
    }
}
