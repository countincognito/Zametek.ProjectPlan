using System;
using System.Collections.Generic;

namespace Zametek.Common.Project
{
    [Serializable]
    public class CircularDependencyDto
    {
        public List<int> Dependencies { get; set; }
    }
}
