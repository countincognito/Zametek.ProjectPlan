using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ProjectService
        : IProjectService
    {
        #region Fields

        private static readonly Random s_Rnd = new Random();

        #endregion

        #region Private Methods

        private static double CalculateCriticalityRisk(
            IEnumerable<ActivityModel> activities,
            ActivitySeverityLookup activitySeverityLookup)
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
            double denominator = activitySeverityLookup.CriticalCriticalityWeight() * activities.Count();
            return (numerator / denominator);
        }

        private static double CalculateFibonacciRisk(
            IEnumerable<ActivityModel> activities,
            ActivitySeverityLookup activitySeverityLookup)
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
            double denominator = activitySeverityLookup.CriticalFibonacciWeight() * activities.Count();
            return (numerator / denominator);
        }

        private static double CalculateActivityRisk(IEnumerable<ActivityModel> activities)
        {
            if (activities == null)
            {
                throw new ArgumentNullException(nameof(activities));
            }
            double numerator = 0.0;
            double maxTotalSlack = 0.0;
            foreach (ActivityModel activity in activities.Where(x => x.TotalSlack.HasValue))
            {
                double totalSlack = Convert.ToDouble(activity.TotalSlack.GetValueOrDefault());
                if (totalSlack > maxTotalSlack)
                {
                    maxTotalSlack = totalSlack;
                }
                numerator += totalSlack;
            }
            double denominator = maxTotalSlack * activities.Count();
            return 1.0 - (numerator / denominator);
        }

        private static double CalculateActivityRiskWithStdDevCorrection(IEnumerable<ActivityModel> activities)
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
            double denominator = maxTotalSlack * activities.Count();
            return 1.0 - (numerator / denominator);
        }

        private static double CalculateGeometricCriticalityRisk(
            IEnumerable<ActivityModel> activities,
            ActivitySeverityLookup activitySeverityLookup)
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
            foreach (ActivityModel activity in activities)
            {
                numerator *= activitySeverityLookup.FindSlackCriticalityWeight(activity.TotalSlack);
            }
            numerator = Math.Pow(numerator, 1.0 / activities.Count());
            double denominator = activitySeverityLookup.CriticalCriticalityWeight();
            return (numerator / denominator);
        }

        private static double CalculateGeometricFibonacciRisk(
            IEnumerable<ActivityModel> activities,
            ActivitySeverityLookup activitySeverityLookup)
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
            foreach (ActivityModel activity in activities)
            {
                numerator *= activitySeverityLookup.FindSlackFibonacciWeight(activity.TotalSlack);
            }
            numerator = Math.Pow(numerator, 1.0 / activities.Count());
            double denominator = activitySeverityLookup.CriticalFibonacciWeight();
            return (numerator / denominator);
        }

        private static double CalculateGeometricActivityRisk(IEnumerable<ActivityModel> activities)
        {
            if (activities == null)
            {
                throw new ArgumentNullException(nameof(activities));
            }
            double numerator = 1.0;
            double maxTotalSlack = 0.0;
            foreach (ActivityModel activity in activities.Where(x => x.TotalSlack.HasValue))
            {
                double totalSlack = Convert.ToDouble(activity.TotalSlack.GetValueOrDefault());
                if (totalSlack > maxTotalSlack)
                {
                    maxTotalSlack = totalSlack;
                }
                numerator *= (totalSlack + 1.0);
            }
            numerator = Math.Pow(numerator, 1.0 / activities.Count());
            numerator -= 1.0;
            double denominator = maxTotalSlack;
            return 1.0 - (numerator / denominator);
        }

        private static ColorFormatModel Randomize(ColorFormatModel colorFormat)
        {
            if (colorFormat == null)
            {
                throw new ArgumentNullException(nameof(colorFormat));
            }
            var b = new byte[4];
            s_Rnd.NextBytes(b);
            colorFormat.A = b[0];
            colorFormat.R = b[1];
            colorFormat.G = b[2];
            colorFormat.B = b[3];
            return colorFormat;
        }

        #endregion

        #region IProjectService Members

        public CostsModel CalculateProjectCosts(ResourceSeriesSetModel resourceSeriesSet)
        {
            if (resourceSeriesSet == null)
            {
                throw new ArgumentNullException(nameof(resourceSeriesSet));
            }

            var costs = new CostsModel();

            if (resourceSeriesSet.Combined.Any())
            {
                costs.DirectCost = resourceSeriesSet.Combined
                    .Where(x => x.InterActivityAllocationType == InterActivityAllocationType.Direct)
                    .Sum(x => x.Values.Sum(y => y * x.UnitCost));
                costs.IndirectCost = resourceSeriesSet.Combined
                    .Where(x => x.InterActivityAllocationType == InterActivityAllocationType.Indirect)
                    .Sum(x => x.Values.Sum(y => y * x.UnitCost));
                costs.OtherCost = resourceSeriesSet.Combined
                    .Where(x => x.InterActivityAllocationType == InterActivityAllocationType.None)
                    .Sum(x => x.Values.Sum(y => y * x.UnitCost));
            }

            costs.TotalCost = costs.DirectCost + costs.IndirectCost + costs.OtherCost;
            return costs;
        }

        public MetricsModel CalculateProjectMetrics(
            IEnumerable<ActivityModel> activities,
            IEnumerable<ActivitySeverityModel> activitySeverities)
        {
            if (activities == null)
            {
                throw new ArgumentNullException(nameof(activities));
            }
            if (activitySeverities == null)
            {
                throw new ArgumentNullException(nameof(activitySeverities));
            }
            var activitySeverityLookup = new ActivitySeverityLookup(activitySeverities);
            return new MetricsModel
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

        public ResourceSeriesSetModel CalculateResourceSeriesSet(
            IEnumerable<ResourceScheduleModel> resourceSchedules,
            IEnumerable<ResourceModel> resources,
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

            var resourceSeriesSet = new ResourceSeriesSetModel
            {
                Scheduled = new List<ResourceSeriesModel>(),
                Unscheduled = new List<ResourceSeriesModel>(),
                Combined = new List<ResourceSeriesModel>(),
            };
            var resourceLookup = resources.ToDictionary(x => x.Id);

            if (resourceSchedules.Any())
            {
                IDictionary<int, ColorFormatModel> colorFormatLookup = resources.ToDictionary(x => x.Id, x => x.ColorFormat);
                int finishTime = resourceSchedules.Max(x => x.FinishTime);
                int spareResourceCount = 1;

                // Scheduled resource series.
                // These are the series that apply to scheduled activities (whether allocated to named or unnamed resources).
                var scheduledSeriesSet = new List<ResourceSeriesModel>();
                var scheduledResourceSeriesLookup = new Dictionary<int, ResourceSeriesModel>();

                foreach (ResourceScheduleModel resourceSchedule in resourceSchedules)
                {
                    var series = new ResourceSeriesModel
                    {
                        ResourceSchedule = resourceSchedule,
                        Values = resourceSchedule.ActivityAllocation.Select(x => x ? 1 : 0).ToList(),
                        InterActivityAllocationType = InterActivityAllocationType.None,
                    };

                    var stringBuilder = new StringBuilder();

                    if (resourceSchedule.Resource != null
                        && resourceLookup.TryGetValue(resourceSchedule.Resource.Id, out ResourceModel resource))
                    {
                        int resourceId = resource.Id;
                        series.ResourceId = resourceId;
                        series.InterActivityAllocationType = resource.InterActivityAllocationType;
                        if (string.IsNullOrWhiteSpace(resource.Name))
                        {
                            stringBuilder.Append($@"{Resource.ProjectPlan.Resources.Label_Resource} {resourceId}");
                        }
                        else
                        {
                            stringBuilder.Append($@"{resource.Name}");
                        }
                        series.ColorFormat = colorFormatLookup.ContainsKey(resourceId) ? colorFormatLookup[resourceId].CloneObject() : Randomize(new ColorFormatModel());
                        series.UnitCost = resource.UnitCost;
                        series.DisplayOrder = resource.DisplayOrder;
                        scheduledResourceSeriesLookup.Add(resourceId, series);
                    }
                    else
                    {
                        stringBuilder.Append($@"{Resource.ProjectPlan.Resources.Label_Resource} {spareResourceCount}");
                        spareResourceCount++;
                        series.ColorFormat = Randomize(new ColorFormatModel());
                        series.UnitCost = defaultUnitCost;
                        series.DisplayOrder = 0;
                    }

                    series.Title = stringBuilder.ToString();
                    scheduledSeriesSet.Add(series);
                }

                // Unscheduled resource series.
                // These are the series that apply to named resources that need to be included, even if they are not
                // scheduled to specific activities.
                var unscheduledSeriesSet = new List<ResourceSeriesModel>();
                var unscheduledResourceSeriesLookup = new Dictionary<int, ResourceSeriesModel>();

                IEnumerable<ResourceModel> unscheduledResources = resources
                    .Where(x => x.InterActivityAllocationType == InterActivityAllocationType.Indirect);

                foreach (ResourceModel resource in unscheduledResources)
                {
                    int resourceId = resource.Id;
                    var series = new ResourceSeriesModel
                    {
                        ResourceId = resourceId,
                        InterActivityAllocationType = resource.InterActivityAllocationType,
                        Values = new List<int>(Enumerable.Repeat(1, finishTime)),
                        ColorFormat = resource.ColorFormat != null ? resource.ColorFormat.CloneObject() : Randomize(new ColorFormatModel()),
                        UnitCost = resource.UnitCost,
                        DisplayOrder = resource.DisplayOrder,
                    };

                    var stringBuilder = new StringBuilder();
                    if (string.IsNullOrWhiteSpace(resource.Name))
                    {
                        stringBuilder.Append($@"{Resource.ProjectPlan.Resources.Label_Resource} {resourceId}");
                    }
                    else
                    {
                        stringBuilder.Append($@"{resource.Name}");
                    }
                    series.Title = stringBuilder.ToString();

                    unscheduledSeriesSet.Add(series);
                    unscheduledResourceSeriesLookup.Add(resourceId, series);
                }

                // Combined resource series.
                // The intersection of the scheduled and unscheduled series.
                var combinedScheduled = new List<ResourceSeriesModel>();
                var unscheduledSeriesAlreadyIncluded = new HashSet<int>();

                foreach (ResourceSeriesModel scheduledSeries in scheduledSeriesSet)
                {
                    var values = new List<int>(Enumerable.Repeat(0, finishTime));
                    if (scheduledSeries.ResourceId.HasValue)
                    {
                        int resourceId = scheduledSeries.ResourceId.GetValueOrDefault();
                        if (unscheduledResourceSeriesLookup.TryGetValue(resourceId, out ResourceSeriesModel unscheduledResourceSeries))
                        {
                            values = scheduledSeries.Values.Zip(unscheduledResourceSeries.Values, (x, y) => Math.Max(x, y)).ToList();
                            unscheduledSeriesAlreadyIncluded.Add(resourceId);
                        }
                        else
                        {
                            values = scheduledSeries.Values.ToList();
                        }
                    }
                    else
                    {
                        values = scheduledSeries.Values.ToList();
                    }

                    scheduledSeries.Values = values;

                    combinedScheduled.Add(scheduledSeries);
                }

                // Finally, add the unscheduled series that have not already been included above.

                // Prepend so that they might be displayed first after sorting.
                List<ResourceSeriesModel> combined = unscheduledSeriesSet
                    .Where(x => !unscheduledSeriesAlreadyIncluded.Contains(x.ResourceId.GetValueOrDefault()))
                    .ToList();

                combined.AddRange(combinedScheduled);

                resourceSeriesSet.Scheduled.AddRange(scheduledSeriesSet);
                resourceSeriesSet.Unscheduled.AddRange(unscheduledSeriesSet);
                resourceSeriesSet.Combined.AddRange(combined.OrderBy(x => x.DisplayOrder));
            }

            return resourceSeriesSet;
        }

        public byte[] ExportArrowGraphToDiagram(DiagramArrowGraphModel diagramArrowGraph)
        {
            if (diagramArrowGraph == null)
            {
                throw new ArgumentNullException(nameof(diagramArrowGraph));
            }
            graphml graphML = GraphMLBuilder.ToGraphML(diagramArrowGraph);
            byte[] output = null;
            using (var ms = new MemoryStream())
            {
                var xmlSerializer = new XmlSerializer(typeof(graphml));
                xmlSerializer.Serialize(ms, graphML);
                output = ms.ToArray();
            }
            return output;
        }

        #endregion
    }
}
