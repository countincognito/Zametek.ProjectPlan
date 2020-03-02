using System;
using System.Collections.Generic;

namespace Zametek.Common.Project.v0_2_0
{
    [Serializable]
    public class ProjectPlanDto
        : IHaveVersion
    {
        public string Version { get; set; } = Versions.v0_2_0;

        public DateTime ProjectStart { get; set; }

        public List<v0_1_0.DependentActivityDto> DependentActivities { get; set; }

        public v0_1_0.ArrowGraphSettingsDto ArrowGraphSettings { get; set; }

        public v0_1_0.ResourceSettingsDto ResourceSettings { get; set; }

        public GraphCompilationDto GraphCompilation { get; set; }

        public v0_1_0.ArrowGraphDto ArrowGraph { get; set; }

        public bool HasStaleOutputs { get; set; }
    }
}
