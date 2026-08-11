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

        // The financial metrics are calculated per resource series first: each series
        // carries exactly one InterActivityAllocationType (which buckets its
        // inter-activity portion and its fixed values) and one ActivityAllocationType
        // (which buckets its activity portion), so a single series contributes to a
        // fixed set of buckets. The project-level metrics are then the member-wise
        // sums of the per-resource results.

        public static ResourceCostsModel CalculateResourceCosts(ResourceSeriesModel resourceSeriesModel)
        {
            ArgumentNullException.ThrowIfNull(resourceSeriesModel);

            double direct = 0.0;
            double indirect = 0.0;
            double other = 0.0;

            ResourceScheduleModel resourceSchedule = resourceSeriesModel.ResourceSchedule;

            // Inter-activity costs only.
            // Where cost allocation is true, but activity allocation is false.

            double interActivityCost = resourceSchedule.CostAllocation
                .Zip(resourceSchedule.ActivityAllocation, (a, b) => a && !b)
                .Sum(x => x ? resourceSeriesModel.UnitCost : 0.0);

            switch (resourceSeriesModel.InterActivityAllocationType)
            {
                case InterActivityAllocationType.Direct:
                    direct += interActivityCost;
                    break;
                case InterActivityAllocationType.Indirect:
                    indirect += interActivityCost;
                    break;
                case InterActivityAllocationType.None:
                    other += interActivityCost;
                    break;
                default:
                    break;
            }

            // Activity costs only.
            // Where cost allocation is true, and activity allocation is true.

            double activityCost = resourceSchedule.CostAllocation
                .Zip(resourceSchedule.ActivityAllocation, (a, b) => a && b)
                .Sum(x => x ? resourceSeriesModel.UnitCost : 0.0);

            switch (resourceSeriesModel.ActivityAllocationType)
            {
                case ActivityAllocationType.Direct:
                    direct += activityCost;
                    break;
                case ActivityAllocationType.Indirect:
                    indirect += activityCost;
                    break;
                case ActivityAllocationType.Other:
                    other += activityCost;
                    break;
                default:
                    break;
            }

            // Fixed costs only.
            // Use InterActivityAllocationType to determine direct, indirect, or other,
            // since fixed costs are not allocated to activities.

            switch (resourceSeriesModel.InterActivityAllocationType)
            {
                case InterActivityAllocationType.Direct:
                    direct += resourceSeriesModel.FixedCost;
                    break;
                case InterActivityAllocationType.Indirect:
                    indirect += resourceSeriesModel.FixedCost;
                    break;
                case InterActivityAllocationType.None:
                    other += resourceSeriesModel.FixedCost;
                    break;
                default:
                    break;
            }

            double total = direct + indirect + other;

            return new ResourceCostsModel
            {
                Direct = direct,
                Indirect = indirect,
                Other = other,
                Total = total,
            };
        }

        public static CostsModel SumResourceCosts(IEnumerable<ResourceCostsModel> resourceCostsModels)
        {
            ArgumentNullException.ThrowIfNull(resourceCostsModels);

            double totalDirect = 0.0;
            double totalIndirect = 0.0;
            double totalOther = 0.0;

            foreach (ResourceCostsModel resourceCostsModel in resourceCostsModels)
            {
                totalDirect += resourceCostsModel.Direct.GetValueOrDefault();
                totalIndirect += resourceCostsModel.Indirect.GetValueOrDefault();
                totalOther += resourceCostsModel.Other.GetValueOrDefault();
            }

            double total = totalDirect + totalIndirect + totalOther;

            return new CostsModel
            {
                Direct = totalDirect,
                Indirect = totalIndirect,
                Other = totalOther,
                Total = total,
            };
        }

        public static CostsModel CalculateProjectCosts(IList<ResourceSeriesModel> resourceSeriesModels)
        {
            ArgumentNullException.ThrowIfNull(resourceSeriesModels);
            return SumResourceCosts(resourceSeriesModels.Select(CalculateResourceCosts));
        }

        public static ResourceBillingsModel CalculateResourceBillings(ResourceSeriesModel resourceSeriesModel)
        {
            ArgumentNullException.ThrowIfNull(resourceSeriesModel);

            double direct = 0.0;
            double indirect = 0.0;
            double other = 0.0;

            ResourceScheduleModel resourceSchedule = resourceSeriesModel.ResourceSchedule;

            // Inter-activity billings only.
            // Where billing allocation is true, but activity allocation is false.

            double interActivityBilling = resourceSchedule.BillingAllocation
                .Zip(resourceSchedule.ActivityAllocation, (a, b) => a && !b)
                .Sum(x => x ? resourceSeriesModel.UnitBilling : 0.0);

            switch (resourceSeriesModel.InterActivityAllocationType)
            {
                case InterActivityAllocationType.Direct:
                    direct += interActivityBilling;
                    break;
                case InterActivityAllocationType.Indirect:
                    indirect += interActivityBilling;
                    break;
                case InterActivityAllocationType.None:
                    other += interActivityBilling;
                    break;
                default:
                    break;
            }

            // Activity billings only.
            // Where billing allocation is true, and activity allocation is true.

            double activityBilling = resourceSchedule.BillingAllocation
                .Zip(resourceSchedule.ActivityAllocation, (a, b) => a && b)
                .Sum(x => x ? resourceSeriesModel.UnitBilling : 0.0);

            switch (resourceSeriesModel.ActivityAllocationType)
            {
                case ActivityAllocationType.Direct:
                    direct += activityBilling;
                    break;
                case ActivityAllocationType.Indirect:
                    indirect += activityBilling;
                    break;
                case ActivityAllocationType.Other:
                    other += activityBilling;
                    break;
                default:
                    break;
            }

            // Fixed billings only.
            // Use InterActivityAllocationType to determine direct, indirect, or other,
            // since fixed billings are not allocated to activities.

            switch (resourceSeriesModel.InterActivityAllocationType)
            {
                case InterActivityAllocationType.Direct:
                    direct += resourceSeriesModel.FixedBilling;
                    break;
                case InterActivityAllocationType.Indirect:
                    indirect += resourceSeriesModel.FixedBilling;
                    break;
                case InterActivityAllocationType.None:
                    other += resourceSeriesModel.FixedBilling;
                    break;
                default:
                    break;
            }

            double total = direct + indirect + other;

            return new ResourceBillingsModel
            {
                Direct = direct,
                Indirect = indirect,
                Other = other,
                Total = total,
            };
        }

        public static BillingsModel SumResourceBillings(IEnumerable<ResourceBillingsModel> resourceBillingsModels)
        {
            ArgumentNullException.ThrowIfNull(resourceBillingsModels);

            double totalDirect = 0.0;
            double totalIndirect = 0.0;
            double totalOther = 0.0;

            foreach (ResourceBillingsModel resourceBillingsModel in resourceBillingsModels)
            {
                totalDirect += resourceBillingsModel.Direct.GetValueOrDefault();
                totalIndirect += resourceBillingsModel.Indirect.GetValueOrDefault();
                totalOther += resourceBillingsModel.Other.GetValueOrDefault();
            }

            double total = totalDirect + totalIndirect + totalOther;

            return new BillingsModel
            {
                Direct = totalDirect,
                Indirect = totalIndirect,
                Other = totalOther,
                Total = total,
            };
        }

        public static BillingsModel CalculateProjectBillings(IList<ResourceSeriesModel> resourceSeriesModels)
        {
            ArgumentNullException.ThrowIfNull(resourceSeriesModels);
            return SumResourceBillings(resourceSeriesModels.Select(CalculateResourceBillings));
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

        public static ResourceEffortsModel CalculateResourceEfforts(ResourceSeriesModel resourceSeriesModel)
        {
            ArgumentNullException.ThrowIfNull(resourceSeriesModel);
            static double allocationAccumulator(bool x) => x ? 1.0 : 0.0;

            double direct = 0.0;
            double indirect = 0.0;
            double other = 0.0;

            ResourceScheduleModel resourceSchedule = resourceSeriesModel.ResourceSchedule;

            // Inter-activity effort only.
            // Where effort allocation is true, but activity allocation is false.

            double interActivityEffort = resourceSchedule.EffortAllocation
                .Zip(resourceSchedule.ActivityAllocation, (a, b) => a && !b)
                .Sum(allocationAccumulator);

            switch (resourceSeriesModel.InterActivityAllocationType)
            {
                case InterActivityAllocationType.Direct:
                    direct += interActivityEffort;
                    break;
                case InterActivityAllocationType.Indirect:
                    indirect += interActivityEffort;
                    break;
                case InterActivityAllocationType.None:
                    other += interActivityEffort;
                    break;
                default:
                    break;
            }

            // Activity effort only.
            // Where effort allocation is true, and activity allocation is true.

            double activityAllocatedEffort = resourceSchedule.EffortAllocation
                .Zip(resourceSchedule.ActivityAllocation, (a, b) => a && b)
                .Sum(allocationAccumulator);

            switch (resourceSeriesModel.ActivityAllocationType)
            {
                case ActivityAllocationType.Direct:
                    direct += activityAllocatedEffort;
                    break;
                case ActivityAllocationType.Indirect:
                    indirect += activityAllocatedEffort;
                    break;
                case ActivityAllocationType.Other:
                    other += activityAllocatedEffort;
                    break;
                default:
                    break;
            }

            double total = direct + indirect + other;

            static int durationAccumulator(ScheduledActivityModel x) => x.HasNoEffort ? 0 : x.Duration;

            double activity = resourceSchedule.ScheduledActivities.Sum(durationAccumulator);

            double? efficiency = CalculateEfficiency(activity, total);

            return new ResourceEffortsModel
            {
                Direct = direct,
                Indirect = indirect,
                Other = other,
                Total = total,
                Activity = activity,
                Efficiency = efficiency,
            };
        }

        public static EffortsModel SumResourceEfforts(IEnumerable<ResourceEffortsModel> resourceEffortsModels)
        {
            ArgumentNullException.ThrowIfNull(resourceEffortsModels);

            double totalDirect = 0.0;
            double totalIndirect = 0.0;
            double totalOther = 0.0;
            double totalActivity = 0.0;

            foreach (ResourceEffortsModel resourceEffortsModel in resourceEffortsModels)
            {
                totalDirect += resourceEffortsModel.Direct.GetValueOrDefault();
                totalIndirect += resourceEffortsModel.Indirect.GetValueOrDefault();
                totalOther += resourceEffortsModel.Other.GetValueOrDefault();
                totalActivity += resourceEffortsModel.Activity.GetValueOrDefault();
            }

            double total = totalDirect + totalIndirect + totalOther;

            // The project efficiency is recalculated from the summed components,
            // not aggregated from the per-resource efficiencies (a ratio of sums,
            // rather than a sum of ratios).
            double? efficiency = CalculateEfficiency(totalActivity, total);

            return new EffortsModel
            {
                Direct = totalDirect,
                Indirect = totalIndirect,
                Other = totalOther,
                Total = total,
                Activity = totalActivity,
                Efficiency = efficiency,
            };
        }

        public static EffortsModel CalculateProjectEfforts(IList<ResourceSeriesModel> resourceSeriesModels)
        {
            ArgumentNullException.ThrowIfNull(resourceSeriesModels);
            return SumResourceEfforts(resourceSeriesModels.Select(CalculateResourceEfforts));
        }

        public static double? CalculateMargin(double? cost, double? billing)
        {
            double? abs = CalculateMarginAbsolute(cost, billing);

            if (abs is null)
            {
                if (billing is not null
                    && billing != 0)
                {
                    return 1.0;
                }
            }
            else
            {
                if (abs != 0
                    && billing is not null)
                {
                    if (billing == 0)
                    {
                        return null;
                    }
                    else
                    {
                        return abs / billing;
                    }
                }
            }

            return 0;

            // The above logic is equivalent to the commented out code below:

            //if (abs is null)
            //{
            //    if (billing is null)
            //    {
            //        return 0;
            //    }
            //    else
            //    {
            //        if (billing == 0)
            //        {
            //            return 0;
            //        }
            //        else
            //        {
            //            return 1.0;
            //        }
            //    }
            //}
            //else
            //{
            //    if (abs == 0)
            //    {
            //        if (billing is null)
            //        {
            //            return 0;
            //        }
            //        else
            //        {
            //            if (billing == 0)
            //            {
            //                return 0;
            //            }
            //            else
            //            {
            //                return 0;
            //            }
            //        }
            //    }
            //    else
            //    {
            //        if (billing is null)
            //        {
            //            return 0;
            //        }
            //        else
            //        {
            //            if (billing == 0)
            //            {
            //                return null;
            //            }
            //            else
            //            {
            //                return abs / billing;
            //            }
            //        }
            //    }
            //}
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

        public static ResourceMarginsModel CalculateResourceMargins(
            ResourceCostsModel resourceCostsModel,
            ResourceBillingsModel resourceBillingsModel)
        {
            ArgumentNullException.ThrowIfNull(resourceCostsModel);
            ArgumentNullException.ThrowIfNull(resourceBillingsModel);

            double? direct = CalculateMargin(resourceCostsModel.Direct, resourceBillingsModel.Direct);
            double? indirect = CalculateMargin(resourceCostsModel.Indirect, resourceBillingsModel.Indirect);
            double? other = CalculateMargin(resourceCostsModel.Other, resourceBillingsModel.Other);
            double? total = CalculateMargin(resourceCostsModel.Total, resourceBillingsModel.Total);

            double? directAbs = CalculateMarginAbsolute(resourceCostsModel.Direct, resourceBillingsModel.Direct);
            double? indirectAbs = CalculateMarginAbsolute(resourceCostsModel.Indirect, resourceBillingsModel.Indirect);
            double? otherAbs = CalculateMarginAbsolute(resourceCostsModel.Other, resourceBillingsModel.Other);
            double? totalAbs = CalculateMarginAbsolute(resourceCostsModel.Total, resourceBillingsModel.Total);

            return new ResourceMarginsModel
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
