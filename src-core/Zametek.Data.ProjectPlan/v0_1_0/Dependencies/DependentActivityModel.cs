using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public class DependentActivityModel
    {
        public ActivityModel Activity { get; set; }
        public List<int> Dependencies { get; set; }
        public List<int> ResourceDependencies { get; set; }
    }
}
