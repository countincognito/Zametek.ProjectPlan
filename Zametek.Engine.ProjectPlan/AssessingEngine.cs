using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Zametek.Common.Project;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Engine.ProjectPlan
{
    public class AssessingEngine
        : IAssessingEngine
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

        #region IAssessingEngine Members

        public MetricsDto CalculateProjectMetrics(
            IList<IActivity<int>> activities,
            IList<ActivitySeverityDto> activitySeverityDtos)
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

        public IList<ResourceSeriesDto> CalculateResourceSeriesSet(
            IList<IResourceSchedule<int>> resourceSchedules,
            IList<ResourceDto> resources,
            double defaultUnitCost)
        {
            if (resourceSchedules == null)
            {
                throw new ArgumentNullException(nameof(resourceSchedules));
            }
            if (resources == null)
            {
                throw new ArgumentNullException(nameof(resources));
            }

            var resourceSeriesSet = new List<ResourceSeriesDto>();

            if (resourceSchedules.Any())
            {
                IDictionary<int, ColorFormatDto> colorFormatLookup = resources.ToDictionary(x => x.Id, x => x.ColorFormat);
                var indirectResourceIdsToIgnore = new HashSet<int>();
                int finishTime = resourceSchedules.Max(x => x.FinishTime);
                int spareResourceCount = 1;
                var scheduledSeriesSet = new List<ResourceSeriesDto>();

                for (int resourceIndex = 0; resourceIndex < resourceSchedules.Count; resourceIndex++)
                {
                    IResourceSchedule<int> resourceSchedule = resourceSchedules[resourceIndex];
                    var series = new ResourceSeriesDto()
                    {
                        Values = resourceSchedule.ActivityAllocation.Select(x => x ? 1 : 0).ToList()
                    };
                    series.InterActivityAllocationType = InterActivityAllocationType.None;
                    var stringBuilder = new StringBuilder();
                    IResource<int> resource = resourceSchedule.Resource;

                    if (resource != null)
                    {
                        series.InterActivityAllocationType = resource.InterActivityAllocationType;
                        indirectResourceIdsToIgnore.Add(resource.Id);
                        if (string.IsNullOrWhiteSpace(resource.Name))
                        {
                            stringBuilder.Append($@"Resource {resource.Id}");
                        }
                        else
                        {
                            stringBuilder.Append($@"{resource.Name}");
                        }
                    }
                    else
                    {
                        stringBuilder.Append($@"Resource {spareResourceCount}");
                        spareResourceCount++;
                    }

                    series.Title = stringBuilder.ToString();
                    series.ColorFormatDto = resource != null && colorFormatLookup.ContainsKey(resource.Id) ? colorFormatLookup[resource.Id].Copy() : new ColorFormatDto().Randomize();
                    series.UnitCost = resource?.UnitCost ?? defaultUnitCost;
                    series.DisplayOrder = resource?.DisplayOrder ?? 0;
                    scheduledSeriesSet.Add(series);
                }

                // Now add the remaining resources that are indirect costs, but
                // sort them separately and add them to the front of the list.
                var unscheduledSeriesSet = new List<ResourceSeriesDto>();
                IEnumerable<ResourceDto> indirectResources =
                    resources.Where(x => !indirectResourceIdsToIgnore.Contains(x.Id) && x.InterActivityAllocationType == InterActivityAllocationType.Indirect);

                foreach (ResourceDto resourceDto in indirectResources)
                {
                    var series = new ResourceSeriesDto()
                    {
                        InterActivityAllocationType = resourceDto.InterActivityAllocationType,
                        Values = new List<int>(Enumerable.Repeat(1, finishTime))
                    };
                    var stringBuilder = new StringBuilder();
                    if (string.IsNullOrWhiteSpace(resourceDto.Name))
                    {
                        stringBuilder.Append($@"Resource {resourceDto.Id}");
                    }
                    else
                    {
                        stringBuilder.Append($@"{resourceDto.Name}");
                    }

                    series.Title = stringBuilder.ToString();
                    series.ColorFormatDto = resourceDto.ColorFormat != null ? resourceDto.ColorFormat.Copy() : new ColorFormatDto().Randomize();
                    series.UnitCost = resourceDto.UnitCost;
                    series.DisplayOrder = resourceDto.DisplayOrder;
                    unscheduledSeriesSet.Add(series);
                }

                resourceSeriesSet.AddRange(unscheduledSeriesSet.OrderBy(x => x.DisplayOrder));
                resourceSeriesSet.AddRange(scheduledSeriesSet.OrderBy(x => x.DisplayOrder));
            }

            return resourceSeriesSet;
        }

        public CostsDto CalculateProjectCosts(IList<ResourceSeriesDto> resourceSeriesSet)
        {
            if (resourceSeriesSet == null)
            {
                throw new ArgumentNullException(nameof(resourceSeriesSet));
            }

            var costs = new CostsDto();

            if (resourceSeriesSet.Any())
            {
                costs.DirectCost = resourceSeriesSet
                    .Where(x => x.InterActivityAllocationType == InterActivityAllocationType.Direct)
                    .Sum(x => x.Values.Sum(y => y * x.UnitCost));
                costs.IndirectCost = resourceSeriesSet
                    .Where(x => x.InterActivityAllocationType == InterActivityAllocationType.Indirect)
                    .Sum(x => x.Values.Sum(y => y * x.UnitCost));
                costs.OtherCost = resourceSeriesSet
                    .Where(x => x.InterActivityAllocationType == InterActivityAllocationType.None)
                    .Sum(x => x.Values.Sum(y => y * x.UnitCost));
                costs.TotalCost = resourceSeriesSet
                    .Sum(x => x.Values.Sum(y => y * x.UnitCost));
            }

            return costs;
        }

        #endregion
    }
}
