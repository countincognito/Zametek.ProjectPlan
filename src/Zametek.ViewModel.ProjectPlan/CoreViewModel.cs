using AutoMapper;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Text;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class CoreViewModel
        : ViewModelBase, ICoreViewModel, IDisposable
    {
        #region Fields

        private readonly object m_Lock;

        private readonly VertexGraphCompiler<int, int, IDependentActivity<int, int>> m_VertexGraphCompiler;

        private readonly ISettingService m_SettingService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly IMapper m_Mapper;

        private readonly IDisposable? m_CyclomaticComplexitySub;
        private readonly IDisposable? m_AreActivitiesUncompiledSub;
        private readonly IDisposable? m_CompileOnSettingsUpdateSub;
        private readonly IDisposable? m_BuildArrowGraphSub;
        private readonly IDisposable? m_BuildResourceSeriesSetSub;
        private readonly IDisposable? m_BuildTrackingSeriesSetSub;

        #endregion

        #region Ctors

        public CoreViewModel(
            ISettingService settingService,
            IDateTimeCalculator dateTimeCalculator,
            IMapper mapper)
        {
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(mapper);
            m_Lock = new object();
            m_VertexGraphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            m_SettingService = settingService;
            m_DateTimeCalculator = dateTimeCalculator;
            m_Mapper = mapper;

            m_IsBusy = false;
            m_HasStaleOutputs = false;
            m_ProjectStart = new DateTimeOffset(DateTime.Today);
            m_ResourceSettings = new ResourceSettingsModel();
            m_Activities = [];
            m_ReadOnlyActivities = new ReadOnlyObservableCollection<IManagedActivityViewModel>(m_Activities);
            m_ArrowGraphSettings = m_SettingService.DefaultArrowGraphSettings;
            m_ResourceSettings = m_SettingService.DefaultResourceSettings;
            m_GraphCompilation = new GraphCompilation<int, int, DependentActivity<int, int>>(
                Enumerable.Empty<DependentActivity<int, int>>(),
                Enumerable.Empty<IResourceSchedule<int, int>>());
            m_ArrowGraph = new ArrowGraphModel();
            m_ResourceSeriesSet = new ResourceSeriesSetModel();
            m_TrackingSeriesSet = new TrackingSeriesSetModel();

            m_ProjectTitle = this
                .WhenAnyValue(core => core.m_SettingService.ProjectTitle)
                .ToProperty(this, core => core.ProjectTitle);

            m_HasCompilationErrors = this
                .WhenAnyValue(
                    core => core.GraphCompilation,
                    compilation => compilation.CompilationErrors.Any())
                .ToProperty(this, core => core.HasCompilationErrors);

            m_CyclomaticComplexitySub = this
                .WhenAnyValue(core => core.GraphCompilation)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildCyclomaticComplexity());

            m_Duration = this
                .WhenAnyValue(
                    core => core.HasCompilationErrors,
                    hasCompilationErrors => hasCompilationErrors ? (int?)null : m_VertexGraphCompiler.Duration)
                .ToProperty(this, core => core.Duration);

            m_AreActivitiesUncompiledSub = m_ReadOnlyActivities
                .ToObservableChangeSet()
                .AutoRefresh(activity => activity.IsCompiled) // Subscribe only to IsCompiled property changes
                .Filter(activity => !activity.IsCompiled)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(changeSet =>
                {
                    if (!IsBusy && changeSet.TotalChanges > 0)
                    {
                        lock (m_Lock)
                        {
                            if (!IsBusy && changeSet.TotalChanges > 0) // Replaced only counts the individually updated items.
                            {
                                RunAutoCompile();
                            }
                        }
                    }
                });

            m_CompileOnSettingsUpdateSub = this
                .WhenAnyValue(
                    core => core.ProjectStart,
                    core => core.ResourceSettings,
                    core => core.ArrowGraphSettings,
                    core => core.UseBusinessDays)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ =>
                {
                    if (!IsBusy)
                    {
                        lock (m_Lock)
                        {
                            if (!IsBusy)
                            {
                                RunAutoCompile();
                            }
                        }
                    }
                });

            m_BuildArrowGraphSub = this
                .WhenAnyValue(core => core.GraphCompilation)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildArrowGraph());

            m_BuildResourceSeriesSetSub = this
                .WhenAnyValue(
                    core => core.GraphCompilation)//,
                                                  //core => core.ResourceSettings)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildResourceSeriesSet());

            m_BuildTrackingSeriesSetSub = this
                .WhenAnyValue(core => core.GraphCompilation)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => BuildTrackingSeriesSet());
        }

        #endregion

        #region Private Methods

        private void BuildArrowGraph()
        {
            lock (m_Lock)
            {
                ArrowGraph = new ArrowGraphModel();

                if (!HasCompilationErrors)
                {
                    IEnumerable<IDependentActivity<int, int>> dependentActivities =
                        GraphCompilation.DependentActivities.Select(x => (IDependentActivity<int, int>)x.CloneObject());

                    if (dependentActivities.Any())
                    {
                        var arrowGraphCompiler = new ArrowGraphCompiler<int, int, IDependentActivity<int, int>>();
                        foreach (IDependentActivity<int, int> dependentActivity in dependentActivities)
                        {
                            dependentActivity.Dependencies.UnionWith(dependentActivity.ResourceDependencies);
                            dependentActivity.ResourceDependencies.Clear();
                            arrowGraphCompiler.AddActivity(dependentActivity);
                        }

                        arrowGraphCompiler.Compile();
                        Graph<int, IDependentActivity<int, int>, IEvent<int>>? arrowGraph =
                            arrowGraphCompiler.ToGraph() ?? throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_CannotBuildArrowGraph);
                        ArrowGraph = m_Mapper.Map<Graph<int, IDependentActivity<int, int>, IEvent<int>>, ArrowGraphModel>(arrowGraph);
                    }
                }
            }
        }

        private static ResourceSeriesSetModel CalculateResourceSeriesSet(
            IEnumerable<ResourceScheduleModel> resourceSchedules,
            IEnumerable<ResourceModel> resources,
            double defaultUnitCost)
        {
            ArgumentNullException.ThrowIfNull(resourceSchedules);
            ArgumentNullException.ThrowIfNull(resources);
            var resourceSeriesSet = new ResourceSeriesSetModel();
            var resourceLookup = resources.ToDictionary(x => x.Id);

            if (resourceSchedules.Any())
            {
                Dictionary<int, ColorFormatModel> colorFormatLookup = resources.ToDictionary(x => x.Id, x => x.ColorFormat);
                int finishTime = resourceSchedules.Select(x => x.FinishTime).DefaultIfEmpty().Max();
                int spareResourceCount = 1;

                // Scheduled resource series.
                // These are the series that apply to scheduled activities (whether allocated to named or unnamed resources).
                var scheduledSeriesSet = new List<ResourceSeriesModel>();

                foreach (ResourceScheduleModel resourceSchedule in resourceSchedules)
                {
                    var stringBuilder = new StringBuilder();
                    InterActivityAllocationType interActivityAllocationType = InterActivityAllocationType.None;
                    ColorFormatModel color = ColorHelper.RandomColor();
                    double unitCost = defaultUnitCost;
                    int displayOrder = 0;

                    if (resourceSchedule.Resource.Id != default
                        && resourceLookup.TryGetValue(resourceSchedule.Resource.Id, out ResourceModel? resource))
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
                        DisplayOrder = displayOrder,
                        ResourceSchedule = resourceSchedule,
                        InterActivityAllocationType = interActivityAllocationType,
                    };

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
                        ResourceSchedule = new ResourceScheduleModel
                        {
                            Resource = resource,
                            ActivityAllocation = new List<bool>(Enumerable.Repeat(true, finishTime)),
                            FinishTime = finishTime
                        },
                        ColorFormat = resource.ColorFormat != null ? resource.ColorFormat.CloneObject() : ColorHelper.RandomColor(),
                        UnitCost = resource.UnitCost,
                        DisplayOrder = resource.DisplayOrder,
                    };

                    unscheduledSeriesSet.Add(series);
                    unscheduledResourceSeriesLookup.Add(resourceId, series);
                }

                // Combined resource series.
                // The intersection of the scheduled and unscheduled series.
                var combinedScheduled = new List<ResourceSeriesModel>();
                var unscheduledSeriesAlreadyIncluded = new HashSet<int>();

                foreach (ResourceSeriesModel scheduledSeries in scheduledSeriesSet)
                {
                    IList<bool> values = new List<bool>(Enumerable.Repeat(false, finishTime));
                    if (scheduledSeries.ResourceSchedule.Resource.Id != default)
                    {
                        int resourceId = scheduledSeries.ResourceSchedule.Resource.Id;
                        if (unscheduledResourceSeriesLookup.TryGetValue(resourceId, out ResourceSeriesModel? unscheduledResourceSeries))
                        {
                            values = scheduledSeries.ResourceSchedule.ActivityAllocation.Zip(unscheduledResourceSeries.ResourceSchedule.ActivityAllocation, (x, y) => x || y).ToList();
                            unscheduledSeriesAlreadyIncluded.Add(resourceId);
                        }
                        else
                        {
                            values = scheduledSeries.ResourceSchedule.ActivityAllocation.ToList();
                        }
                    }
                    else
                    {
                        values = scheduledSeries.ResourceSchedule.ActivityAllocation.ToList();
                    }

                    scheduledSeries.ResourceSchedule.ActivityAllocation.Clear();
                    scheduledSeries.ResourceSchedule.ActivityAllocation.AddRange(values);
                    combinedScheduled.Add(scheduledSeries);
                }

                // Finally, add the unscheduled series that have not already been included above.

                // Prepend so that they might be displayed first after sorting.
                List<ResourceSeriesModel> combined = unscheduledSeriesSet
                    .Where(x => !unscheduledSeriesAlreadyIncluded.Contains(x.ResourceSchedule.Resource.Id))
                    .ToList();

                combined.AddRange(combinedScheduled);

                resourceSeriesSet.ResourceSchedules.AddRange(resourceSchedules);
                resourceSeriesSet.Scheduled.AddRange(scheduledSeriesSet);
                resourceSeriesSet.Unscheduled.AddRange(unscheduledSeriesSet);
                resourceSeriesSet.Combined.AddRange(combined.OrderBy(x => x.DisplayOrder));
            }

            return resourceSeriesSet;
        }

        private void BuildResourceSeriesSet()
        {
            lock (m_Lock)
            {
                var resourceSeriesSet = new ResourceSeriesSetModel();

                if (!HasCompilationErrors)
                {
                    IList<ResourceModel> resourceModels = ResourceSettings.Resources;

                    IList<ResourceScheduleModel> resourceScheduleModels =
                        m_Mapper.Map<IEnumerable<IResourceSchedule<int, int>>, IList<ResourceScheduleModel>>(GraphCompilation.ResourceSchedules);

                    resourceSeriesSet = CalculateResourceSeriesSet(
                        resourceScheduleModels,
                        resourceModels,
                        ResourceSettings.DefaultUnitCost);
                }

                ResourceSeriesSet = resourceSeriesSet;
            }
        }

        private static TrackingSeriesSetModel CalculateTrackingSeriesSet(IEnumerable<ActivityModel> activities)
        {
            ArgumentNullException.ThrowIfNull(activities);
            IList<ActivityModel> orderedActivities = activities
                .Select(x => x.CloneObject())
                .OrderBy(x => x.EarliestFinishTime.GetValueOrDefault())
                .ThenBy(x => x.EarliestStartTime.GetValueOrDefault())
                .ToList();

            // Plan.
            List<TrackingPointModel> planPointSeries =
            [
                // Starting point.
                new TrackingPointModel()
            ];

            // Plan Projection.
            List<TrackingPointModel> planProjectionPointSeries =
            [
                // Starting point.
                new TrackingPointModel()
            ];

            // Progress.
            List<TrackingPointModel> progressPointSeries =
            [
                new TrackingPointModel()
            ];

            // Progress Projection.
            List<TrackingPointModel> progressProjectionPointSeries =
            [
                new TrackingPointModel()
            ];

            // Effort.
            List<TrackingPointModel> effortPointSeries =
            [
                new TrackingPointModel()
            ];

            // Effort Projection.
            List<TrackingPointModel> effortProjectionPointSeries =
            [
                new TrackingPointModel()
            ];

            if (orderedActivities.Any())
            {
                double totalTime = Convert.ToDouble(orderedActivities.Sum(s => s.Duration));

                // Plan.
                if (orderedActivities.All(x => x.EarliestFinishTime.HasValue))
                {
                    int runningTotalTime = 0;
                    foreach (ActivityModel activity in orderedActivities)
                    {
                        int time = activity.EarliestFinishTime.GetValueOrDefault();
                        runningTotalTime += activity.Duration;
                        double percentage = totalTime == 0 ? 0.0 : 100.0 * runningTotalTime / totalTime;
                        planPointSeries.Add(new TrackingPointModel
                        {
                            Time = time,
                            ActivityId = activity.Id,
                            ActivityName = activity.Name,
                            Value = runningTotalTime,
                            ValuePercentage = percentage
                        });
                    }
                }

                // Progress and Effort.
                int runningEffort = 0;

                for (int timeIndex = 0; timeIndex < totalTime; timeIndex++)
                {
                    // Calculate percentage progress at each time index.
                    double timeIndexRunningProgress = 0.0;
                    bool includePoints = false;
                    var includedActivities = new List<ActivityModel>();

                    foreach (ActivityModel activity in orderedActivities)
                    {
                        if (timeIndex < activity.Trackers.Count)
                        {
                            var tracker = activity.Trackers[timeIndex];

                            Debug.Assert(tracker.Index == timeIndex);
                            Debug.Assert(tracker.Time == timeIndex);

                            timeIndexRunningProgress += activity.Duration * (tracker.PercentageComplete / 100.0);

                            if (tracker.IsIncluded)
                            {
                                runningEffort++;
                                includedActivities.Add(activity);
                            }

                            includePoints = true;
                        }
                    }

                    if (includePoints)
                    {
                        double progressPercentage = totalTime == 0 ? 0.0 : 100.0 * timeIndexRunningProgress / totalTime;
                        double effortPercentage = totalTime == 0 ? 0.0 : 100.0 * runningEffort / totalTime;
                        int time = timeIndex + 1; // Since the equivalent finish time would be the next day.

                        foreach (ActivityModel includedActivity in includedActivities.OrderBy(x => x.Id))
                        {
                            progressPointSeries.Add(new TrackingPointModel
                            {
                                Time = time,
                                ActivityId = includedActivity.Id,
                                ActivityName = includedActivity.Name,
                                Value = timeIndexRunningProgress,
                                ValuePercentage = progressPercentage
                            });

                            effortPointSeries.Add(new TrackingPointModel
                            {
                                Time = time,
                                ActivityId = includedActivity.Id,
                                ActivityName = includedActivity.Name,
                                Value = runningEffort,
                                ValuePercentage = effortPercentage
                            });
                        }

                    }
                }
            }

            // Projections.

            // Each series will always have at least one item.
            planProjectionPointSeries.Add(planPointSeries.Last());

            if (progressPointSeries.Count > 1)
            {
                var projectedLinearFit = MathNet.Numerics.Fit.LineThroughOrigin(
                    progressPointSeries.Select(p => (double)p.Time).ToArray(),
                    progressPointSeries.Select(p => p.Value).ToArray());

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

            if (effortPointSeries.Count > 1)
            {
                // We want to project effort out to the greater of the plan or the progress projection
                var projectedCompletion = Math.Max(
                    progressProjectionPointSeries.Last().Time,
                    planProjectionPointSeries.Last().Time);

                var projectedLinearFit = MathNet.Numerics.Fit.LineThroughOrigin(
                    effortPointSeries.Select(p => (double)p.Time).ToArray(),
                    effortPointSeries.Select(p => p.Value).ToArray());

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

            var trackingSeriesSet = new TrackingSeriesSetModel
            {
                Plan = planPointSeries,
                PlanProjection = planProjectionPointSeries,
                Progress = progressPointSeries,
                ProgressProjection = progressProjectionPointSeries,
                Effort = effortPointSeries,
                EffortProjection = effortProjectionPointSeries
            };
            return trackingSeriesSet;
        }

        private void BuildTrackingSeriesSet()
        {
            lock (m_Lock)
            {
                var trackingSeriesSet = new TrackingSeriesSetModel();

                if (!HasCompilationErrors)
                {
                    IList<ActivityModel> activityModels = m_Mapper.Map<List<ActivityModel>>(Activities);
                    trackingSeriesSet = CalculateTrackingSeriesSet(activityModels);
                }

                TrackingSeriesSet = trackingSeriesSet;
            }
        }

        private static int CalculateCyclomaticComplexity(IEnumerable<IDependentActivity<int, int>> dependentActivities)
        {
            ArgumentNullException.ThrowIfNull(dependentActivities);
            var vertexGraphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            foreach (var dependentActivity in dependentActivities.Cast<DependentActivity<int, int>>())
            {
                dependentActivity.ResourceDependencies.Clear();
                vertexGraphCompiler.AddActivity(dependentActivity);
            }
            vertexGraphCompiler.TransitiveReduction();
            return vertexGraphCompiler.CyclomaticComplexity;
        }

        private void BuildCyclomaticComplexity()
        {
            lock (m_Lock)
            {
                CyclomaticComplexity = null;

                if (!HasCompilationErrors)
                {
                    IEnumerable<IDependentActivity<int, int>> dependentActivities =
                        GraphCompilation.DependentActivities.Select(x => (IDependentActivity<int, int>)x.CloneObject());

                    if (!dependentActivities.Any())
                    {
                        return;
                    }

                    CyclomaticComplexity = CalculateCyclomaticComplexity(dependentActivities);
                }
            }
        }

        #endregion

        #region ICoreViewModel Members

        private readonly ObservableAsPropertyHelper<string> m_ProjectTitle;
        public string ProjectTitle
        {
            get => m_ProjectTitle.Value;
            set
            {
                lock (m_Lock) m_SettingService.SetTitle(value);
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

        private bool m_IsProjectUpdated;
        public bool IsProjectUpdated
        {
            get => m_IsProjectUpdated;
            set
            {
                lock (m_Lock)
                {
                    HasStaleOutputs = value;
                    this.RaiseAndSetIfChanged(ref m_IsProjectUpdated, value);
                }
            }
        }

        private bool m_HasStaleOutputs;
        public bool HasStaleOutputs
        {
            get => m_HasStaleOutputs;
            set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_HasStaleOutputs, value);
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
                    IsProjectUpdated = true;
                    this.RaiseAndSetIfChanged(ref m_ProjectStart, value);
                    this.RaisePropertyChanged(nameof(ProjectStartDateTime));
                    this.RaisePropertyChanged(nameof(ProjectStartTimeOffset));
                }
            }
        }

        public DateTime ProjectStartDateTime
        {
            get => m_ProjectStart.DateTime;
            set
            {
                lock (m_Lock)
                {
                    ProjectStart = new DateTimeOffset(value, ProjectStartTimeOffset);
                }
            }
        }

        public TimeSpan ProjectStartTimeOffset
        {
            get => m_ProjectStart.Offset;
            set
            {
                lock (m_Lock)
                {
                    ProjectStart = new DateTimeOffset(ProjectStartDateTime, value);
                }
            }
        }

        private bool m_ShowDates;
        public bool ShowDates
        {
            get => m_ShowDates;
            set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_ShowDates, value);
            }
        }

        private bool m_UseBusinessDays;
        public bool UseBusinessDays
        {
            get => m_UseBusinessDays;
            set
            {
                lock (m_Lock)
                {
                    m_UseBusinessDays = value;
                    if (m_UseBusinessDays)
                    {
                        m_DateTimeCalculator.Mode = DateTimeCalculatorMode.BusinessDays;
                    }
                    else
                    {
                        m_DateTimeCalculator.Mode = DateTimeCalculatorMode.AllDays;
                    }
                    IsProjectUpdated = true;
                    this.RaisePropertyChanged();
                }
            }
        }

        private bool m_ViewEarnedValueProjections;
        public bool ViewEarnedValueProjections
        {
            get => m_ViewEarnedValueProjections;
            set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_ViewEarnedValueProjections, value);
            }
        }

        private bool m_AutoCompile;
        public bool AutoCompile
        {
            get => m_AutoCompile;
            set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_AutoCompile, value);
            }
        }

        private readonly ObservableCollection<IManagedActivityViewModel> m_Activities;
        private readonly ReadOnlyObservableCollection<IManagedActivityViewModel> m_ReadOnlyActivities;
        public ReadOnlyObservableCollection<IManagedActivityViewModel> Activities => m_ReadOnlyActivities;

        private ArrowGraphSettingsModel m_ArrowGraphSettings;
        public ArrowGraphSettingsModel ArrowGraphSettings
        {
            get => m_ArrowGraphSettings;
            set
            {
                lock (m_Lock)
                {
                    m_ArrowGraphSettings = value;
                    IsProjectUpdated = true;
                    this.RaisePropertyChanged();
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
                    IsProjectUpdated = true;
                    this.RaisePropertyChanged();
                }
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        private IGraphCompilation<int, int, IDependentActivity<int, int>> m_GraphCompilation;
        public IGraphCompilation<int, int, IDependentActivity<int, int>> GraphCompilation
        {
            get => m_GraphCompilation;
            private set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_GraphCompilation, value);
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

        private int? m_CyclomaticComplexity;
        public int? CyclomaticComplexity
        {
            get => m_CyclomaticComplexity;
            private set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_CyclomaticComplexity, value);
            }
        }

        private readonly ObservableAsPropertyHelper<int?> m_Duration;
        public int? Duration => m_Duration.Value;

        public void ClearSettings()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    ArrowGraphSettings = m_SettingService.DefaultArrowGraphSettings;
                    ResourceSettings = m_SettingService.DefaultResourceSettings;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ResetProject()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    ClearManagedActivities();

                    ClearSettings();

                    GraphCompilation = new GraphCompilation<int, int, DependentActivity<int, int>>(
                        Enumerable.Empty<DependentActivity<int, int>>(),
                        Enumerable.Empty<IResourceSchedule<int, int>>());

                    ArrowGraph = new ArrowGraphModel();

                    IsProjectUpdated = false;
                    HasStaleOutputs = false;

                    m_SettingService.Reset();
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ProcessProjectImport(ProjectImportModel projectImportModel)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    ResetProject();

                    // Project Start Date.
                    ProjectStart = projectImportModel.ProjectStart;

                    // Resources.
                    ResourceSettingsModel resourceSettings = m_SettingService.DefaultResourceSettings.CloneObject();
                    resourceSettings = resourceSettings with { DefaultUnitCost = projectImportModel.DefaultUnitCost };

                    if (projectImportModel.Resources.Count != 0)
                    {
                        resourceSettings.Resources.Clear();

                        foreach (ResourceModel resource in projectImportModel.Resources)
                        {
                            resourceSettings.Resources.Add(resource);
                        }
                    }

                    ResourceSettings = resourceSettings;

                    // Arrow graph settings.
                    ArrowGraphSettingsModel arrowGraphSettings = m_SettingService.DefaultArrowGraphSettings.CloneObject();

                    if (projectImportModel.ActivitySeverities.Count != 0)
                    {
                        arrowGraphSettings.ActivitySeverities.Clear();

                        foreach (ActivitySeverityModel? activitySeverity in projectImportModel.ActivitySeverities)
                        {
                            arrowGraphSettings.ActivitySeverities.Add(activitySeverity);
                        }
                    }

                    ArrowGraphSettings = arrowGraphSettings;

                    // Activities.
                    // Be sure to set the ResourceSettings first, so that the activities know
                    // which resources are being referred to when marking them as selected.
                    AddManagedActivities(projectImportModel.DependentActivities);

                    IsProjectUpdated = false;
                    HasStaleOutputs = true;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ProcessProjectPlan(ProjectPlanModel projectPlanModel)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    ResetProject();

                    // Project Start Date.
                    ProjectStart = projectPlanModel.ProjectStart;

                    // Resource Settings.
                    ResourceSettings = projectPlanModel.ResourceSettings;

                    // Arrow Graph Settings.
                    ArrowGraphSettings = projectPlanModel.ArrowGraphSettings;

                    // Compilation.
                    GraphCompilation = m_Mapper.Map<GraphCompilation<int, int, DependentActivity<int, int>>>(projectPlanModel.GraphCompilation);

                    // Activities.
                    AddManagedActivities(new HashSet<DependentActivityModel>(projectPlanModel.DependentActivities));

                    // Arrow Graph.
                    ArrowGraph = projectPlanModel.ArrowGraph;

                    IsProjectUpdated = false;
                    HasStaleOutputs = projectPlanModel.HasStaleOutputs;
                }
            }
            finally
            {
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
                    var graphCompilation = m_Mapper.Map<IGraphCompilation<int, int, IDependentActivity<int, int>>, GraphCompilationModel>(GraphCompilation);

                    return new ProjectPlanModel
                    {
                        Version = Data.ProjectPlan.Versions.v0_3_0,
                        ProjectStart = ProjectStart,
                        DependentActivities = m_Mapper.Map<List<DependentActivityModel>>(Activities),
                        ResourceSettings = ResourceSettings.CloneObject(),
                        ArrowGraphSettings = ArrowGraphSettings.CloneObject(),
                        GraphCompilation = graphCompilation,
                        ArrowGraph = ArrowGraph.CloneObject(),
                        HasStaleOutputs = HasStaleOutputs
                    };
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void AddManagedActivity()
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
                    IsBusy = true;
                    // Check that the number of trackers across each activity is consistent.
                    // Make them match the highest number.
                    int maxCurrentTrackers = Activities
                        .Select(x => x.Trackers.Count).DefaultIfEmpty().Max();

                    int maxNewTrackers = dependentActivityModels
                        .Select(x => x.Activity.Trackers.Count).DefaultIfEmpty().Max();

                    int maxTrackers = Math.Max(maxCurrentTrackers, maxNewTrackers);

                    foreach (DependentActivityModel dependentActivity in dependentActivityModels)
                    {
                        int trackerDifference = maxTrackers - dependentActivity.Activity.Trackers.Count;

                        if (trackerDifference > 0)
                        {
                            dependentActivity.Activity.Trackers.AddRange(Enumerable.Repeat(new TrackerModel(), trackerDifference));
                        }

                        var activity = new ManagedActivityViewModel(
                            this,
                            m_Mapper.Map<DependentActivityModel, DependentActivity<int, int>>(dependentActivity),
                            m_DateTimeCalculator,
                            m_VertexGraphCompiler,
                            ProjectStart,
                            dependentActivity.Activity.Trackers,
                            dependentActivity.Activity.MinimumEarliestStartDateTime,
                            dependentActivity.Activity.MaximumLatestFinishDateTime);

                        if (m_VertexGraphCompiler.AddActivity(activity))
                        {
                            m_Activities.Add(activity);
                        }
                        else
                        {
                            activity.Dispose();
                        }
                    }

                    IsProjectUpdated = true;
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
                    IsBusy = true;
                    IEnumerable<IManagedActivityViewModel> dependentActivities = Activities
                        .Where(x => dependentActivityIds.Contains(x.Id))
                        .ToList();

                    foreach (IManagedActivityViewModel dependentActivity in dependentActivities)
                    {
                        if (m_VertexGraphCompiler.RemoveActivity(dependentActivity.Id))
                        {
                            m_Activities.Remove(dependentActivity);
                            dependentActivity.Dispose();
                        }
                    }

                    IsProjectUpdated = true;
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
                    IsBusy = true;
                    foreach (IDisposable activity in Activities)
                    {
                        activity.Dispose();
                    }
                    m_Activities.Clear();
                    m_VertexGraphCompiler.Reset();
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void AddTrackers()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    foreach (IManagedActivityViewModel activity in Activities)
                    {
                        activity.AddTracker();
                    }
                    IsProjectUpdated = true;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void RemoveTrackers()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    foreach (IManagedActivityViewModel activity in Activities)
                    {
                        activity.RemoveTracker();
                    }
                    IsProjectUpdated = true;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ReviseTrackers()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    foreach (IManagedActivityViewModel activity in Activities)
                    {
                        int runningPercentageCompleted = 0;
                        bool activeUpdate = false;

                        foreach (ITrackerViewModel tracker in activity.Trackers)
                        {
                            if (tracker.IsUpdated)
                            {
                                if (tracker.PercentageComplete >= runningPercentageCompleted)
                                {
                                    activeUpdate = true;
                                    runningPercentageCompleted = tracker.PercentageComplete;
                                }
                            }

                            if (activeUpdate || tracker.PercentageComplete < runningPercentageCompleted)
                            {
                                tracker.PercentageComplete = runningPercentageCompleted;
                            }

                            tracker.IsUpdated = false;
                            runningPercentageCompleted = tracker.PercentageComplete;
                        }
                    }
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
                    ReviseTrackers();

                    var availableResources = new List<IResource<int>>();
                    if (!ResourceSettings.AreDisabled)
                    {
                        availableResources.AddRange(m_Mapper.Map<IEnumerable<ResourceModel>, IEnumerable<Resource<int>>>(ResourceSettings.Resources));
                    }

                    GraphCompilation = m_VertexGraphCompiler.Compile(availableResources);
                    IsProjectUpdated = true;
                    HasStaleOutputs = false;
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
                    m_VertexGraphCompiler.Compile(new List<IResource<int>>());
                    m_VertexGraphCompiler.TransitiveReduction();
                    RunCompile();
                }
            }
            finally
            {
                IsBusy = false;
            }
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
                m_CyclomaticComplexitySub?.Dispose();
                m_AreActivitiesUncompiledSub?.Dispose();
                m_CompileOnSettingsUpdateSub?.Dispose();
                m_BuildArrowGraphSub?.Dispose();
                m_BuildResourceSeriesSetSub?.Dispose();
                m_BuildTrackingSeriesSetSub?.Dispose();
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
