using System;
using System.Collections.Generic;

namespace Zametek.Common.Project.v0_2_0
{
    [Serializable]
    public class GraphCompilationErrorsDto
    {
        public bool AllResourcesExplicitTargetsButNotAllActivitiesTargeted { get; set; }
        public List<v0_1_0.CircularDependencyDto> CircularDependencies { get; set; }
        public List<int> MissingDependencies { get; set; }
    }
}
