using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class CircularDependencyModel
    {
        public List<int> Dependencies { get; set; }
    }
}
