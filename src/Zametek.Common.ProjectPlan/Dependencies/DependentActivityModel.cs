using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class DependentActivityModel
    {
        public ActivityModel Activity { get; set; }
        public List<int> Dependencies { get; set; }
        public List<int> ResourceDependencies { get; set; }
    }
}
