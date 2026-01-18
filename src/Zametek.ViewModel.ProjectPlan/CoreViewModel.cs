using AutoMapper;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class CoreViewModel
        : ViewModelBase, ICoreViewModel
    {
        #region Fields

        private readonly object m_Lock;
        private bool m_TrackIsProjectPlanUpdated;
        private bool m_TrackHasStaleOutputs;

        private readonly VertexGraphCompiler m_VertexGraphCompiler;

        private readonly IProjectPlanFileImport m_ProjectPlanFileImport;
        private readonly IProjectPlanFileExport m_ProjectPlanFileExport;
        private readonly ISettingService m_SettingService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly IMapper m_Mapper;

        private readonly IDisposable? m_NetworkMetricsSub;
        private readonly IDisposable? m_AreActivitiesUncompiledSub;
        private readonly IDisposable? m_CompileOnSettingsUpdateSub;
        private readonly IDisposable? m_BuildArrowGraphSub;
        private readonly IDisposable? m_BuildVertexGraphSub;
        private readonly IDisposable? m_BuildResourceSeriesSetSub;
        private readonly IDisposable? m_BuildTrackingSeriesSetSub;
        private readonly IDisposable? m_BuildRiskMetricsSub;
        private readonly IDisposable? m_BuildFinancialMetricsSub;

        #endregion

        #region Ctors

        public CoreViewModel(
            IProjectPlanFileImport projectPlanFileImport,
            IProjectPlanFileExport projectPlanFileExport,
            ISettingService settingService,
            IDateTimeCalculator dateTimeCalculator,
            IMapper mapper)
        {
            ArgumentNullException.ThrowIfNull(projectPlanFileImport);
            ArgumentNullException.ThrowIfNull(projectPlanFileExport);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(mapper);
            m_Lock = new object();
            m_TrackIsProjectPlanUpdated = true;
            m_TrackHasStaleOutputs = true;
            m_VertexGraphCompiler = new VertexGraphCompiler();
            m_ProjectPlanFileImport = projectPlanFileImport;
            m_ProjectPlanFileExport = projectPlanFileExport;
            m_SettingService = settingService;
            m_DateTimeCalculator = dateTimeCalculator;
            m_DisplaySettingsViewModel = new DisplaySettingsViewModel(
                m_DateTimeCalculator,
                SetIsProjectPlanUpdated,
                () => IsReadyToCompile = ReadyToCompile.Yes);
            m_Mapper = mapper;

            m_IsReadyToCompile = ReadyToCompile.No;
            m_IsBusy = false;
            m_HasStaleOutputs = false;
            m_ProjectStart = new(DateTime.Today);
            m_Today = new(DateTime.Today);
            m_ResourceSettings = new ResourceSettingsModel();
            m_Activities = new();
            m_GraphSettings = m_SettingService.DefaultGraphSettings;
            m_ResourceSettings = m_SettingService.DefaultResourceSettings;
            m_WorkStreamSettings = m_SettingService.DefaultWorkStreamSettings;
            m_RiskMetrics = new();
            m_CostMetrics = new();
            m_BillingMetrics = new();
            m_MarginMetrics = new();
            m_EffortMetrics = new();
            m_NetworkMetrics = new();

            DisplaySettingsViewModel.ShowDates = m_SettingService.DefaultShowDates;
            DisplaySettingsViewModel.UseClassicDates = m_SettingService.DefaultUseClassicDates;
            DisplaySettingsViewModel.UseBusinessDays = m_SettingService.DefaultUseBusinessDays;
            DisplaySettingsViewModel.HideCost = m_SettingService.DefaultHideCost;
            DisplaySettingsViewModel.HideBilling = m_SettingService.DefaultHideBilling;

            m_GraphCompilation = new GraphCompilation<int, int, int, DependentActivity>([], [], []);
            m_ArrowGraph = new ArrowGraphModel();
            m_VertexGraph = new VertexGraphModel();
            m_ResourceSeriesSet = new ResourceSeriesSetModel();
            m_TrackingSeriesSet = new TrackingSeriesSetModel();

            m_SelectedTheme = m_SettingService.SelectedTheme;

            // Create read-only view to the source list.
            m_Activities.Connect()
               .ObserveOn(RxApp.MainThreadScheduler)
               .Bind(out m_ReadOnlyActivities)
               .Subscribe();

            m_HasActivities = m_ReadOnlyActivities
                .ToObservableChangeSet()
                .Select(x => m_ReadOnlyActivities.Count > 0)
                .ToProperty(this, core => core.HasActivities);

            m_HasResources = this
                .WhenAnyValue(
                    core => core.ResourceSettings,
                    settings => settings.Resources.Count > 0 && !settings.AreDisabled)
                .ToProperty(this, core => core.HasResources);

            m_HasWorkStreams = this
                .WhenAnyValue(
                    core => core.WorkStreamSettings,
                    settings => settings.WorkStreams.Count > 0)
                .ToProperty(this, core => core.HasWorkStreams);

            m_HasPhases = this
                .WhenAnyValue(
                    core => core.WorkStreamSettings,
                    settings => settings.WorkStreams.Any(x => x.IsPhase))
                .ToProperty(this, core => core.HasPhases);

            m_AreActivitiesUncompiledSub = m_ReadOnlyActivities
                .ToObservableChangeSet()
                .AutoRefresh(activity => activity.IsCompiled) // Subscribe only to IsCompiled property changes
                .Filter(activity => !activity.IsCompiled)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(changeSet =>
                {
                    if (!IsBusy && changeSet.Replaced > 0)
                    {
                        lock (m_Lock)
                        {
                            if (AutoCompile)
                            {
                                IsReadyToReviseTrackers = ReadyToRevise.Yes;
                                IsReadyToCompile = ReadyToCompile.Yes;
                            }
                            else
                            {
                                IsReadyToReviseTrackers = ReadyToRevise.No;
                                IsReadyToCompile = ReadyToCompile.No;
                            }
                        }
                    }
                });

            m_CompileOnSettingsUpdateSub = this
                .WhenAnyValue(core => core.IsReadyToCompile)
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(isReady =>
                {
                    if (isReady == ReadyToCompile.Yes
                        && !IsBusy)
                    {
                        lock (m_Lock)
                        {
                            if (isReady == ReadyToCompile.Yes
                                && !IsBusy)
                            {
                                RunAutoCompile();
                            }
                        }
                    }
                });

            m_BuildArrowGraphSub = this
                .ObservableForProperty(core => core.GraphCompilation)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildArrowGraph());

            m_BuildVertexGraphSub = this
                .ObservableForProperty(core => core.GraphCompilation)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildVertexGraph());

            m_BuildResourceSeriesSetSub = this
                .ObservableForProperty(core => core.GraphCompilation)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildResourceSeriesSet());

            m_BuildTrackingSeriesSetSub = this
                .ObservableForProperty(core => core.GraphCompilation)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildTrackingSeriesSet());

            m_NetworkMetricsSub = this
                .WhenAnyValue(
                    core => core.GraphCompilation,
                    core => core.HasCompilationErrors)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildNetworkMetrics());

            m_BuildRiskMetricsSub = this
                .WhenAnyValue(
                    core => core.GraphCompilation,
                    core => core.GraphSettings,
                    core => core.HasCompilationErrors)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildRiskMetrics());

            m_BuildFinancialMetricsSub = this
                .WhenAnyValue(
                    core => core.ResourceSeriesSet,
                    core => core.HasCompilationErrors)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildFinancialMetrics());
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
                Dictionary<int, ColorFormatModel> colorFormatLookup = resources.ToDictionary(x => x.Id, x => x.ColorFormat);
                int finishTime = resourceSchedules.Select(x => x.FinishTime).DefaultIfEmpty().Max();
                int spareResourceCount = 1;
                //var resourceScheduleLookup = resourceSchedules.ToDictionary(x => x.Resource.Id);

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

                            if (colorFormatLookup.TryGetValue(resourceId, out ColorFormatModel? colorFormat))
                            {
                                color = colorFormat;
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

                // These are series the that apply to Indirect resources, and also
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
                            ColorFormat = resource.ColorFormat != null ? resource.ColorFormat.CloneObject() : ColorHelper.Preset(),
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
                List<ResourceSeriesModel> combinedSeriesSet = scheduledSeriesSet.CloneObject();
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
                .Select(x => x.CloneObject())
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

            // Only bother calculating the plan is there is an end time.
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

                // At this stage we need to cycle across all the time period and
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

            // Only bother calculating the progress and effort is there is an end time.
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
                        // a effort percentage completed entry.
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

                //if (planPointSeries.Count > 1)
                //{
                //    var projectedLinearFit = MathNet.Numerics.Fit.LineThroughOrigin(
                //        planPointSeries.Select(p => (double)p.Time).ToArray(),
                //        planPointSeries.Select(p => p.Value).ToArray());

                //    var lastTrackingPoint = planPointSeries.Last();

                //    if (projectedLinearFit > 0)
                //    {
                //        var projectedCompletion = lastTrackingPoint.Value / projectedLinearFit;

                //        planProjectionPointSeries.Add(new TrackingPointModel
                //        {
                //            ActivityId = lastTrackingPoint.ActivityId,
                //            ActivityName = lastTrackingPoint.ActivityName,
                //            Value = lastTrackingPoint.Value,
                //            ValuePercentage = lastTrackingPoint.ValuePercentage,
                //            Time = (int)Math.Ceiling(projectedCompletion)
                //        });
                //    }
                //}

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

                // We want to project effort out to the greater of the plan or the progress projection
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

        private static int? CalculateCyclomaticComplexity(IEnumerable<IDependentActivity> dependentActivities)
        {
            ArgumentNullException.ThrowIfNull(dependentActivities);

            IEnumerable<IDependentActivity> dependentActivitiesCopy =
                dependentActivities.Select(x => (IDependentActivity)x.CloneObject());

            if (!dependentActivitiesCopy.Any())
            {
                return null;
            }

            var vertexGraphCompiler = new VertexGraphCompiler();

            foreach (var dependentActivity in dependentActivitiesCopy.Cast<DependentActivity>())
            {
                dependentActivity.Dependencies.UnionWith(dependentActivity.PlanningDependencies);
                dependentActivity.Dependencies.UnionWith(dependentActivity.ResourceDependencies);
                dependentActivity.PlanningDependencies.Clear();
                dependentActivity.ResourceDependencies.Clear();
                vertexGraphCompiler.AddActivity(dependentActivity);
            }

            vertexGraphCompiler.TransitiveReduction();
            return vertexGraphCompiler.CyclomaticComplexity;
        }

        private static double? CalculateDurationManMonths(
            int? duration,
            int daysPerWeek)
        {
            if (duration is not null
                && duration != 0
                && daysPerWeek != 0)
            {
                return duration / (daysPerWeek * 52 / 12.0);
            }
            return null;
        }

        private void SetIsProjectPlanUpdated(bool isProjectPlanUpdated, bool trackStaleOutputs)
        {
            lock (m_Lock)
            {
                bool originalTrackValue = m_TrackHasStaleOutputs;
                m_TrackHasStaleOutputs = originalTrackValue && trackStaleOutputs;
                IsProjectPlanUpdated = isProjectPlanUpdated;
                m_TrackHasStaleOutputs = originalTrackValue;
            }
        }

        #endregion

        #region ICoreViewModel Members

        private Guid m_ProjectPlanId;
        public Guid ProjectPlanId
        {
            get => m_ProjectPlanId;
            private set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_ProjectPlanId, value);
                }
            }
        }

        private bool m_IsBusy;
        public bool IsBusy
        {
            get => m_IsBusy;
            private set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_IsBusy, value);
            }
        }

        // We need to use an enum because raised changes on bools aren't always captured.
        // https://github.com/reactiveui/ReactiveUI/issues/3846
        private ReadyToCompile m_IsReadyToCompile;

        // This should always be the last thing altered in order to trigger a compile.
        public ReadyToCompile IsReadyToCompile
        {
            get => m_IsReadyToCompile;
            private set
            {
                lock (m_Lock)
                {
                    if (m_TrackIsProjectPlanUpdated)
                    {
                        this.RaiseAndSetIfChanged(ref m_IsReadyToCompile, value);
                    }
                }
            }
        }

        private bool m_IsProjectPlanUpdated;
        public bool IsProjectPlanUpdated
        {
            get => m_IsProjectPlanUpdated;
            set
            {
                lock (m_Lock)
                {
                    HasStaleOutputs = value;
                    if (m_TrackIsProjectPlanUpdated)
                    {
                        this.RaiseAndSetIfChanged(ref m_IsProjectPlanUpdated, value);
                    }
                }
            }
        }

        private bool m_HasStaleOutputs;
        public bool HasStaleOutputs
        {
            get => m_HasStaleOutputs;
            set
            {
                lock (m_Lock)
                {
                    if (m_TrackHasStaleOutputs)
                    {
                        this.RaiseAndSetIfChanged(ref m_HasStaleOutputs, value);
                    }
                }
            }
        }

        private DateTimeOffset m_ProjectStart;
        public DateTimeOffset ProjectStart
        {
            get => m_ProjectStart;
            set
            {
                lock (m_Lock)
                {
                    IsProjectPlanUpdated = true;
                    this.RaiseAndSetIfChanged(ref m_ProjectStart, value);
                    IsReadyToCompile = ReadyToCompile.Yes;
                }
            }
        }

        private DateTimeOffset m_Today;
        public DateTimeOffset Today
        {
            get => m_Today;
            set
            {
                lock (m_Lock)
                {
                    SetIsProjectPlanUpdated(isProjectPlanUpdated: true, trackStaleOutputs: false);
                    this.RaiseAndSetIfChanged(ref m_Today, value);
                }
            }
        }

        private readonly DisplaySettingsViewModel m_DisplaySettingsViewModel;
        public IDisplaySettingsViewModel DisplaySettingsViewModel
        {
            get => m_DisplaySettingsViewModel;
        }

        public bool DefaultShowDates
        {
            get => m_SettingService.DefaultShowDates;
            set
            {
                lock (m_Lock)
                {
                    m_SettingService.DefaultShowDates = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public bool DefaultUseClassicDates
        {
            get => m_SettingService.DefaultUseClassicDates;
            set
            {
                lock (m_Lock)
                {
                    m_SettingService.DefaultUseClassicDates = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public bool DefaultUseBusinessDays
        {
            get => m_SettingService.DefaultUseBusinessDays;
            set
            {
                lock (m_Lock)
                {
                    m_SettingService.DefaultUseBusinessDays = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public bool DefaultHideCost
        {
            get => m_SettingService.DefaultHideCost;
            set
            {
                lock (m_Lock)
                {
                    m_SettingService.DefaultHideCost = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public bool DefaultHideBilling
        {
            get => m_SettingService.DefaultHideBilling;
            set
            {
                lock (m_Lock)
                {
                    m_SettingService.DefaultHideBilling = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        private bool m_AutoCompile;
        public bool AutoCompile
        {
            get => m_AutoCompile;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_AutoCompile, value);

                    if (m_AutoCompile)
                    {
                        IsReadyToCompile = ReadyToCompile.No;
                    }
                }
            }
        }

        private string m_SelectedTheme;

        public string SelectedTheme
        {
            get => m_SelectedTheme;
            set
            {
                lock (m_Lock)
                {
                    m_SettingService.SelectedTheme = value;
                    this.RaiseAndSetIfChanged(ref m_SelectedTheme, value);
                }
            }
        }

        private BaseTheme m_BaseTheme;
        public BaseTheme BaseTheme
        {
            get => m_BaseTheme;
            set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_BaseTheme, value);
            }
        }

        private readonly SourceList<IManagedActivityViewModel> m_Activities;
        public IReadOnlyList<IManagedActivityViewModel> RawActivities => m_Activities.Items;

        private readonly ReadOnlyObservableCollection<IManagedActivityViewModel> m_ReadOnlyActivities;
        public ReadOnlyObservableCollection<IManagedActivityViewModel> Activities => m_ReadOnlyActivities;

        private GraphSettingsModel m_GraphSettings;
        public GraphSettingsModel GraphSettings
        {
            get => m_GraphSettings;
            set
            {
                lock (m_Lock)
                {
                    m_GraphSettings = value;
                    IsProjectPlanUpdated = true;
                    this.RaisePropertyChanged();
                    IsReadyToCompile = ReadyToCompile.Yes;
                }
            }
        }

        private ResourceSettingsModel m_ResourceSettings;
        public ResourceSettingsModel ResourceSettings
        {
            get => m_ResourceSettings;
            set
            {
                lock (m_Lock)
                {
                    m_ResourceSettings = value;
                    IsProjectPlanUpdated = true;
                    this.RaisePropertyChanged();
                    IsReadyToCompile = ReadyToCompile.Yes;
                }
            }
        }

        private WorkStreamSettingsModel m_WorkStreamSettings;
        public WorkStreamSettingsModel WorkStreamSettings
        {
            get => m_WorkStreamSettings;
            set
            {
                lock (m_Lock)
                {
                    m_WorkStreamSettings = value;
                    IsProjectPlanUpdated = true;
                    this.RaisePropertyChanged();
                    IsReadyToCompile = ReadyToCompile.Yes;
                }
            }
        }

        public MetricsModel Metrics
        {
            get
            {
                return new MetricsModel
                {
                    Risks = RiskMetrics,
                    Costs = CostMetrics,
                    Billings = BillingMetrics,
                    Margins = MarginMetrics,
                    Efforts = EffortMetrics,
                    Network = NetworkMetrics,
                };
            }
            private set
            {
                RiskMetrics = value.Risks;
                CostMetrics = value.Costs;
                BillingMetrics = value.Billings;
                MarginMetrics = value.Margins;
                EffortMetrics = value.Efforts;
                NetworkMetrics = value.Network;
                this.RaisePropertyChanged();
            }
        }

        private RisksModel m_RiskMetrics;
        public RisksModel RiskMetrics
        {
            get => m_RiskMetrics;
            private set
            {
                lock (m_Lock)
                {
                    if (m_RiskMetrics == value)
                    {
                        return;
                    }
                    m_RiskMetrics = value;
                    SetIsProjectPlanUpdated(isProjectPlanUpdated: true, trackStaleOutputs: false);
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged(nameof(Metrics));
                }
            }
        }

        private CostsModel m_CostMetrics;
        public CostsModel CostMetrics
        {
            get => m_CostMetrics;
            private set
            {
                lock (m_Lock)
                {
                    if (m_CostMetrics == value)
                    {
                        return;
                    }
                    m_CostMetrics = value;
                    SetIsProjectPlanUpdated(isProjectPlanUpdated: true, trackStaleOutputs: false);
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged(nameof(Metrics));
                }
            }
        }

        private BillingsModel m_BillingMetrics;
        public BillingsModel BillingMetrics
        {
            get => m_BillingMetrics;
            private set
            {
                lock (m_Lock)
                {
                    if (m_BillingMetrics == value)
                    {
                        return;
                    }
                    m_BillingMetrics = value;
                    SetIsProjectPlanUpdated(isProjectPlanUpdated: true, trackStaleOutputs: false);
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged(nameof(Metrics));
                }
            }
        }

        private MarginsModel m_MarginMetrics;
        public MarginsModel MarginMetrics
        {
            get => m_MarginMetrics;
            private set
            {
                lock (m_Lock)
                {
                    if (m_MarginMetrics == value)
                    {
                        return;
                    }
                    m_MarginMetrics = value;
                    SetIsProjectPlanUpdated(isProjectPlanUpdated: true, trackStaleOutputs: false);
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged(nameof(Metrics));
                }
            }
        }

        private EffortsModel m_EffortMetrics;
        public EffortsModel EffortMetrics
        {
            get => m_EffortMetrics;
            private set
            {
                lock (m_Lock)
                {
                    if (m_EffortMetrics == value)
                    {
                        return;
                    }
                    m_EffortMetrics = value;
                    SetIsProjectPlanUpdated(isProjectPlanUpdated: true, trackStaleOutputs: false);
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged(nameof(Metrics));
                }
            }
        }

        private NetworkModel m_NetworkMetrics;
        public NetworkModel NetworkMetrics
        {
            get => m_NetworkMetrics;
            private set
            {
                lock (m_Lock)
                {
                    if (m_NetworkMetrics == value)
                    {
                        return;
                    }
                    m_NetworkMetrics = value;
                    SetIsProjectPlanUpdated(isProjectPlanUpdated: true, trackStaleOutputs: false);
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged(nameof(Metrics));
                }
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_HasActivities;
        public bool HasActivities => m_HasActivities.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasResources;
        public bool HasResources => m_HasResources.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasWorkStreams;
        public bool HasWorkStreams => m_HasWorkStreams.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasPhases;
        public bool HasPhases => m_HasPhases.Value;

        private bool m_HasCompilationErrors;
        public bool HasCompilationErrors
        {
            get => m_HasCompilationErrors;
            private set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_HasCompilationErrors, value);
                }
            }
        }

        private IGraphCompilation<int, int, int, IDependentActivity> m_GraphCompilation;
        public IGraphCompilation<int, int, int, IDependentActivity> GraphCompilation
        {
            get => m_GraphCompilation;
            private set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_GraphCompilation, value);
                }
            }
        }

        private ArrowGraphModel m_ArrowGraph;
        public ArrowGraphModel ArrowGraph
        {
            get => m_ArrowGraph;
            private set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_ArrowGraph, value);
                }
            }
        }

        private VertexGraphModel m_VertexGraph;
        public VertexGraphModel VertexGraph
        {
            get => m_VertexGraph;
            private set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_VertexGraph, value);
                }
            }
        }

        private ResourceSeriesSetModel m_ResourceSeriesSet;
        public ResourceSeriesSetModel ResourceSeriesSet
        {
            get => m_ResourceSeriesSet;
            private set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_ResourceSeriesSet, value);
            }
        }

        private TrackingSeriesSetModel m_TrackingSeriesSet;
        public TrackingSeriesSetModel TrackingSeriesSet
        {
            get => m_TrackingSeriesSet;
            private set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_TrackingSeriesSet, value);
            }
        }

        private int m_TrackerIndex;
        public int TrackerIndex
        {
            get => m_TrackerIndex;
            set => this.RaiseAndSetIfChanged(ref m_TrackerIndex, value);
        }

        private ReadyToRevise m_IsReadyToReviseTrackers;
        public ReadyToRevise IsReadyToReviseTrackers
        {
            get => m_IsReadyToReviseTrackers;
            set
            {
                m_IsReadyToReviseTrackers = value;
                this.RaisePropertyChanged();
            }
        }

        //private ReadyToRevise m_IsReadyToReviseSettings;
        //public ReadyToRevise IsReadyToReviseSettings
        //{
        //    get => m_IsReadyToReviseSettings;
        //    set
        //    {
        //        m_IsReadyToReviseSettings = value;
        //        this.RaisePropertyChanged();
        //    }
        //}

        public ProjectPlanModel CreateEmptyProjectPlan()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;

                    var plan = new ProjectPlanModel
                    {
                        ProjectStart = new(DateTime.Today),
                        Today = new(DateTime.Today),
                        DependentActivities = [],
                        GraphSettings = m_SettingService.DefaultGraphSettings,
                        ResourceSettings = m_SettingService.DefaultResourceSettings,
                        WorkStreamSettings = m_SettingService.DefaultWorkStreamSettings,
                        Metrics = new(),
                        DisplaySettings = new DisplaySettingsModel
                        {
                            ShowDates = m_SettingService.DefaultShowDates,
                            UseClassicDates = m_SettingService.DefaultUseClassicDates,
                            UseBusinessDays = m_SettingService.DefaultUseBusinessDays,
                            HideCost = m_SettingService.DefaultHideCost,
                            HideBilling = m_SettingService.DefaultHideBilling,
                        },
                    };

                    return plan;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ClearSettings()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    ProjectPlanModel emptyPlan = CreateEmptyProjectPlan();

                    ProjectStart = emptyPlan.ProjectStart;
                    Today = emptyPlan.Today;

                    GraphSettings = emptyPlan.GraphSettings;
                    ResourceSettings = emptyPlan.ResourceSettings;
                    WorkStreamSettings = emptyPlan.WorkStreamSettings;

                    DisplaySettingsModel defaultDisplaySettings = emptyPlan.DisplaySettings;
                    DisplaySettingsViewModel.SetValues(defaultDisplaySettings);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ResetProjectPlan()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    m_TrackIsProjectPlanUpdated = false;
                    m_TrackHasStaleOutputs = false;

                    ClearManagedActivities();

                    ClearSettings();

                    Metrics = new();

                    HasCompilationErrors = false;
                    GraphCompilation = new GraphCompilation<int, int, int, DependentActivity>([], [], []);

                    ArrowGraph = new();
                    VertexGraph = new();

                    IsReadyToCompile = ReadyToCompile.No;
                    IsReadyToReviseTrackers = ReadyToRevise.No;
                    //IsReadyToReviseSettings = ReadyToRevise.No;

                    //m_SettingService.Reset();

                    m_TrackIsProjectPlanUpdated = true;
                    IsProjectPlanUpdated = false;
                    ProjectPlanId = Guid.NewGuid();

                    m_TrackHasStaleOutputs = true;
                    HasStaleOutputs = false;
                }
            }
            finally
            {
                m_TrackIsProjectPlanUpdated = true;
                m_TrackHasStaleOutputs = true;
                IsBusy = false;
            }
        }

        public ProjectPlanImportModel ImportProjectPlanFile(string filename)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    return m_ProjectPlanFileImport.ImportProjectPlanFile(filename);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ExportProjectPlanFile(
            ProjectPlanModel projectPlanModel,
            ResourceSeriesSetModel resourceSeriesSetModel,
            TrackingSeriesSetModel trackingSeriesSetModel,
            bool showDates,
            string filename)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    m_ProjectPlanFileExport.ExportProjectPlanFile(
                        projectPlanModel,
                        resourceSeriesSetModel,
                        trackingSeriesSetModel,
                        showDates,
                        filename);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ProcessProjectPlanImport(
            ProjectPlanImportModel projectPlanImportModel,
            Guid projectPlanId)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    ResetProjectPlan();
                    m_TrackIsProjectPlanUpdated = false;
                    m_TrackHasStaleOutputs = false;
                    ProjectPlanId = projectPlanId;

                    // Default display mode is required for all file opening and closing.
                    m_DateTimeCalculator.DisplayMode = DateTimeDisplayMode.Default;

                    // Project Start Date.
                    ProjectStart = projectPlanImportModel.ProjectStart;

                    // Project Start Date.
                    Today = projectPlanImportModel.Today;

                    // Work Stream settings.
                    WorkStreamSettingsModel workStreamSettings = m_SettingService.DefaultWorkStreamSettings.CloneObject();

                    if (projectPlanImportModel.WorkStreams.Count != 0)
                    {
                        workStreamSettings.WorkStreams.Clear();

                        foreach (WorkStreamModel workStream in projectPlanImportModel.WorkStreams)
                        {
                            workStreamSettings.WorkStreams.Add(workStream);
                        }
                    }

                    WorkStreamSettings = workStreamSettings;

                    // Resources.
                    ResourceSettingsModel resourceSettings = m_SettingService.DefaultResourceSettings.CloneObject();
                    resourceSettings = resourceSettings with
                    {
                        DefaultUnitCost = projectPlanImportModel.ResourceSettings.DefaultUnitCost,
                        DefaultUnitBilling = projectPlanImportModel.ResourceSettings.DefaultUnitBilling,
                        AreDisabled = projectPlanImportModel.ResourceSettings.AreDisabled,
                    };

                    if (projectPlanImportModel.ResourceSettings.Resources.Count != 0)
                    {
                        resourceSettings.Resources.Clear();

                        foreach (ResourceModel resource in projectPlanImportModel.ResourceSettings.Resources)
                        {
                            resourceSettings.Resources.Add(resource);
                        }
                    }

                    ResourceSettings = resourceSettings;

                    // Graph settings.
                    GraphSettingsModel graphSettings = m_SettingService.DefaultGraphSettings.CloneObject();

                    if (projectPlanImportModel.ActivitySeverities.Count != 0)
                    {
                        graphSettings.ActivitySeverities.Clear();

                        foreach (ActivitySeverityModel activitySeverity in projectPlanImportModel.ActivitySeverities)
                        {
                            graphSettings.ActivitySeverities.Add(activitySeverity);
                        }
                    }

                    GraphSettings = graphSettings;

                    // Activities.
                    // Be sure to set the ResourceSettings first, so that the activities know
                    // which resources are being referred to when marking them as selected.
                    AddManagedActivities(projectPlanImportModel.DependentActivities);

                    // Display settings.
                    DisplaySettingsViewModel.SetValues(projectPlanImportModel.DisplaySettings);

                    RunCompile();

                    //// Metrics.
                    //// It is important to put this after the compilation, so it will only
                    //// trigger a project plan updated event if it is different from the compiled metrics.
                    //Metrics = projectPlanModel.Metrics;

                    m_TrackIsProjectPlanUpdated = true;
                    IsProjectPlanUpdated = true;
                    m_TrackHasStaleOutputs = true;
                }
            }
            finally
            {
                m_TrackIsProjectPlanUpdated = true;
                m_TrackHasStaleOutputs = true;
                IsBusy = false;
            }
        }

        public void ProcessProjectPlan(
            ProjectPlanModel projectPlanModel,
            Guid projectPlanId)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    ResetProjectPlan();
                    m_TrackIsProjectPlanUpdated = false;
                    m_TrackHasStaleOutputs = false;
                    ProjectPlanId = projectPlanId;

                    // Default display mode is required for all file opening and closing.
                    m_DateTimeCalculator.DisplayMode = DateTimeDisplayMode.Default;

                    // Project Start Date.
                    ProjectStart = projectPlanModel.ProjectStart;

                    // Project Start Date.
                    Today = projectPlanModel.Today;

                    // Display settings.
                    var displaySettings = projectPlanModel.DisplaySettings with
                    {
                        ShowDates = DisplaySettingsViewModel.ShowDates,
                        UseClassicDates = DisplaySettingsViewModel.UseClassicDates,
                        UseBusinessDays = DisplaySettingsViewModel.UseBusinessDays,
                    };

                    DisplaySettingsViewModel.SetValues(displaySettings);

                    // Work Stream Settings.
                    WorkStreamSettings = projectPlanModel.WorkStreamSettings;

                    // Resource Settings.
                    ResourceSettings = projectPlanModel.ResourceSettings;

                    // Graph Settings.
                    GraphSettings = projectPlanModel.GraphSettings;

                    // Activities.
                    AddManagedActivities(projectPlanModel.DependentActivities);

                    // Now that Resources and Activities are in place,
                    // revise all tracker values.
                    IsReadyToReviseTrackers = ReadyToRevise.Yes;

                    // Now update Settings to the core model.
                    //IsReadyToReviseSettings = ReadyToRevise.Yes;

                    // Display settings (the rest of the settings).
                    displaySettings = projectPlanModel.DisplaySettings with
                    {
                        ShowDates = projectPlanModel.DisplaySettings.ShowDates,
                        UseClassicDates = projectPlanModel.DisplaySettings.UseClassicDates,
                        UseBusinessDays = projectPlanModel.DisplaySettings.UseBusinessDays,
                    };

                    DisplaySettingsViewModel.SetValues(displaySettings);

                    RunCompile();

                    // Metrics.
                    // It is important to put this after the compilation, so it will only
                    // trigger a project plan updated event if it is different from the compiled metrics.
                    Metrics = projectPlanModel.Metrics;

                    m_TrackIsProjectPlanUpdated = true;
                    IsProjectPlanUpdated = false;
                    m_TrackHasStaleOutputs = true;
                }
            }
            finally
            {
                m_TrackIsProjectPlanUpdated = true;
                m_TrackHasStaleOutputs = true;
                IsBusy = false;
            }
        }

        public ProjectPlanModel BuildProjectPlan()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    var graphCompilation = m_Mapper.Map<IGraphCompilation<int, int, int, IDependentActivity>, GraphCompilationModel>(GraphCompilation);

                    // Default display mode is required for all file opening and closing.
                    DateTimeDisplayMode oldDisplayMode = m_DateTimeCalculator.DisplayMode;
                    m_DateTimeCalculator.DisplayMode = DateTimeDisplayMode.Default;

                    var plan = new ProjectPlanModel
                    {
                        ProjectStart = ProjectStart,
                        Today = Today,
                        DependentActivities = m_Mapper.Map<List<DependentActivityModel>>(RawActivities),
                        GraphSettings = GraphSettings.CloneObject(),
                        ResourceSettings = ResourceSettings.CloneObject(),
                        WorkStreamSettings = WorkStreamSettings.CloneObject(),
                        Metrics = Metrics.CloneObject(),
                        DisplaySettings = DisplaySettingsViewModel.GetValues(),
                    };

                    // Reorder activity dependencies so they are more readable.
                    foreach (DependentActivityModel activityModel in plan.DependentActivities)
                    {
                        activityModel.Dependencies.Sort();
                        activityModel.PlanningDependencies.Sort();
                        activityModel.ResourceDependencies.Sort();
                        activityModel.Successors.Sort();
                    }

                    // Put display mode back to the way it was.
                    m_DateTimeCalculator.DisplayMode = oldDisplayMode;

                    return plan;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public int AddManagedActivity()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    var activityId = m_VertexGraphCompiler.GetNextActivityId();
                    var set = new HashSet<DependentActivityModel>
                    {
                        new()
                        {
                            Activity = new ActivityModel
                            {
                                Id = activityId
                            }
                        }
                    };
                    AddManagedActivities(set);
                    return activityId;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void AddManagedActivities(IEnumerable<DependentActivityModel> dependentActivityModels)
        {
            try
            {
                lock (m_Lock)
                {
                    m_Activities.Edit(activities =>
                    {
                        IsBusy = true;

                        foreach (DependentActivityModel dependentActivity in dependentActivityModels)
                        {
                            var activity = new ManagedActivityViewModel(
                                this,
                                m_Mapper.Map<DependentActivityModel, DependentActivity>(dependentActivity),
                                m_DateTimeCalculator,
                                m_VertexGraphCompiler,
                                ProjectStart,
                                dependentActivity.Activity.Trackers,
                                dependentActivity.Activity.MinimumEarliestStartDateTime,
                                dependentActivity.Activity.MaximumLatestFinishDateTime);

                            if (m_VertexGraphCompiler.AddActivity(activity))
                            {
                                activities.Add(activity);
                            }
                            else
                            {
                                activity.Dispose();
                            }
                        }
                    });

                    //IsProjectPlanUpdated = true;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void RemoveManagedActivities(IEnumerable<int> dependentActivityIds)
        {
            try
            {
                lock (m_Lock)
                {
                    m_Activities.Edit(activities =>
                    {
                        IsBusy = true;
                        IEnumerable<IManagedActivityViewModel> dependentActivities = [.. RawActivities.Where(x => dependentActivityIds.Contains(x.Id))];

                        foreach (IManagedActivityViewModel dependentActivity in dependentActivities)
                        {
                            if (m_VertexGraphCompiler.RemoveActivity(dependentActivity.Id))
                            {
                                activities.Remove(dependentActivity);
                                dependentActivity.Dispose();
                            }
                        }
                    });

                    IsProjectPlanUpdated = true;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void UpdateManagedActivities(IEnumerable<UpdateDependentActivityModel> updateModels)
        {
            try
            {
                lock (m_Lock)
                {
                    m_Activities.Edit(activities =>
                    {
                        IsBusy = true;
                        Dictionary<int, IManagedActivityViewModel> activityLookup = RawActivities.ToDictionary(x => x.Id);

                        foreach (UpdateDependentActivityModel updateModel in updateModels)
                        {
                            if (activityLookup.TryGetValue(updateModel.Id, out IManagedActivityViewModel? activity))
                            {
                                if (activity is IEditableObject editable)
                                {
                                    editable.BeginEdit();

                                    if (updateModel.IsNameEdited)
                                    {
                                        activity.Name = updateModel.Name;
                                    }
                                    if (updateModel.IsNotesEdited)
                                    {
                                        activity.Notes = updateModel.Notes;
                                    }
                                    if (updateModel.IsTargetWorkStreamsEdited)
                                    {
                                        activity.WorkStreamSelector.SetSelectedTargetWorkStreams([.. updateModel.TargetWorkStreams]);
                                    }
                                    if (updateModel.IsTargetResourcesEdited)
                                    {
                                        activity.ResourceSelector.SetSelectedTargetResources([.. updateModel.TargetResources]);
                                    }
                                    if (updateModel.IsTargetResourceOperatorEdited)
                                    {
                                        activity.TargetResourceOperator = updateModel.TargetResourceOperator;
                                    }
                                    if (updateModel.IsHasNoCostEdited)
                                    {
                                        activity.HasNoCost = updateModel.HasNoCost;
                                    }
                                    if (updateModel.IsHasNoBillingEdited)
                                    {
                                        activity.HasNoBilling = updateModel.HasNoBilling;
                                    }
                                    if (updateModel.IsHasNoEffortEdited)
                                    {
                                        activity.HasNoEffort = updateModel.HasNoEffort;
                                    }
                                    if (updateModel.IsHasNoRiskEdited)
                                    {
                                        activity.HasNoRisk = updateModel.HasNoRisk;
                                    }

                                    editable.EndEdit();
                                }
                            }
                        }
                    });

                    IsProjectPlanUpdated = true;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void AddMilestone(IEnumerable<int> dependentActivityIds)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;

                    if (!HasCompilationErrors)
                    {
                        // Check the upstream activities to be milestoned are all present.
                        IEnumerable<IManagedActivityViewModel> upstreamActivities = [.. RawActivities.Where(x => dependentActivityIds.Contains(x.Id))];
                        HashSet<int> upstreamActivityIds = [.. upstreamActivities.Select(x => x.Id)];

                        if (upstreamActivityIds.Count != 0)
                        {
                            // Create the milestone activity
                            int milestoneId = AddManagedActivity();

                            IManagedActivityViewModel? milestoneActivity = RawActivities
                                .Where(x => x.Id == milestoneId)
                                .FirstOrDefault();

                            if (milestoneActivity != null)
                            {
                                // Now go through all the downstream activities, whose dependencies
                                // contain the upstream activity IDs, and add the ID of the milestone.
                                // Be sure to exclude the upstream activities themselves to avoid
                                // circular dependencies.
                                IEnumerable<IManagedActivityViewModel> downstreamCompiledActivities = [.. RawActivities
                                    .Where(x => x.Dependencies.Intersect(upstreamActivityIds).Any())
                                    .Except(upstreamActivities)];

                                IEnumerable<IManagedActivityViewModel> downstreamPlanningActivities = [.. RawActivities
                                    .Where(x => x.PlanningDependencies.Intersect(upstreamActivityIds).Any())
                                    .Except(upstreamActivities)];

                                // Repopulate the selected downstream activities' dependencies.
                                // This time with the new milestone activity ID.
                                foreach (IManagedActivityViewModel downstreamActivity in downstreamCompiledActivities)
                                {
                                    m_VertexGraphCompiler.SetActivityDependencies(
                                        downstreamActivity.Id,
                                        [.. downstreamActivity.Dependencies, milestoneId],
                                        downstreamActivity.PlanningDependencies);
                                }

                                foreach (IManagedActivityViewModel downstreamActivity in downstreamPlanningActivities)
                                {
                                    m_VertexGraphCompiler.SetActivityDependencies(
                                        downstreamActivity.Id,
                                        downstreamActivity.Dependencies,
                                        [.. downstreamActivity.PlanningDependencies, milestoneId]);
                                }

                                // Finally, add the upstream activities' IDs as dependencies
                                // for the milestone activity.
                                m_VertexGraphCompiler.SetActivityDependencies(
                                    milestoneId, upstreamActivityIds, []);
                            }
                        }

                        IsProjectPlanUpdated = true;
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ClearManagedActivities()
        {
            try
            {
                lock (m_Lock)
                {
                    m_Activities.Edit(activities =>
                    {
                        IsBusy = true;

                        foreach (IManagedActivityViewModel activity in RawActivities)
                        {
                            activity.Dispose();
                        }
                        activities.Clear();

                        m_VertexGraphCompiler.Reset();
                    });
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void RunCompile()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;

                    var availableResources = new List<IResource<int, int>>();
                    if (!ResourceSettings.AreDisabled)
                    {
                        availableResources.AddRange(m_Mapper.Map<IEnumerable<ResourceModel>, IEnumerable<Resource<int, int>>>(ResourceSettings.Resources));
                    }

                    var workStreams = new List<IWorkStream<int>>();
                    workStreams.AddRange(m_Mapper.Map<IEnumerable<WorkStreamModel>, IEnumerable<WorkStream<int>>>(WorkStreamSettings.WorkStreams));

                    var graphCompilation = m_VertexGraphCompiler.Compile(availableResources, workStreams);
                    HasCompilationErrors = graphCompilation.CompilationErrors.Any();
                    GraphCompilation = graphCompilation;

                    IsProjectPlanUpdated = true;
                    HasStaleOutputs = false;
                    IsReadyToReviseTrackers = ReadyToRevise.No;
                    //IsReadyToReviseSettings = ReadyToRevise.No;
                    IsReadyToCompile = ReadyToCompile.No;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void RunAutoCompile()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    if (AutoCompile)
                    {
                        RunCompile();
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void RunTransitiveReduction()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    m_VertexGraphCompiler.Compile();
                    m_VertexGraphCompiler.TransitiveReduction();
                    RunCompile();
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void BuildArrowGraph()
        {
            lock (m_Lock)
            {
                ArrowGraph = new ArrowGraphModel();

                if (!HasCompilationErrors)
                {
                    IEnumerable<IDependentActivity> dependentActivities =
                        GraphCompilation.DependentActivities.Select(x => (IDependentActivity)x.CloneObject());

                    if (dependentActivities.Any())
                    {
                        var arrowGraphCompiler = new ArrowGraphCompiler();
                        foreach (IDependentActivity dependentActivity in dependentActivities)
                        {
                            dependentActivity.Dependencies.UnionWith(dependentActivity.ResourceDependencies);
                            dependentActivity.ResourceDependencies.Clear();
                            arrowGraphCompiler.AddActivity(dependentActivity);
                        }

                        arrowGraphCompiler.Compile();
                        Graph<int, IDependentActivity, IEvent<int>>? arrowGraph =
                            arrowGraphCompiler.ToGraph() ?? throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_CannotBuildArrowGraph);
                        ArrowGraph = m_Mapper.Map<Graph<int, IDependentActivity, IEvent<int>>, ArrowGraphModel>(arrowGraph);
                    }
                }
            }
        }

        public void BuildVertexGraph()
        {
            lock (m_Lock)
            {
                VertexGraph = new VertexGraphModel();

                if (!HasCompilationErrors)
                {
                    IEnumerable<IDependentActivity> dependentActivities =
                        GraphCompilation.DependentActivities.Select(x => (IDependentActivity)x.CloneObject());

                    if (dependentActivities.Any())
                    {
                        var availableResources = new List<IResource<int, int>>();
                        if (!ResourceSettings.AreDisabled)
                        {
                            availableResources.AddRange(m_Mapper.Map<IEnumerable<ResourceModel>, IEnumerable<Resource<int, int>>>(ResourceSettings.Resources));
                        }

                        var workStreams = new List<IWorkStream<int>>();
                        workStreams.AddRange(m_Mapper.Map<IEnumerable<WorkStreamModel>, IEnumerable<WorkStream<int>>>(WorkStreamSettings.WorkStreams));

                        var vertexGraphCompiler = new VertexGraphCompiler();
                        foreach (IDependentActivity dependentActivity in dependentActivities)
                        {
                            dependentActivity.Dependencies.UnionWith(dependentActivity.ResourceDependencies);
                            dependentActivity.ResourceDependencies.Clear();
                            vertexGraphCompiler.AddActivity(dependentActivity);
                        }

                        vertexGraphCompiler.TransitiveReduction();
                        vertexGraphCompiler.Compile(availableResources, workStreams);

                        Graph<int, IEvent<int>, IDependentActivity>? vertexGraph =
                            vertexGraphCompiler.ToGraph() ?? throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_CannotBuildArrowGraph);
                        VertexGraph = m_Mapper.Map<Graph<int, IEvent<int>, IDependentActivity>, VertexGraphModel>(vertexGraph);
                    }
                }
            }
        }

        public void BuildResourceSeriesSet()
        {
            lock (m_Lock)
            {
                var resourceSeriesSet = new ResourceSeriesSetModel();

                //if (!HasCompilationErrors)
                //{

                IList<ResourceScheduleModel> resourceScheduleModels =
                    m_Mapper.Map<IGraphCompilation<int, int, int, IDependentActivity>, IList<ResourceScheduleModel>>(GraphCompilation);

                resourceSeriesSet = CalculateResourceSeriesSet(
                    resourceScheduleModels,
                    ResourceSettings);
                //}

                ResourceSeriesSet = resourceSeriesSet;
            }
        }

        public void BuildTrackingSeriesSet()
        {
            lock (m_Lock)
            {
                var trackingSeriesSet = new TrackingSeriesSetModel();

                //if (!HasCompilationErrors)
                //{
                // TODO fix this mapping
                IList<ActivityModel> activityModels = m_Mapper.Map<List<ActivityModel>>(RawActivities);
                trackingSeriesSet = CalculateTrackingSeriesSet(activityModels, ResourceSettings, HasResources);
                //}

                TrackingSeriesSet = trackingSeriesSet;
            }
        }

        public void BuildNetworkMetrics()
        {
            lock (m_Lock)
            {
                int? cyclomaticComplexity = null;
                int? duration = null;
                double? durationManMonths = null;

                if (!HasCompilationErrors)
                {
                    if (!GraphCompilation.DependentActivities.Any())
                    {
                        return;
                    }

                    cyclomaticComplexity = CalculateCyclomaticComplexity(GraphCompilation.DependentActivities);
                    duration = m_VertexGraphCompiler.FinishTime - m_VertexGraphCompiler.StartTime;
                    durationManMonths = CalculateDurationManMonths(duration, m_DateTimeCalculator.DaysPerWeek);
                }

                NetworkMetrics = new NetworkModel
                {
                    CyclomaticComplexity = cyclomaticComplexity,
                    Duration = duration,
                    DurationManMonths = durationManMonths,
                };
                this.RaisePropertyChanged(nameof(Metrics));
            }
        }

        public void BuildRiskMetrics()
        {
            lock (m_Lock)
            {
                var risksModel = new RisksModel();

                List<IDependentActivity> dependentActivities =
                    [.. GraphCompilation.DependentActivities.Select(x => (IDependentActivity)x.CloneObject())];

                if (dependentActivities.Count != 0)
                {
                    if (!HasCompilationErrors)
                    {
                        IEnumerable<ActivityModel> activities =
                            m_Mapper.Map<IEnumerable<IActivity<int, int, int>>, IList<ActivityModel>>(
                                dependentActivities.Where(x => !x.IsDummy).Select(x => (IActivity<int, int, int>)x));

                        IEnumerable<ActivitySeverityModel> activitySeverities = GraphSettings.ActivitySeverities;

                        risksModel = MetricsHelper.CalculateProjectRisks(activities, activitySeverities);
                    }
                }

                RiskMetrics = risksModel;
                this.RaisePropertyChanged(nameof(Metrics));
            }
        }

        public void BuildFinancialMetrics()
        {

            lock (m_Lock)
            {
                var costsModel = new CostsModel();
                var billingsModel = new BillingsModel();
                var marginsModel = new MarginsModel();
                var effortsModel = new EffortsModel();

                List<ResourceSeriesModel> combinedResourceSeriesModels = ResourceSeriesSet.Combined;

                if (combinedResourceSeriesModels.Count != 0)
                {
                    if (!HasCompilationErrors)
                    {
                        costsModel = MetricsHelper.CalculateProjectCosts(combinedResourceSeriesModels);
                        billingsModel = MetricsHelper.CalculateProjectBillings(combinedResourceSeriesModels);
                        marginsModel = MetricsHelper.CalculateProjectMargins(costsModel, billingsModel);
                        effortsModel = MetricsHelper.CalculateProjectEfforts(combinedResourceSeriesModels);
                    }
                }

                CostMetrics = costsModel;
                BillingMetrics = billingsModel;
                MarginMetrics = marginsModel;
                EffortMetrics = effortsModel;
                this.RaisePropertyChanged(nameof(Metrics));
            }
        }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
            m_NetworkMetricsSub?.Dispose();
            m_AreActivitiesUncompiledSub?.Dispose();
            m_CompileOnSettingsUpdateSub?.Dispose();
            m_BuildArrowGraphSub?.Dispose();
            m_BuildVertexGraphSub?.Dispose();
            m_BuildResourceSeriesSetSub?.Dispose();
            m_BuildTrackingSeriesSetSub?.Dispose();
            m_BuildRiskMetricsSub?.Dispose();
            m_BuildFinancialMetricsSub?.Dispose();
        }

        #endregion

        #region IDisposable Members

        private bool m_Disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (m_Disposed)
            {
                return;
            }

            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
                KillSubscriptions();
                m_HasActivities?.Dispose();
                m_HasResources?.Dispose();
                m_HasWorkStreams?.Dispose();
                m_HasPhases?.Dispose();
                ClearManagedActivities();
                m_Activities?.Dispose();
                m_DisplaySettingsViewModel?.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

            m_Disposed = true;
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
