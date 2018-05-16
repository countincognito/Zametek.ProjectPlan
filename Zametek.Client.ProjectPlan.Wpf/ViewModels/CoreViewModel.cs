using Prism.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Zametek.Common.Project;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class CoreViewModel
        : PropertyChangedPubSubViewModel, ICoreViewModel
    {
        #region Fields

        private readonly object m_Lock;
        private readonly IProjectManager m_ProjectManager;
        private readonly ISettingManager m_SettingManager;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly VertexGraphCompiler<int, IDependentActivity<int>> m_VertexGraphCompiler;
        private DateTime m_ProjectStart;
        private bool m_IsProjectUpdated;
        private bool m_ShowDates;
        private bool m_UseBusinessDays;
        private bool m_HasStaleOutputs;
        private bool m_AutoCompile;
        private bool m_HasCompilationErrors;
        private GraphCompilation<int, IDependentActivity<int>> m_GraphCompilation;
        private string m_CompilationOutput;
        private ArrowGraphDto m_ArrowGraphDto;
        private ArrowGraphSettingsDto m_ArrowGraphSettingsDto;
        private ResourceSettingsDto m_ResourceSettingsDto;
        private int? m_CyclomaticComplexity;
        private int? m_Duration;
        private double? m_DirectCost;
        private double? m_IndirectCost;
        private double? m_OtherCost;
        private double? m_TotalCost;

        private readonly IEventAggregator m_EventService;

        #endregion

        #region Ctors

        public CoreViewModel(
            IProjectManager projectManager,
            ISettingManager settingManager,
            IDateTimeCalculator dateTimeCalculator,
            IEventAggregator eventService)
            : base(eventService)
        {
            m_Lock = new object();
            m_ProjectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
            m_SettingManager = settingManager ?? throw new ArgumentNullException(nameof(settingManager));
            m_DateTimeCalculator = dateTimeCalculator ?? throw new ArgumentNullException(nameof(dateTimeCalculator));
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));

            m_VertexGraphCompiler = VertexGraphCompiler<int, IDependentActivity<int>>.Create();
            Activities = new ObservableCollection<ManagedActivityViewModel>();
            ResourceSeriesSet = new List<ResourceSeriesDto>();
            ClearSettings();
        }

        #endregion

        #region Private Methods

        private void PublishGraphCompiledPayload()
        {
            m_EventService.GetEvent<PubSubEvent<GraphCompiledPayload>>()
                .Publish(new GraphCompiledPayload());
        }

        private void PublishGraphCompilationUpdatedPayload()
        {
            m_EventService.GetEvent<PubSubEvent<GraphCompilationUpdatedPayload>>()
                .Publish(new GraphCompilationUpdatedPayload());
        }

        private string BuildCircularDependenciesErrorMessage(IList<CircularDependency<int>> circularDependencies)
        {
            if (circularDependencies == null)
            {
                return string.Empty;
            }
            var output = new StringBuilder();
            output.AppendLine($@">{Properties.Resources.Message_CircularDependencies}");
            foreach (CircularDependency<int> circularDependency in circularDependencies)
            {
                output.AppendLine(string.Join(@" -> ", circularDependency.Dependencies));
            }
            return output.ToString();
        }

        private string BuildMissingDependenciesErrorMessage(IList<int> missingDependencies)
        {
            if (missingDependencies == null)
            {
                return string.Empty;
            }
            var output = new StringBuilder();
            output.AppendLine($@">{Properties.Resources.Message_MissingDependencies}");
            foreach (int missingDependency in missingDependencies)
            {
                IList<int> activities = Activities
                    .Where(x => x.Dependencies.Contains(missingDependency))
                    .Select(x => x.Id)
                    .ToList();
                output.AppendFormat($@"{missingDependency} -> ");
                output.AppendLine(string.Join(@", ", activities));
            }
            return output.ToString();
        }

        private string BuildActivitySchedules(IList<IResourceSchedule<int>> resourceSchedules)
        {
            lock (m_Lock)
            {
                if (resourceSchedules == null)
                {
                    return string.Empty;
                }
                var output = new StringBuilder();
                int spareResourceCount = 1;
                for (int resourceIndex = 0; resourceIndex < resourceSchedules.Count; resourceIndex++)
                {
                    IResourceSchedule<int> resourceSchedule = resourceSchedules[resourceIndex];
                    IList<IScheduledActivity<int>> scheduledActivities = resourceSchedule?.ScheduledActivities;
                    if (scheduledActivities == null)
                    {
                        continue;
                    }
                    var stringBuilder = new StringBuilder(@">Resource");
                    if (resourceSchedule.Resource != null)
                    {
                        stringBuilder.Append($@" {resourceSchedule.Resource.Id}");
                        if (!string.IsNullOrWhiteSpace(resourceSchedule.Resource.Name))
                        {
                            stringBuilder.Append($@" ({resourceSchedule.Resource.Name})");
                        }
                    }
                    else
                    {
                        stringBuilder.Append($@" {spareResourceCount}");
                        spareResourceCount++;
                    }
                    output.AppendLine(stringBuilder.ToString());
                    int previousFinishTime = 0;
                    foreach (IScheduledActivity<int> scheduledActivity in scheduledActivities)
                    {
                        int startTime = scheduledActivity.StartTime;
                        int finishTime = scheduledActivity.FinishTime;
                        if (startTime > previousFinishTime)
                        {
                            string from = ChartHelper.FormatScheduleOutput(previousFinishTime, ShowDates, ProjectStart, m_DateTimeCalculator);
                            string to = ChartHelper.FormatScheduleOutput(startTime, ShowDates, ProjectStart, m_DateTimeCalculator);
                            output.AppendLine($@"*** {from} -> {to} ***");
                        }
                        string start = ChartHelper.FormatScheduleOutput(startTime, ShowDates, ProjectStart, m_DateTimeCalculator);
                        string finish = ChartHelper.FormatScheduleOutput(finishTime, ShowDates, ProjectStart, m_DateTimeCalculator);
                        output.AppendLine($@"Activity {scheduledActivity.Id}: {start} -> {finish}");
                        previousFinishTime = finishTime;
                    }
                    output.AppendLine();
                }
                return output.ToString();
            }
        }

        #endregion

        #region ICoreViewModel Members

        public DateTime ProjectStart
        {
            get
            {
                return m_ProjectStart;
            }
            set
            {
                lock (m_Lock)
                {
                    m_ProjectStart = value;
                }
                RaisePropertyChanged();
            }
        }

        public bool IsProjectUpdated
        {
            get
            {
                return m_IsProjectUpdated;
            }
            set
            {
                lock (m_Lock)
                {
                    m_IsProjectUpdated = value;
                }
                RaisePropertyChanged();
            }
        }

        public bool ShowDates
        {
            get
            {
                return m_ShowDates;
            }
            set
            {
                lock (m_Lock)
                {
                    m_ShowDates = value;
                }
                RaisePropertyChanged();
            }
        }

        public bool UseBusinessDays
        {
            get
            {
                return m_UseBusinessDays;
            }
            set
            {
                lock (m_Lock)
                {
                    m_UseBusinessDays = value;
                    m_DateTimeCalculator.UseBusinessDays(value);
                }
                RaisePropertyChanged();
            }
        }

        public bool HasStaleOutputs
        {
            get
            {
                return m_HasStaleOutputs;
            }
            set
            {
                lock (m_Lock)
                {
                    m_HasStaleOutputs = value;
                }
                RaisePropertyChanged();
            }
        }

        public bool AutoCompile
        {
            get
            {
                return m_AutoCompile;
            }
            set
            {
                lock (m_Lock)
                {
                    m_AutoCompile = value;
                }
                RaisePropertyChanged();
            }
        }

        public bool HasCompilationErrors
        {
            get
            {
                return m_HasCompilationErrors;
            }
            set
            {
                lock (m_Lock)
                {
                    m_HasCompilationErrors = value;
                }
                RaisePropertyChanged();
            }
        }

        public GraphCompilation<int, IDependentActivity<int>> GraphCompilation
        {
            get
            {
                return m_GraphCompilation;
            }
            set
            {
                lock (m_Lock)
                {
                    m_GraphCompilation = value;
                }
                RaisePropertyChanged();
            }
        }

        public string CompilationOutput
        {
            get
            {
                return m_CompilationOutput;
            }
            set
            {
                lock (m_Lock)
                {
                    m_CompilationOutput = value;
                }
                RaisePropertyChanged();
            }
        }

        public ArrowGraphDto ArrowGraphDto
        {
            get
            {
                return m_ArrowGraphDto;
            }
            set
            {
                lock (m_Lock)
                {
                    m_ArrowGraphDto = value;
                }
                RaisePropertyChanged();
            }
        }

        public ObservableCollection<ManagedActivityViewModel> Activities
        {
            get;
        }

        public ArrowGraphSettingsDto ArrowGraphSettingsDto
        {
            get
            {
                return m_ArrowGraphSettingsDto;
            }
            set
            {
                lock (m_Lock)
                {
                    m_ArrowGraphSettingsDto = value;
                }
                RaisePropertyChanged();
            }
        }

        public ResourceSettingsDto ResourceSettingsDto
        {
            get
            {
                return m_ResourceSettingsDto;
            }
            set
            {
                lock (m_Lock)
                {
                    m_ResourceSettingsDto = value;
                }
                RaisePropertyChanged();
            }
        }

        public IList<ResourceSeriesDto> ResourceSeriesSet
        {
            get;
        }

        public int? CyclomaticComplexity
        {
            get
            {
                return m_CyclomaticComplexity;
            }
            set
            {
                lock (m_Lock)
                {
                    m_CyclomaticComplexity = value;
                }
                RaisePropertyChanged();
            }
        }

        public int? Duration
        {
            get
            {
                return m_Duration;
            }
            set
            {
                lock (m_Lock)
                {
                    m_Duration = value;
                }
                RaisePropertyChanged();
            }
        }

        public double? DirectCost
        {
            get
            {
                return m_DirectCost;
            }
            set
            {
                lock (m_Lock)
                {
                    m_DirectCost = value;
                }
                RaisePropertyChanged();
            }
        }

        public double? IndirectCost
        {
            get
            {
                return m_IndirectCost;
            }
            set
            {
                lock (m_Lock)
                {
                    m_IndirectCost = value;
                }
                RaisePropertyChanged();
            }
        }

        public double? OtherCost
        {
            get
            {
                return m_OtherCost;
            }
            set
            {
                lock (m_Lock)
                {
                    m_OtherCost = value;
                }
                RaisePropertyChanged();
            }
        }

        public double? TotalCost
        {
            get
            {
                return m_TotalCost;
            }
            set
            {
                lock (m_Lock)
                {
                    m_TotalCost = value;
                }
                RaisePropertyChanged();
            }
        }

        public void AddManagedActivity()
        {
            lock (m_Lock)
            {
                var activityId = m_VertexGraphCompiler.GetNextActivityId();
                AddManagedActivity(new DependentActivity<int>(activityId, 0));
            }
        }

        public void AddManagedActivity(IDependentActivity<int> dependentActivity)
        {
            if (dependentActivity == null)
            {
                throw new ArgumentNullException(nameof(dependentActivity));
            }

            lock (m_Lock)
            {
                var dateTimeCalculator = new DateTimeCalculator();
                dateTimeCalculator.UseBusinessDays(UseBusinessDays);

                var activity = new ManagedActivityViewModel(
                    dependentActivity,
                    ProjectStart,
                    ResourceSettingsDto.Resources,
                    dateTimeCalculator,
                    m_EventService);

                if (m_VertexGraphCompiler.AddActivity(activity))
                {
                    Activities.Add(activity);
                }
            }
        }

        public void RemoveManagedActivities(HashSet<int> dependentActivityIds)
        {
            if (dependentActivityIds == null)
            {
                throw new ArgumentNullException(nameof(dependentActivityIds));
            }

            lock (m_Lock)
            {
                IEnumerable<ManagedActivityViewModel> dependentActivities = Activities.Where(x => dependentActivityIds.Contains(x.Id)).ToList();

                foreach (ManagedActivityViewModel dependentActivity in dependentActivities)
                {
                    if (m_VertexGraphCompiler.RemoveActivity(dependentActivity.Id))
                    {
                        Activities.Remove(dependentActivity);
                    }
                }
            }
        }

        public void ClearManagedActivities()
        {
            lock (m_Lock)
            {
                Activities.Clear();
                m_VertexGraphCompiler.Reset();
            }
        }

        public void UpdateActivitiesTargetResources()
        {
            lock (m_Lock)
            {
                foreach (ManagedActivityViewModel activity in Activities)
                {
                    activity.SetTargetResources(ResourceSettingsDto.Resources.Select(x => x.Copy()));
                }
            }
        }

        public void UpdateActivitiesTargetResourceDependencies()
        {
            lock (m_Lock)
            {
                foreach (ManagedActivityViewModel activity in Activities.Where(x => x.HasUpdatedDependencies))
                {
                    m_VertexGraphCompiler.SetActivityDependencies(activity.Id, new HashSet<int>(activity.UpdatedDependencies));
                    activity.UpdatedDependencies.Clear();
                    activity.HasUpdatedDependencies = false;
                }
            }
        }

        public void UpdateActivitiesAllocatedResources()
        {
            lock (m_Lock)
            {
                IList<IResourceSchedule<int>> resourceSchedules = GraphCompilation?.ResourceSchedules;
                IList<ResourceDto> resources = ResourceSettingsDto.Resources;

                if (resourceSchedules == null || resources == null)
                {
                    return;
                }

                var activityAllocatedResourcesLookup = new Dictionary<int, HashSet<int>>();

                for (int resourceIndex = 0; resourceIndex < resourceSchedules.Count; resourceIndex++)
                {
                    IResourceSchedule<int> resourceSchedule = resourceSchedules[resourceIndex];
                    IList<IScheduledActivity<int>> scheduledActivities = resourceSchedule?.ScheduledActivities;
                    if (resourceSchedule.Resource == null || scheduledActivities == null)
                    {
                        continue;
                    }

                    foreach (IScheduledActivity<int> scheduledActivity in scheduledActivities)
                    {
                        HashSet<int> allocatedResources;
                        if (!activityAllocatedResourcesLookup.TryGetValue(scheduledActivity.Id, out allocatedResources))
                        {
                            allocatedResources = new HashSet<int>();
                            activityAllocatedResourcesLookup.Add(scheduledActivity.Id, allocatedResources);
                        }

                        allocatedResources.Add(resourceSchedule.Resource.Id);
                    }
                }

                foreach (ManagedActivityViewModel activity in Activities)
                {
                    HashSet<int> allocatedResources;
                    if (!activityAllocatedResourcesLookup.TryGetValue(activity.Id, out allocatedResources))
                    {
                        allocatedResources = new HashSet<int>();
                    }

                    activity.SetAllocatedResources(
                        resources.Select(x => x.Copy()),
                        allocatedResources);
                }
            }
        }

        public void UpdateActivitiesProjectStart()
        {
            lock (m_Lock)
            {
                foreach (ManagedActivityViewModel activity in Activities)
                {
                    activity.ProjectStart = ProjectStart;
                }
            }
        }

        public void UpdateActivitiesUseBusinessDays()
        {
            lock (m_Lock)
            {
                foreach (ManagedActivityViewModel activity in Activities)
                {
                    activity.UseBusinessDays = UseBusinessDays;
                }
            }
        }

        public void RunCompile()
        {
            lock (m_Lock)
            {
                UpdateActivitiesTargetResourceDependencies();

                var availableResources = new List<IResource<int>>();
                if (!ResourceSettingsDto.AreDisabled)
                {
                    availableResources.AddRange(ResourceSettingsDto.Resources.Select(x => DtoConverter.FromDto(x)));
                }

                GraphCompilation = m_VertexGraphCompiler.Compile(availableResources);

                CyclomaticComplexity = m_VertexGraphCompiler.CyclomaticComplexity;

                Duration = m_VertexGraphCompiler.Duration;

                UpdateActivitiesAllocatedResources();

                CalculateCosts();

                IsProjectUpdated = true;

                SetCompilationOutput();
                HasStaleOutputs = false;
            }

            PublishGraphCompiledPayload();
            PublishGraphCompilationUpdatedPayload();
        }

        public void RunAutoCompile()
        {
            if (AutoCompile)
            {
                RunCompile();
            }
        }

        public void RunTransitiveReduction()
        {
            lock (m_Lock)
            {
                UpdateActivitiesTargetResourceDependencies();
                m_VertexGraphCompiler.Compile(new List<IResource<int>>());
                m_VertexGraphCompiler.TransitiveReduction();
                RunCompile();
            }
        }

        public void SetCompilationOutput()
        {
            lock (m_Lock)
            {
                GraphCompilation<int, IDependentActivity<int>> graphCompilation = GraphCompilation;
                CompilationOutput = string.Empty;
                HasCompilationErrors = false;
                if (graphCompilation == null)
                {
                    return;
                }
                var output = new StringBuilder();

                if (graphCompilation.AllResourcesExplicitTargetsButNotAllActivitiesTargeted)
                {
                    HasCompilationErrors = true;
                    output.AppendLine($@">{Properties.Resources.Message_AllResourcesExplicitTargetsNotAllActivitiesTargeted}");
                }

                if (graphCompilation.CircularDependencies.Any())
                {
                    HasCompilationErrors = true;
                    output.Append(BuildCircularDependenciesErrorMessage(graphCompilation.CircularDependencies));
                }

                if (graphCompilation.MissingDependencies.Any())
                {
                    HasCompilationErrors = true;
                    output.Append(BuildMissingDependenciesErrorMessage(graphCompilation.MissingDependencies));
                }

                if (graphCompilation.ResourceSchedules.Any()
                    && !HasCompilationErrors)
                {
                    output.Append(BuildActivitySchedules(graphCompilation.ResourceSchedules));
                }

                if (HasCompilationErrors)
                {
                    output.Insert(0, Environment.NewLine);
                    output.Insert(0, $@">{Properties.Resources.Message_CompilationErrors}");
                }

                CompilationOutput = output.ToString();
            }
        }

        public void CalculateCosts()
        {
            lock (m_Lock)
            {
                IList<IResourceSchedule<int>> resourceSchedules = GraphCompilation?.ResourceSchedules;
                IList<ResourceDto> resources = ResourceSettingsDto.Resources;

                if (resourceSchedules == null || resources == null)
                {
                    return;
                }

                ClearCosts();

                IList<ResourceSeriesDto> resourceSeriesSet = m_ProjectManager.CalculateResourceSeriesSet(resourceSchedules, resources, ResourceSettingsDto.DefaultUnitCost);
                ResourceSeriesSet.Clear();
                foreach (ResourceSeriesDto series in resourceSeriesSet)
                {
                    ResourceSeriesSet.Add(series);
                }

                if (HasCompilationErrors)
                {
                    return;
                }

                CostsDto costs = m_ProjectManager.CalculateProjectCosts(resourceSeriesSet);
                DirectCost = costs.DirectCost;
                IndirectCost = costs.IndirectCost;
                OtherCost = costs.OtherCost;
                TotalCost = costs.TotalCost;
            }
        }

        public void ClearCosts()
        {
            lock (m_Lock)
            {
                ResourceSeriesSet.Clear();
                DirectCost = null;
                IndirectCost = null;
                OtherCost = null;
                TotalCost = null;
            }
        }

        public void ClearSettings()
        {
            ArrowGraphSettingsDto = m_SettingManager.GetArrowGraphSettings();
            ResourceSettingsDto = m_SettingManager.GetResourceSettings();
        }

        #endregion
    }
}
