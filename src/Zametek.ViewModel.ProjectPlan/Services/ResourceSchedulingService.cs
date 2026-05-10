using System.Text;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ResourceSchedulingService
        : IResourceSchedulingService
    {
        #region Fields

        private readonly ProjectPlanMapper m_Mapper;

        #endregion

        #region Ctors

        public ResourceSchedulingService(ProjectPlanMapper mapper)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            m_Mapper = mapper;
        }

        #endregion

        #region Private Methods

        private static ResourceSeriesSetModel CalculateResourceSeriesSet(
            IEnumerable<ResourceScheduleModel> resourceSchedules,
            ResourceSettingsModel resourceSettings)
        {
            ArgumentNullException.ThrowIfNull(resourceSchedules);
            ArgumentNullException.ThrowIfNull(resourceSettings);
            var resourceSeriesSet = new ResourceSeriesSetModel();

            IList<ResourceModel> resources = resourceSettings.Resources;
            double defaultUnitCost = resourceSettings.DefaultUnitCost;
            double defaultUnitBilling = resourceSettings.DefaultUnitBilling;

            var resourceLookup = resources.ToDictionary(x => x.Id);

            if (resourceSchedules.Any())
            {
                int finishTime = resourceSchedules.Select(x => x.FinishTime).DefaultIfEmpty().Max();
                int spareResourceCount = 1;

                // Scheduled resource series.
                // These are the series that apply to None and Direct resources only.
                var scheduledSeriesSet = new List<ResourceSeriesModel>();

                IEnumerable<ResourceScheduleModel> noneAndDirectResourceSchedules = resourceSchedules
                    .Where(x => x.Resource.InterActivityAllocationType == InterActivityAllocationType.None || x.Resource.InterActivityAllocationType == InterActivityAllocationType.Direct);

                // Make 'random' colors seem consistent.
                ColorHelper.PresetReset();

                foreach (ResourceScheduleModel scheduledResourceSchedule in noneAndDirectResourceSchedules)
                {
                    if (scheduledResourceSchedule.ScheduledActivities.Count > 0)
                    {
                        var stringBuilder = new StringBuilder();
                        InterActivityAllocationType interActivityAllocationType = InterActivityAllocationType.None;
                        ColorFormatModel color = ColorHelper.Preset();
                        double unitCost = defaultUnitCost;
                        double unitBilling = defaultUnitBilling;
                        double fixedCost = 0.0;
                        double fixedBilling = 0.0;
                        int displayOrder = 0;

                        if (scheduledResourceSchedule.Resource.Id != default
                            && resourceLookup.TryGetValue(scheduledResourceSchedule.Resource.Id, out ResourceModel? resource))
                        {
                            int resourceId = resource.Id;
                            interActivityAllocationType = resource.InterActivityAllocationType;
                            if (string.IsNullOrWhiteSpace(resource.Name))
                            {
                                stringBuilder.Append($@"{Resource.ProjectPlan.Labels.Label_Resource} {resourceId}");
                            }
                            else
                            {
                                stringBuilder.Append($@"{resource.Name}");
                            }

                            if (resource.ColorFormat is not null)
                            {
                                color = resource.ColorFormat;
                            }

                            unitCost = resource.UnitCost;
                            unitBilling = resource.UnitBilling;
                            fixedCost = resource.FixedCost;
                            fixedBilling = resource.FixedBilling;
                            displayOrder = resource.DisplayOrder;
                        }
                        else
                        {
                            stringBuilder.Append($@"{Resource.ProjectPlan.Labels.Label_Resource} {spareResourceCount}");
                            spareResourceCount++;
                        }

                        var series = new ResourceSeriesModel
                        {
                            Title = stringBuilder.ToString(),
                            ColorFormat = color,
                            UnitCost = unitCost,
                            UnitBilling = unitBilling,
                            FixedCost = fixedCost,
                            FixedBilling = fixedBilling,
                            DisplayOrder = displayOrder,
                            ResourceSchedule = scheduledResourceSchedule,
                            InterActivityAllocationType = interActivityAllocationType,
                        };

                        scheduledSeriesSet.Add(series);
                    }
                }

                // Unscheduled resource series.
                // These are series that apply to Indirect resources, and also
                // None and Direct resources that have roll-off periods.
                var unscheduledSeriesSet = new List<ResourceSeriesModel>();
                var unscheduledResourceSeriesLookup = new Dictionary<int, ResourceSeriesModel>();

                IEnumerable<ResourceScheduleModel> indirectResourceSchedules = resourceSchedules
                    .Where(x => x.Resource.InterActivityAllocationType == InterActivityAllocationType.Indirect);

                foreach (ResourceScheduleModel resourceSchedule in indirectResourceSchedules)
                {
                    if (resourceLookup.TryGetValue(resourceSchedule.Resource.Id, out ResourceModel? resource))
                    {
                        int resourceId = resource.Id;
                        var stringBuilder = new StringBuilder();

                        if (string.IsNullOrWhiteSpace(resource.Name))
                        {
                            stringBuilder.Append($@"{Resource.ProjectPlan.Labels.Label_Resource} {resourceId}");
                        }
                        else
                        {
                            stringBuilder.Append($@"{resource.Name}");
                        }

                        string title = stringBuilder.ToString();

                        var series = new ResourceSeriesModel
                        {
                            Title = title,
                            InterActivityAllocationType = resource.InterActivityAllocationType,
                            ResourceSchedule = resourceSchedule,
                            ColorFormat = resource.ColorFormat != null ? resource.ColorFormat with { } : ColorHelper.Preset(),
                            UnitCost = resource.UnitCost,
                            UnitBilling = resource.UnitBilling,
                            FixedCost = resource.FixedCost,
                            FixedBilling = resource.FixedBilling,
                            DisplayOrder = resource.DisplayOrder,
                        };

                        unscheduledSeriesSet.Add(series);
                        unscheduledResourceSeriesLookup.Add(resourceId, series);
                    }
                }

                // Combined resource series.
                // The intersection of the scheduled and unscheduled series.
                List<ResourceSeriesModel> combinedSeriesSet = [.. scheduledSeriesSet];
                var unscheduledSeriesAlreadyIncluded = new HashSet<int>();

                foreach (ResourceSeriesModel combinedSeries in combinedSeriesSet)
                {
                    IList<bool> combinedActivityAllocations = [.. Enumerable.Repeat(false, finishTime)];
                    IList<bool> combinedCostAllocations = [.. Enumerable.Repeat(false, finishTime)];
                    IList<bool> combinedBillingAllocations = [.. Enumerable.Repeat(false, finishTime)];
                    IList<bool> combinedEffortAllocations = [.. Enumerable.Repeat(false, finishTime)];

                    if (combinedSeries.ResourceSchedule.Resource.Id != default)
                    {
                        int resourceId = combinedSeries.ResourceSchedule.Resource.Id;
                        if (unscheduledResourceSeriesLookup.TryGetValue(resourceId, out ResourceSeriesModel? unscheduledResourceSeries))
                        {
                            combinedActivityAllocations = [.. combinedSeries.ResourceSchedule.ActivityAllocation.Zip(unscheduledResourceSeries.ResourceSchedule.ActivityAllocation, (x, y) => x || y)];
                            combinedCostAllocations = [.. combinedSeries.ResourceSchedule.CostAllocation.Zip(unscheduledResourceSeries.ResourceSchedule.CostAllocation, (x, y) => x || y)];
                            combinedBillingAllocations = [.. combinedSeries.ResourceSchedule.BillingAllocation.Zip(unscheduledResourceSeries.ResourceSchedule.BillingAllocation, (x, y) => x || y)];
                            combinedEffortAllocations = [.. combinedSeries.ResourceSchedule.EffortAllocation.Zip(unscheduledResourceSeries.ResourceSchedule.EffortAllocation, (x, y) => x || y)];
                            unscheduledSeriesAlreadyIncluded.Add(resourceId);
                        }
                        else
                        {
                            combinedActivityAllocations = [.. combinedSeries.ResourceSchedule.ActivityAllocation];
                            combinedCostAllocations = [.. combinedSeries.ResourceSchedule.CostAllocation];
                            combinedBillingAllocations = [.. combinedSeries.ResourceSchedule.BillingAllocation];
                            combinedEffortAllocations = [.. combinedSeries.ResourceSchedule.EffortAllocation];
                        }
                    }
                    else
                    {
                        combinedActivityAllocations = [.. combinedSeries.ResourceSchedule.ActivityAllocation];
                        combinedCostAllocations = [.. combinedSeries.ResourceSchedule.CostAllocation];
                        combinedBillingAllocations = [.. combinedSeries.ResourceSchedule.BillingAllocation];
                        combinedEffortAllocations = [.. combinedSeries.ResourceSchedule.EffortAllocation];
                    }

                    combinedSeries.ResourceSchedule.ActivityAllocation.Clear();
                    combinedSeries.ResourceSchedule.ActivityAllocation.AddRange(combinedActivityAllocations);
                    combinedSeries.ResourceSchedule.CostAllocation.Clear();
                    combinedSeries.ResourceSchedule.CostAllocation.AddRange(combinedCostAllocations);
                    combinedSeries.ResourceSchedule.BillingAllocation.Clear();
                    combinedSeries.ResourceSchedule.BillingAllocation.AddRange(combinedBillingAllocations);
                    combinedSeries.ResourceSchedule.EffortAllocation.Clear();
                    combinedSeries.ResourceSchedule.EffortAllocation.AddRange(combinedEffortAllocations);
                }

                // Finally, add the unscheduled series that have not already been included above.
                // Prepend so that they might be displayed first after sorting.
                List<ResourceSeriesModel> combined = [.. unscheduledSeriesSet.Where(x => !unscheduledSeriesAlreadyIncluded.Contains(x.ResourceSchedule.Resource.Id))];

                combined.AddRange(combinedSeriesSet);

                resourceSeriesSet.ResourceSchedules.AddRange(resourceSchedules);
                resourceSeriesSet.Scheduled.AddRange(scheduledSeriesSet);
                resourceSeriesSet.Unscheduled.AddRange(unscheduledSeriesSet);
                resourceSeriesSet.Combined.AddRange(combined.OrderBy(x => x.DisplayOrder));
            }

            return resourceSeriesSet;
        }

        private static TrackingSeriesSetModel CalculateTrackingSeriesSet(
            IEnumerable<ActivityModel> activities,
            ResourceSettingsModel resourceSettings,
            bool hasResources)
        {
            ArgumentNullException.ThrowIfNull(activities);
            ArgumentNullException.ThrowIfNull(resourceSettings);

            List<ResourceModel> resources = resourceSettings.Resources;

            IList<ActivityModel> orderedActivities = [.. activities
                .Where(x => !x.HasNoEffort)
                .Select(x => x with { })
                .OrderBy(x => x.EarliestFinishTime.GetValueOrDefault())
                .ThenBy(x => x.EarliestStartTime.GetValueOrDefault())];

            // Plan.
            List<TrackingPointModel> planPointSeries = [];

            // Progress.
            List<TrackingPointModel> progressPointSeries = [];

            // Effort.
            List<TrackingPointModel> effortPointSeries = [];

            // Plan Projection.
            List<TrackingPointModel> planProjectionPointSeries = [];

            // Progress Projection.
            List<TrackingPointModel> progressProjectionPointSeries = [];

            // Effort Projection.
            List<TrackingPointModel> effortProjectionPointSeries = [];

            var trackingSeriesSet = new TrackingSeriesSetModel
            {
                Plan = planPointSeries,
                PlanProjection = planProjectionPointSeries,
                Progress = progressPointSeries,
                ProgressProjection = progressProjectionPointSeries,
                Effort = effortPointSeries,
                EffortProjection = effortProjectionPointSeries
            };

            if (!orderedActivities.Any())
            {
                return trackingSeriesSet;
            }

            double totalWorkingTime = Convert.ToDouble(orderedActivities.Sum(s => s.AllocatedToResources.Count * s.Duration));

            // Find the anticipated end time according to the design plan.
            int endTime = 0;

            if (orderedActivities.Count > 0)
            {
                endTime = orderedActivities.Last().EarliestFinishTime.GetValueOrDefault();
            }

            // Always need at least one to mark the start.
            planPointSeries.Add(new TrackingPointModel());

            // Only bother calculating the plan if there is an end time.
            if (endTime > 0)
            {
                // Plan.
                // Build out a matrix of how we would expect progress to proceed
                // for each activity if conditions were predictable.

                var progressTimeline = new List<Dictionary<int, TrackingPointModel>>();

                for (int i = 0; i < endTime; i++)
                {
                    progressTimeline.Add([]);
                }

                // Cycle through each activity and add its individual progress to the matrix.
                foreach (ActivityModel activity in orderedActivities)
                {
                    int startTime = activity.EarliestStartTime.GetValueOrDefault();
                    int finishTime = activity.EarliestFinishTime.GetValueOrDefault();

                    // Check finish time < endtime
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(finishTime, endTime);

                    // Do not bother if these values are not valid.
                    if (finishTime > startTime)
                    {
                        double plannedProgress = 0.0;

                        // Cycle through each time index.
                        for (int timeIndex = 0; timeIndex < endTime; timeIndex++)
                        {
                            // Only need to increment during the period of activity.
                            if (timeIndex >= startTime
                                && timeIndex < finishTime)
                            {
                                plannedProgress++;
                            }

                            Dictionary<int, TrackingPointModel> trackingPointLookup = progressTimeline[timeIndex];

                            if (!trackingPointLookup.TryGetValue(activity.Id, out TrackingPointModel? trackingPointModel))
                            {
                                trackingPointModel = new TrackingPointModel
                                {
                                    Time = timeIndex,
                                    ActivityId = activity.Id,
                                    ActivityName = activity.Name,
                                    Value = plannedProgress,
                                    ValuePercentage = 0.0,
                                };
                                trackingPointLookup.Add(activity.Id, trackingPointModel);
                            }
                            else
                            {
                                trackingPointModel.Value = plannedProgress;
                                trackingPointModel.ValuePercentage = 0.0;
                            }
                        }
                    }
                }

                // At this stage we need to cycle across all the time periods and
                // work out how the time spent on each activity has contributed to
                // overall progress.

                Dictionary<int, ActivityModel> activityLookup = orderedActivities.ToDictionary(activity => activity.Id);

                // Cycle through each time index.
                for (int timeIndex = 0; timeIndex < endTime; timeIndex++)
                {
                    double runningTotalSpent = 0.0;
                    Dictionary<int, TrackingPointModel> trackingPointLookup = progressTimeline[timeIndex];

                    // Now cycle across each activity at this time index.
                    foreach (KeyValuePair<int, TrackingPointModel> trackingPoint in trackingPointLookup)
                    {
                        int activityId = trackingPoint.Key;
                        double portionOfActivityDuration = trackingPoint.Value.Value;

                        if (activityLookup.TryGetValue(activityId, out ActivityModel? activity))
                        {
                            // Remember to count the time spent for each resource used.
                            runningTotalSpent += activity.AllocatedToResources.Count * portionOfActivityDuration;
                        }
                    }

                    // Now record the overall progress for the project and mark it out for the activities
                    // that are being worked on at this moment in time.

                    int time = timeIndex + 1; // Since the equivalent finish time would be the next day.

                    foreach (KeyValuePair<int, TrackingPointModel> trackingPoint in trackingPointLookup)
                    {
                        int activityId = trackingPoint.Key;

                        double percentage = totalWorkingTime == 0 ? 0.0 : 100.0 * runningTotalSpent / totalWorkingTime;

                        if (activityLookup.TryGetValue(activityId, out ActivityModel? activity))
                        {
                            int startTime = activity.EarliestStartTime.GetValueOrDefault();
                            int finishTime = activity.EarliestFinishTime.GetValueOrDefault();

                            if (timeIndex >= startTime
                                && timeIndex < finishTime)
                            {
                                planPointSeries.Add(new TrackingPointModel
                                {
                                    Time = time,
                                    ActivityId = activityId,
                                    ActivityName = activity.Name,
                                    Value = runningTotalSpent,
                                    ValuePercentage = percentage
                                });
                            }
                        }
                    }
                }
            }

            // Find the new end time based on the tracking data.

            int progressTime = activities
                .SelectMany(x => x.Trackers)
                .DefaultIfEmpty()
                .Max(x => x?.Time ?? 0);

            if (progressTime > endTime)
            {
                endTime = progressTime;
            }

            int effortTime = resources
                .SelectMany(x => x.Trackers)
                .DefaultIfEmpty()
                .Max(x => x?.Time ?? 0);

            if (effortTime > endTime)
            {
                endTime = effortTime;
            }

            // Only bother calculating the progress and effort if there is an end time.
            if (endTime > 0)
            {
                {
                    // Progress.
                    progressPointSeries.Add(new TrackingPointModel());

                    // Preprocess the activity trackers so they can be looked up
                    // quickly according to the time.

                    Dictionary<int, (ActivityModel activity, Dictionary<int, ActivityTrackerModel> activityTrackerLookup)> activityBehaviourLookup =
                        orderedActivities.ToDictionary(
                            activity => activity.Id,
                            activity =>
                            {
                                int activityId = activity.Id;
                                Dictionary<int, ActivityTrackerModel> activityTrackerLookup = [];

                                foreach (ActivityTrackerModel tracker in activity.Trackers)
                                {
                                    if (tracker.ActivityId == activityId)
                                    {
                                        activityTrackerLookup[tracker.Time] = tracker;
                                    }
                                }

                                return (activity, activityTrackerLookup);
                            });

                    // This is for tracking the working progresses for each activity.
                    Dictionary<int, int> runningWorkingProgresses = orderedActivities.ToDictionary(activity => activity.Id, activity => 0);

                    // Cycle through each time index.
                    for (int timeIndex = 0; timeIndex <= endTime; timeIndex++)
                    {
                        // Here we need to update the running percentage completion for each activity.
                        foreach ((ActivityModel activity, Dictionary<int, ActivityTrackerModel> activityTrackerLookup) in activityBehaviourLookup.Values)
                        {
                            int runningWorkingProgress = 0;

                            if (runningWorkingProgresses.TryGetValue(activity.Id, out int workingCompletion))
                            {
                                runningWorkingProgress = workingCompletion;
                            }

                            if (activityTrackerLookup.TryGetValue(timeIndex, out ActivityTrackerModel? tracker))
                            {
                                if (tracker.PercentageComplete > runningWorkingProgress)
                                {
                                    runningWorkingProgress = tracker.PercentageComplete;
                                }
                            }

                            runningWorkingProgresses[activity.Id] = runningWorkingProgress;
                        }

                        // Now we can calculate percentage progress for each activity that has
                        // a percentage completed entry.
                        double currentWorkingProgress = 0.0;

                        foreach (ActivityModel activity in orderedActivities)
                        {
                            int percentageCompleted = runningWorkingProgresses[activity.Id];
                            currentWorkingProgress += activity.AllocatedToResources.Count * activity.Duration * (percentageCompleted / 100.0);
                        }

                        double progressPercentage = endTime == 0 ? 0.0 : 100.0 * currentWorkingProgress / totalWorkingTime;
                        int time = timeIndex + 1; // Since the equivalent finish time would be the next day.

                        foreach ((ActivityModel activity, Dictionary<int, ActivityTrackerModel> activityTrackerLookup) in activityBehaviourLookup.Values)
                        {
                            // Now add progress points for activities only if they have
                            // a recorded percentage completed entry for this time index.
                            if (activityTrackerLookup.TryGetValue(timeIndex, out ActivityTrackerModel? tracker))
                            {
                                progressPointSeries.Add(new TrackingPointModel
                                {
                                    Time = time,
                                    ActivityId = activity.Id,
                                    ActivityName = activity.Name,
                                    Value = currentWorkingProgress,
                                    ValuePercentage = progressPercentage
                                });
                            }
                        }
                    }
                }

                // Do not bother with effort measures if we are assuming infinite resources.
                if (hasResources)
                {
                    // Effort
                    effortPointSeries.Add(new TrackingPointModel());

                    // Preprocess the resource trackers so they can be looked up
                    // quickly according to the time.

                    Dictionary<int, (ResourceModel resource, Dictionary<int, ResourceTrackerModel> resourceTrackerLookup)> resourceBehaviourLookup =
                        resources.ToDictionary(
                            resource => resource.Id,
                            resource =>
                            {
                                int resourceId = resource.Id;
                                Dictionary<int, ResourceTrackerModel> resourceTrackerLookup = [];

                                foreach (ResourceTrackerModel tracker in resource.Trackers)
                                {
                                    if (tracker.ResourceId == resourceId)
                                    {
                                        resourceTrackerLookup[tracker.Time] = tracker;
                                    }
                                }

                                return (resource, resourceTrackerLookup);
                            });

                    // This is for tracking the working effort for each activity.
                    Dictionary<int, int> runningWorkingEfforts = orderedActivities.ToDictionary(activity => activity.Id, activity => 0);

                    // Cycle through each time index.
                    for (int timeIndex = 0; timeIndex <= endTime; timeIndex++)
                    {
                        // Here we need to update the running percentage effort for each resource.
                        foreach ((ResourceModel resource, Dictionary<int, ResourceTrackerModel> resourceTrackerLookup) in resourceBehaviourLookup.Values)
                        {
                            foreach (ActivityModel activity in orderedActivities)
                            {
                                int runningWorkingEffort = 0;
                                int activityId = activity.Id;

                                if (runningWorkingEfforts.TryGetValue(activityId, out int workingEffort))
                                {
                                    runningWorkingEffort = workingEffort;
                                }

                                if (resourceTrackerLookup.TryGetValue(timeIndex, out ResourceTrackerModel? tracker))
                                {
                                    foreach (ResourceActivityTrackerModel activityTracker in tracker.ActivityTrackers.Where(x => x.ActivityId == activityId))
                                    {
                                        runningWorkingEffort += activityTracker.PercentageWorked;
                                    }
                                }

                                runningWorkingEfforts[activity.Id] = runningWorkingEffort;
                            }
                        }

                        // Now we can calculate percentage effort for each activity that has
                        // an effort percentage completed entry.
                        double currentWorkingEffort = 0.0;

                        foreach (ActivityModel activity in orderedActivities)
                        {
                            int percentageWorked = runningWorkingEfforts[activity.Id];
                            currentWorkingEffort += percentageWorked / 100.0;
                        }

                        double effortPercentage = endTime == 0 ? 0.0 : 100.0 * currentWorkingEffort / totalWorkingTime;
                        int time = timeIndex + 1; // Since the equivalent finish time would be the next day.

                        foreach ((ResourceModel resource, Dictionary<int, ResourceTrackerModel> resourceTrackerLookup) in resourceBehaviourLookup.Values)
                        {
                            // Now add effort points for activities only if they have
                            // a recorded effort completed entry for this time index.
                            if (resourceTrackerLookup.TryGetValue(timeIndex, out ResourceTrackerModel? tracker))
                            {
                                int resourceId = tracker.ResourceId;

                                foreach (ResourceActivityTrackerModel activityTracker in tracker.ActivityTrackers.Where(x => x.ResourceId == resourceId))
                                {
                                    effortPointSeries.Add(new TrackingPointModel
                                    {
                                        Time = time,
                                        ActivityId = activityTracker.ActivityId,
                                        ActivityName = activityTracker.ActivityName,
                                        Value = currentWorkingEffort,
                                        ValuePercentage = effortPercentage
                                    });
                                }
                            }
                        }
                    }
                }
            }

            // Projections.

            // Plan
            {
                planProjectionPointSeries.Add(new TrackingPointModel());
                // Each series will always have at least one item.
                planProjectionPointSeries.Add(planPointSeries.Last());
            }

            // Progress
            {
                progressProjectionPointSeries.Add(new TrackingPointModel());

                if (progressPointSeries.Count > 1)
                {
                    var projectedLinearFit = MathNet.Numerics.Fit.LineThroughOrigin(
                        [.. progressPointSeries.Select(p => (double)p.Time)],
                        [.. progressPointSeries.Select(p => p.Value)]);

                    var lastTrackingPoint = planPointSeries.Last();

                    if (projectedLinearFit > 0)
                    {
                        var projectedCompletion = lastTrackingPoint.Value / projectedLinearFit;

                        progressProjectionPointSeries.Add(new TrackingPointModel
                        {
                            ActivityId = lastTrackingPoint.ActivityId,
                            ActivityName = lastTrackingPoint.ActivityName,
                            Value = lastTrackingPoint.Value,
                            ValuePercentage = lastTrackingPoint.ValuePercentage,
                            Time = (int)Math.Ceiling(projectedCompletion)
                        });
                    }
                }
            }

            // Effort
            if (hasResources
                && effortPointSeries.Count > 1)
            {
                effortProjectionPointSeries.Add(new TrackingPointModel());

                // We want to project effort out to the greater of the plan or the progress projection.
                var projectedCompletion = Math.Max(
                    progressProjectionPointSeries.Last().Time,
                    planProjectionPointSeries.Last().Time);

                var projectedLinearFit = MathNet.Numerics.Fit.LineThroughOrigin(
                    [.. effortPointSeries.Select(p => (double)p.Time)],
                    [.. effortPointSeries.Select(p => p.Value)]);

                var lastTrackingPoint = planPointSeries.Last();

                if (lastTrackingPoint.Value > 0)
                {
                    var projectedFinalEffort = projectedLinearFit * projectedCompletion;

                    effortProjectionPointSeries.Add(new TrackingPointModel
                    {
                        ActivityId = lastTrackingPoint.ActivityId,
                        ActivityName = lastTrackingPoint.ActivityName,
                        Value = projectedFinalEffort,
                        ValuePercentage = (projectedFinalEffort / lastTrackingPoint.Value) * 100.0,
                        Time = projectedCompletion
                    });
                }
            }

            return trackingSeriesSet;
        }

        #endregion

        #region IResourceSchedulingService Members

        public ResourceSeriesSetModel BuildResourceSeriesSet(
            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
            ResourceSettingsModel resourceSettings)
        {
            ArgumentNullException.ThrowIfNull(graphCompilation);
            ArgumentNullException.ThrowIfNull(resourceSettings);

            IList<ResourceScheduleModel> resourceScheduleModels =
                [.. m_Mapper.ToResourceScheduleModels(graphCompilation)];

            return CalculateResourceSeriesSet(resourceScheduleModels, resourceSettings);
        }

        public TrackingSeriesSetModel BuildTrackingSeriesSet(
            IEnumerable<ActivityModel> activities,
            ResourceSettingsModel resourceSettings,
            bool hasResources)
        {
            ArgumentNullException.ThrowIfNull(activities);
            ArgumentNullException.ThrowIfNull(resourceSettings);

            return CalculateTrackingSeriesSet(activities, resourceSettings, hasResources);
        }

        #endregion
    }
}
