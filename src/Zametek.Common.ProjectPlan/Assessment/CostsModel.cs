using System;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class CostsModel
    {
        public double DirectCost { get; set; }

        public double IndirectCost { get; set; }

        public double OtherCost { get; set; }

        public double TotalCost { get; set; }
    }
}
