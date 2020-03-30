using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ProjectService
        : IProjectService
    {
        private static Random _Rnd;

        static ProjectService()
        {
            _Rnd = new Random();
        }

        #region Private Methods

        private static double CalculateCriticalityRisk(
            IEnumerable<IActivity<int, int>> activities,
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
            IEnumerable<IActivity<int, int>> activities,
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

        private static double CalculateActivityRisk(IEnumerable<IActivity<int, int>> activities)
        {
            if (activities == null)
            {
                throw new ArgumentNullException(nameof(activities));
            }
            double numerator = 0.0;
            double maxTotalSlack = 0.0;
            foreach (IActivity<int, int> activity in activities.Where(x => x.TotalSlack.HasValue))
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

        private static double CalculateActivityRiskWithStdDevCorrection(IEnumerable<IActivity<int, int>> activities)
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
            IEnumerable<IActivity<int, int>> activities,
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
            foreach (IActivity<int, int> activity in activities)
            {
                numerator *= activitySeverityLookup.FindSlackCriticalityWeight(activity.TotalSlack);
            }
            numerator = Math.Pow(numerator, 1.0 / activities.Count());
            double denominator = activitySeverityLookup.CriticalCriticalityWeight();
            return (numerator / denominator);
        }

        private static double CalculateGeometricFibonacciRisk(
            IEnumerable<IActivity<int, int>> activities,
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
            foreach (IActivity<int, int> activity in activities)
            {
                numerator *= activitySeverityLookup.FindSlackFibonacciWeight(activity.TotalSlack);
            }
            numerator = Math.Pow(numerator, 1.0 / activities.Count());
            double denominator = activitySeverityLookup.CriticalFibonacciWeight();
            return (numerator / denominator);
        }

        private static double CalculateGeometricActivityRisk(IEnumerable<IActivity<int, int>> activities)
        {
            if (activities == null)
            {
                throw new ArgumentNullException(nameof(activities));
            }
            double numerator = 1.0;
            double maxTotalSlack = 0.0;
            foreach (IActivity<int, int> activity in activities.Where(x => x.TotalSlack.HasValue))
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
            _Rnd.NextBytes(b);
            colorFormat.A = b[0];
            colorFormat.R = b[1];
            colorFormat.G = b[2];
            colorFormat.B = b[3];
            return colorFormat;
        }

        #endregion

        #region IProjectService Members

        public CostsModel CalculateProjectCosts(IEnumerable<ResourceSeriesModel> resourceSeriesSet)
        {
            if (resourceSeriesSet == null)
            {
                throw new ArgumentNullException(nameof(resourceSeriesSet));
            }

            var costs = new CostsModel();

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

        public MetricsModel CalculateProjectMetrics(
            IEnumerable<IActivity<int, int>> activities,
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

        public IEnumerable<ResourceSeriesModel> CalculateResourceSeriesSet(
            IEnumerable<IResourceSchedule<int, int>> resourceSchedules,
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

            var resourceSeriesSet = new List<ResourceSeriesModel>();
            var resourceLookup = resources.ToDictionary(x => x.Id);

            if (resourceSchedules.Any())
            {
                IDictionary<int, ColorFormatModel> colorFormatLookup = resources.ToDictionary(x => x.Id, x => x.ColorFormat);
                var indirectResourceIdsToIgnore = new HashSet<int>();
                int finishTime = resourceSchedules.Max(x => x.FinishTime);
                int spareResourceCount = 1;
                var scheduledSeriesSet = new List<ResourceSeriesModel>();

                foreach (IResourceSchedule<int, int> resourceSchedule in resourceSchedules)
                {
                    var series = new ResourceSeriesModel()
                    {
                        Values = resourceSchedule.ActivityAllocation.Select(x => x ? 1 : 0).ToList()
                    };
                    series.InterActivityAllocationType = InterActivityAllocationType.None;
                    var stringBuilder = new StringBuilder();


                    if (resourceSchedule.Resource != null
                        && resourceLookup.TryGetValue(resourceSchedule.Resource.Id, out ResourceModel resource))
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
                        series.ColorFormat = colorFormatLookup.ContainsKey(resource.Id) ? colorFormatLookup[resource.Id].CloneObject() : Randomize(new ColorFormatModel());
                        series.UnitCost = resource.UnitCost;
                        series.DisplayOrder = resource.DisplayOrder;
                    }
                    else
                    {
                        stringBuilder.Append($@"Resource {spareResourceCount}");
                        spareResourceCount++;
                        series.ColorFormat = Randomize(new ColorFormatModel());
                        series.UnitCost = defaultUnitCost;
                        series.DisplayOrder = 0;
                    }

                    series.Title = stringBuilder.ToString();
                    scheduledSeriesSet.Add(series);
                }

                // Now add the remaining resources that are indirect costs, but
                // sort them separately and add them to the front of the list.
                var unscheduledSeriesSet = new List<ResourceSeriesModel>();
                IEnumerable<ResourceModel> indirectResources = resources
                    .Where(x => !indirectResourceIdsToIgnore.Contains(x.Id) && x.InterActivityAllocationType == InterActivityAllocationType.Indirect);

                foreach (ResourceModel resource in indirectResources)
                {
                    var series = new ResourceSeriesModel()
                    {
                        InterActivityAllocationType = resource.InterActivityAllocationType,
                        Values = new List<int>(Enumerable.Repeat(1, finishTime))
                    };
                    var stringBuilder = new StringBuilder();
                    if (string.IsNullOrWhiteSpace(resource.Name))
                    {
                        stringBuilder.Append($@"Resource {resource.Id}");
                    }
                    else
                    {
                        stringBuilder.Append($@"{resource.Name}");
                    }

                    series.Title = stringBuilder.ToString();
                    series.ColorFormat = resource.ColorFormat != null ? resource.ColorFormat.CloneObject() : Randomize(new ColorFormatModel());
                    series.UnitCost = resource.UnitCost;
                    series.DisplayOrder = resource.DisplayOrder;
                    unscheduledSeriesSet.Add(series);
                }

                resourceSeriesSet.AddRange(unscheduledSeriesSet.OrderBy(x => x.DisplayOrder));
                resourceSeriesSet.AddRange(scheduledSeriesSet.OrderBy(x => x.DisplayOrder));
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
                var xmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(graphml));
                xmlSerializer.Serialize(ms, graphML);
                output = ms.ToArray();
            }
            return output;
        }

        #endregion
    }
}
