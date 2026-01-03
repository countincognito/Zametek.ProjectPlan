using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class MetricsHelper
    {
        public static double? CalculateCriticalityRisk(
            IEnumerable<ActivityModel> activities,
            ActivitySeverityLookup activitySeverityLookup)
        {
            ArgumentNullException.ThrowIfNull(activities);
            ArgumentNullException.ThrowIfNull(activitySeverityLookup);
            double numerator = activities.Sum(activity => activitySeverityLookup.FindSlackCriticalityWeight(activity.TotalSlack));
            double denominator = activitySeverityLookup.CriticalCriticalityWeight() * activities.Count();

            if (denominator == 0)
            {
                if (numerator == 0)
                {
                    return 1.0;
                }
                return null;
            }

            return numerator / denominator;
        }

        public static double? CalculateFibonacciRisk(
            IEnumerable<ActivityModel> activities,
            ActivitySeverityLookup activitySeverityLookup)
        {
            ArgumentNullException.ThrowIfNull(activities);
            ArgumentNullException.ThrowIfNull(activitySeverityLookup);
            double numerator = activities.Sum(activity => activitySeverityLookup.FindSlackFibonacciWeight(activity.TotalSlack));
            double denominator = activitySeverityLookup.CriticalFibonacciWeight() * activities.Count();

            if (denominator == 0)
            {
                if (numerator == 0)
                {
                    return 1.0;
                }
                return null;
            }

            return numerator / denominator;
        }

        public static double? CalculateActivityRisk(IEnumerable<ActivityModel> activities)
        {
            ArgumentNullException.ThrowIfNull(activities);
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

            if (denominator == 0)
            {
                if (numerator == 0)
                {
                    return 1.0;
                }
                return null;
            }

            return 1.0 - (numerator / denominator);
        }

        public static double? CalculateActivityRiskWithStdDevCorrection(IEnumerable<ActivityModel> activities)
        {
            ArgumentNullException.ThrowIfNull(activities);
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

            if (denominator == 0)
            {
                if (numerator == 0)
                {
                    return 1.0;
                }
                return null;
            }

            return 1.0 - (numerator / denominator);
        }

        public static double? CalculateGeometricCriticalityRisk(
            IEnumerable<ActivityModel> activities,
            ActivitySeverityLookup activitySeverityLookup)
        {
            ArgumentNullException.ThrowIfNull(activities);
            ArgumentNullException.ThrowIfNull(activitySeverityLookup);
            double numerator = 1.0;
            foreach (ActivityModel activity in activities)
            {
                numerator *= activitySeverityLookup.FindSlackCriticalityWeight(activity.TotalSlack);
            }
            numerator = Math.Pow(numerator, 1.0 / activities.Count());
            double denominator = activitySeverityLookup.CriticalCriticalityWeight();

            if (denominator == 0)
            {
                if (numerator == 0)
                {
                    return 1.0;
                }
                return null;
            }

            return numerator / denominator;
        }

        public static double? CalculateGeometricFibonacciRisk(
            IEnumerable<ActivityModel> activities,
            ActivitySeverityLookup activitySeverityLookup)
        {
            ArgumentNullException.ThrowIfNull(activities);
            ArgumentNullException.ThrowIfNull(activitySeverityLookup);
            double numerator = 1.0;
            foreach (ActivityModel activity in activities)
            {
                numerator *= activitySeverityLookup.FindSlackFibonacciWeight(activity.TotalSlack);
            }
            numerator = Math.Pow(numerator, 1.0 / activities.Count());
            double denominator = activitySeverityLookup.CriticalFibonacciWeight();

            if (denominator == 0)
            {
                if (numerator == 0)
                {
                    return 1.0;
                }
                return null;
            }

            return numerator / denominator;
        }

        public static double? CalculateGeometricActivityRisk(IEnumerable<ActivityModel> activities)
        {
            ArgumentNullException.ThrowIfNull(activities);
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

            if (denominator == 0)
            {
                if (numerator == 0)
                {
                    return 1.0;
                }
                return null;
            }

            return 1.0 - (numerator / denominator);
        }

        public static RisksModel CalculateProjectRisks(
            IEnumerable<ActivityModel> activities,
            IEnumerable<ActivitySeverityModel> activitySeverities)
        {
            ArgumentNullException.ThrowIfNull(activities);
            ArgumentNullException.ThrowIfNull(activitySeverities);
            var activitySeverityLookup = new ActivitySeverityLookup(activitySeverities);

            List<ActivityModel> activitesWithRisk = [.. activities.Where(x => !x.HasNoRisk)];

            return new RisksModel
            {
                Criticality = CalculateCriticalityRisk(activitesWithRisk, activitySeverityLookup),
                Fibonacci = CalculateFibonacciRisk(activitesWithRisk, activitySeverityLookup),
                Activity = CalculateActivityRisk(activitesWithRisk),
                ActivityStdDevCorrection = CalculateActivityRiskWithStdDevCorrection(activitesWithRisk),
                GeometricCriticality = CalculateGeometricCriticalityRisk(activitesWithRisk, activitySeverityLookup),
                GeometricFibonacci = CalculateGeometricFibonacciRisk(activitesWithRisk, activitySeverityLookup),
                GeometricActivity = CalculateGeometricActivityRisk(activitesWithRisk),
            };
        }

        public static CostsModel CalculateProjectCosts(IList<ResourceSeriesModel> resourceSeriesModels)
        {
            ArgumentNullException.ThrowIfNull(resourceSeriesModels);

            double direct = resourceSeriesModels
                .Where(static x => x.InterActivityAllocationType == InterActivityAllocationType.Direct)
                .Sum(static x =>
                {
                    double accumulator(bool y) => y ? x.UnitCost : 0.0;
                    return x.ResourceSchedule.CostAllocation.Sum(accumulator) + x.FixedCost;
                });
            double indirect = resourceSeriesModels
                .Where(static x => x.InterActivityAllocationType == InterActivityAllocationType.Indirect)
                .Sum(static x =>
                {
                    double accumulator(bool y) => y ? x.UnitCost : 0.0;
                    return x.ResourceSchedule.CostAllocation.Sum(accumulator) + x.FixedCost;
                });
            double other = resourceSeriesModels
                .Where(static x => x.InterActivityAllocationType == InterActivityAllocationType.None)
                .Sum(static x =>
                {
                    double accumulator(bool y) => y ? x.UnitCost : 0.0;
                    return x.ResourceSchedule.CostAllocation.Sum(accumulator) + x.FixedCost;
                });
            double total = direct + indirect + other;

            return new CostsModel
            {
                Direct = direct,
                Indirect = indirect,
                Other = other,
                Total = total,
            };
        }

        public static BillingsModel CalculateProjectBillings(IList<ResourceSeriesModel> resourceSeriesModels)
        {
            ArgumentNullException.ThrowIfNull(resourceSeriesModels);

            double direct = resourceSeriesModels
                .Where(static x => x.InterActivityAllocationType == InterActivityAllocationType.Direct)
                .Sum(static x =>
                {
                    double accumulator(bool y) => y ? x.UnitBilling : 0.0;
                    return x.ResourceSchedule.BillingAllocation.Sum(accumulator) + x.FixedBilling;
                });
            double indirect = resourceSeriesModels
                .Where(static x => x.InterActivityAllocationType == InterActivityAllocationType.Indirect)
                .Sum(static x =>
                {
                    double accumulator(bool y) => y ? x.UnitBilling : 0.0;
                    return x.ResourceSchedule.BillingAllocation.Sum(accumulator) + x.FixedBilling;
                });
            double other = resourceSeriesModels
                .Where(static x => x.InterActivityAllocationType == InterActivityAllocationType.None)
                .Sum(static x =>
                {
                    double accumulator(bool y) => y ? x.UnitBilling : 0.0;
                    return x.ResourceSchedule.BillingAllocation.Sum(accumulator) + x.FixedBilling;
                });
            double total = direct + indirect + other;

            return new BillingsModel
            {
                Direct = direct,
                Indirect = indirect,
                Other = other,
                Total = total,
            };
        }

        public static double? CalculateEfficiency(double? activityEffort, double? totalEffort)
        {
            if (activityEffort is not null
                && totalEffort is not null
                && activityEffort != 0 && totalEffort != 0)
            {
                return activityEffort / totalEffort;
            }
            return null;
        }

        public static EffortsModel CalculateProjectEfforts(IList<ResourceSeriesModel> resourceSeriesModels)
        {
            ArgumentNullException.ThrowIfNull(resourceSeriesModels);
            static double allocationAccumulator(bool x) => x ? 1.0 : 0.0;
            static int durationAccumulator(ScheduledActivityModel x) => x.HasNoEffort ? 0 : x.Duration;

            double direct = resourceSeriesModels
                .Where(static x => x.InterActivityAllocationType == InterActivityAllocationType.Direct)
                .Sum(static x => x.ResourceSchedule.EffortAllocation.Sum(allocationAccumulator));
            double indirect = resourceSeriesModels
                .Where(static x => x.InterActivityAllocationType == InterActivityAllocationType.Indirect)
                .Sum(static x => x.ResourceSchedule.EffortAllocation.Sum(allocationAccumulator));
            double other = resourceSeriesModels
                .Where(static x => x.InterActivityAllocationType == InterActivityAllocationType.None)
                .Sum(static x => x.ResourceSchedule.EffortAllocation.Sum(allocationAccumulator));
            double total = direct + indirect + other;
            double activity = resourceSeriesModels
                .Sum(static x => x.ResourceSchedule.ScheduledActivities.Sum(durationAccumulator));
            double? efficiency = CalculateEfficiency(activity, total);

            return new EffortsModel
            {
                Direct = direct,
                Indirect = indirect,
                Other = other,
                Total = total,
                Activity = activity,
                Efficiency = efficiency,
            };
        }

        public static double? CalculateMargin(double? cost, double? billing)
        {
            double? abs = CalculateMarginAbsolute(cost, billing);

            if (abs is not null
                && billing is not null
                && billing != 0)
            {
                return abs / billing;
            }
            return null;
        }

        public static double? CalculateMarginAbsolute(double? cost, double? billing)
        {
            if (cost is not null
                && billing is not null)
            {
                return billing - cost;
            }
            return null;
        }

        public static MarginsModel CalculateProjectMargins(CostsModel costsModel, BillingsModel billingsModel)
        {
            ArgumentNullException.ThrowIfNull(costsModel);
            ArgumentNullException.ThrowIfNull(billingsModel);

            double? direct = CalculateMargin(costsModel.Direct, billingsModel.Direct);
            double? indirect = CalculateMargin(costsModel.Indirect, billingsModel.Indirect);
            double? other = CalculateMargin(costsModel.Other, billingsModel.Other);
            double? total = CalculateMargin(costsModel.Total, billingsModel.Total);

            double? directAbs = CalculateMarginAbsolute(costsModel.Direct, billingsModel.Direct);
            double? indirectAbs = CalculateMarginAbsolute(costsModel.Indirect, billingsModel.Indirect);
            double? otherAbs = CalculateMarginAbsolute(costsModel.Other, billingsModel.Other);
            double? totalAbs = CalculateMarginAbsolute(costsModel.Total, billingsModel.Total);

            return new MarginsModel
            {
                Direct = direct,
                Indirect = indirect,
                Other = other,
                Total = total,
                DirectAbsolute = directAbs,
                IndirectAbsolute = indirectAbs,
                OtherAbsolute = otherAbs,
                TotalAbsolute = totalAbs,
            };
        }
    }
}
