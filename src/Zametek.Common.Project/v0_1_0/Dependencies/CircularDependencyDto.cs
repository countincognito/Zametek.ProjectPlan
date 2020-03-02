using System;
using System.Collections.Generic;

namespace Zametek.Common.Project.v0_1_0
{
    [Serializable]
    public class CircularDependencyDto
    {
        public List<int> Dependencies { get; set; }
    }
}
