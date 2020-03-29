using System;
using System.Collections.Generic;

namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public class CircularDependencyModel
    {
        public List<int> Dependencies { get; set; }
    }
}
