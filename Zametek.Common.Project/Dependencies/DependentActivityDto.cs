using System;
using System.Collections.Generic;

namespace Zametek.Common.Project
{
    [Serializable]
    public class DependentActivityDto
    {
        public ActivityDto Activity { get; set; }
        public List<int> Dependencies { get; set; }
        public List<int> ResourceDependencies { get; set; }
    }
}
