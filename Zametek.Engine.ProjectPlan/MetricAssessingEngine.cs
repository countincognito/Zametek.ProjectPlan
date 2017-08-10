using System;
using System.Collections.Generic;
using System.Linq;
using Zametek.Common.Project;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Engine.ProjectPlan
{
    public class MetricAssessingEngine
        : IMetricAssessingEngine
    {
        #region Private Methods

        private static double CalculateCriticalityRisk(IList<IActivity<int>> activities, ActivitySeverityLookup activitySeverityLookup)
        {
            if (activities == null)
            {
                throw new ArgumentNullException(nameof(activities));
            }
            if (activitySeverityLookup == null)
            {
                throw new ArgumentNullException(nameof(activitySeverityLookup));
            }
            double numerator = activities.Sum(activity => activitySeverityLookup.FindSlackCriticalityWeight(activity.TotalSlack));
            double denominator = activitySeverityLookup.CriticalCriticalityWeight() * activities.Count;
            return (numerator / denominator);
        }

        private static double CalculateFibonacciRisk(IList<IActivity<int>> activities, ActivitySeverityLookup activitySeverityLookup)
        {
            if (activities == null)
            {
                throw new ArgumentNullException(nameof(activities));
            }
            if (activitySeverityLookup == null)
            {
                throw new ArgumentNullException(nameof(activitySeverityLookup));
            }
            double numerator = activities.Sum(activity => activitySeverityLookup.FindSlackFibonacciWeight(activity.TotalSlack));
            double denominator = activitySeverityLookup.CriticalFibonacciWeight() * activities.Count;
            return (numerator / denominator);
        }

        private static double CalculateActivityRisk(IList<IActivity<int>> activities)
        {
            if (activities == null)
            {
                throw new ArgumentNullException(nameof(activities));
            }
            double numerator = 0.0;
            double maxTotalSlack = 0.0;
            foreach (Activity<int> activity in activities.Where(x => x.TotalSlack.HasValue))
            {
                double totalSlack = Convert.ToDouble(activity.TotalSlack.GetValueOrDefault());
                if (totalSlack > maxTotalSlack)
                {
                    maxTotalSlack = totalSlack;
                }
                numerator += totalSlack;
            }
            double denominator = maxTotalSlack * activities.Count;
            return 1.0 - (numerator / denominator);
        }

        private static double CalculateActivityRiskWithStdDevCorrection(IList<IActivity<int>> activities)
        {
            if (activities == null)
            {
                throw new ArgumentNullException(nameof(activities));
            }
            double numerator = 0.0;
            double maxTotalSlack = 0.0;

            IList<double> totalSlacks = activities
                .Where(x => x.TotalSlack.HasValue)
                .Select(x => Convert.ToDouble(x.TotalSlack.GetValueOrDefault()))
                .ToList();

            double correctionValue = 0;
            if (totalSlacks.Count > 0)
            {
                double meanAverage = totalSlacks.Average();
                double sumOfSquaresOfDifferences = totalSlacks.Select(val => (val - meanAverage) * (val - meanAverage)).Sum();
                double stdDev = Math.Sqrt(sumOfSquaresOfDifferences / totalSlacks.Count);
                correctionValue = Math.Round(meanAverage + stdDev, MidpointRounding.AwayFromZero);
            }

            foreach (double totalSlack in totalSlacks)
            {
                double localTotalSlack = totalSlack;
                if (localTotalSlack > correctionValue)
                {
                    localTotalSlack = correctionValue;
                }
                if (localTotalSlack > maxTotalSlack)
                {
                    maxTotalSlack = localTotalSlack;
                }
                numerator += localTotalSlack;
            }
            double denominator = maxTotalSlack * activities.Count;
            return 1.0 - (numerator / denominator);
        }

        private static double CalculateGeometricCriticalityRisk(IList<IActivity<int>> activities, ActivitySeverityLookup activitySeverityLookup)
        {
            if (activities == null)
            {
                throw new ArgumentNullException(nameof(activities));
            }
            if (activitySeverityLookup == null)
            {
                throw new ArgumentNullException(nameof(activitySeverityLookup));
            }
            double numerator = 1.0;
            foreach (Activity<int> activity in activities)
            {
                numerator *= activitySeverityLookup.FindSlackCriticalityWeight(activity.TotalSlack);
            }
            numerator = Math.Pow(numerator, 1.0 / activities.Count);
            double denominator = activitySeverityLookup.CriticalCriticalityWeight();
            return (numerator / denominator);
        }

        private static double CalculateGeometricFibonacciRisk(IList<IActivity<int>> activities, ActivitySeverityLookup activitySeverityLookup)
        {
            if (activities == null)
            {
                throw new ArgumentNullException(nameof(activities));
            }
            if (activitySeverityLookup == null)
            {
                throw new ArgumentNullException(nameof(activitySeverityLookup));
            }
            double numerator = 1.0;
            foreach (Activity<int> activity in activities)
            {
                numerator *= activitySeverityLookup.FindSlackFibonacciWeight(activity.TotalSlack);
            }
            numerator = Math.Pow(numerator, 1.0 / activities.Count);
            double denominator = activitySeverityLookup.CriticalFibonacciWeight();
            return (numerator / denominator);
        }

        private static double CalculateGeometricActivityRisk(IList<IActivity<int>> activities)
        {
            if (activities == null)
            {
                throw new ArgumentNullException(nameof(activities));
            }
            double numerator = 1.0;
            double maxTotalSlack = 0.0;
            foreach (Activity<int> activity in activities.Where(x => x.TotalSlack.HasValue))
            {
                double totalSlack = Convert.ToDouble(activity.TotalSlack.GetValueOrDefault());
                if (totalSlack > maxTotalSlack)
                {
                    maxTotalSlack = totalSlack;
                }
                numerator *= (totalSlack + 1.0);
            }
            numerator = Math.Pow(numerator, 1.0 / activities.Count);
            numerator -= 1.0;
            double denominator = maxTotalSlack;
            return 1.0 - (numerator / denominator);
        }

        #endregion

        #region IMetricAssessingEngine Members

        public MetricsDto CalculateProjectMetrics(IList<IActivity<int>> activities, IList<ActivitySeverityDto> activitySeverityDtos)
        {
            if (activities == null)
            {
                throw new ArgumentNullException(nameof(activities));
            }
            if (activitySeverityDtos == null)
            {
                throw new ArgumentNullException(nameof(activitySeverityDtos));
            }
            var activitySeverityLookup = new ActivitySeverityLookup(activitySeverityDtos);
            return new MetricsDto
            {
                Criticality = CalculateCriticalityRisk(activities, activitySeverityLookup),
                Fibonacci = CalculateFibonacciRisk(activities, activitySeverityLookup),
                Activity = CalculateActivityRisk(activities),
                ActivityStdDevCorrection = CalculateActivityRiskWithStdDevCorrection(activities),
                GeometricCriticality = CalculateGeometricCriticalityRisk(activities, activitySeverityLookup),
                GeometricFibonacci = CalculateGeometricFibonacciRisk(activities, activitySeverityLookup),
                GeometricActivity = CalculateGeometricActivityRisk(activities),
            };
        }

        #endregion
    }
}
