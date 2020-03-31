using System;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class MetricsModel
    {
        public double Criticality { get; set; }

        public double Fibonacci { get; set; }

        public double Activity { get; set; }

        public double ActivityStdDevCorrection { get; set; }

        public double GeometricCriticality { get; set; }

        public double GeometricFibonacci { get; set; }

        public double GeometricActivity { get; set; }
    }
}
