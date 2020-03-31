using AutoMapper;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Event.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class CoreViewModel
        : PropertyChangedPubSubViewModel, ICoreViewModel
    {
        #region Fields

        private readonly object m_Lock;
        private readonly IProjectService m_ProjectService;
        private readonly ISettingService m_SettingService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly VertexGraphCompiler<int, int, IDependentActivity<int, int>> m_VertexGraphCompiler;
        private bool m_IsBusy;
        private DateTime m_ProjectStart;
        private bool m_IsProjectUpdated;
        private bool m_ShowDates;
        private bool m_UseBusinessDays;
        private bool m_HasStaleOutputs;
        private bool m_AutoCompile;
        private bool m_HasCompilationErrors;
        private IGraphCompilation<int, int, IDependentActivity<int, int>> m_GraphCompilation;
        private string m_CompilationOutput;
        private ArrowGraphModel m_ArrowGraphModel;
        private ArrowGraphSettingsModel m_ArrowGraphSettingsModel;
        private ResourceSettingsModel m_ResourceSettingsModel;
        private int? m_CyclomaticComplexity;
        private int? m_Duration;
        private double? m_DirectCost;
        private double? m_IndirectCost;
        private double? m_OtherCost;
        private double? m_TotalCost;

        private readonly IMapper m_Mapper;
        private readonly IEventAggregator m_EventService;

        #endregion

        #region Ctors

        public CoreViewModel(
            IProjectService projectService,
            ISettingService settingService,
            IDateTimeCalculator dateTimeCalculator,
            IMapper mapper,
            IEventAggregator eventService)
            : base(eventService)
        {
            m_Lock = new object();
            m_ProjectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            m_SettingService = settingService ?? throw new ArgumentNullException(nameof(settingService));
            m_DateTimeCalculator = dateTimeCalculator ?? throw new ArgumentNullException(nameof(dateTimeCalculator));
            m_Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));

            m_VertexGraphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            Activities = new ObservableCollection<IManagedActivityViewModel>();
            ResourceSeriesSet = new List<ResourceSeriesModel>();
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

        private string BuildCircularDependenciesErrorMessage(IEnumerable<ICircularDependency<int>> circularDependencies)
        {
            if (circularDependencies == null)
            {
                return string.Empty;
            }
            var output = new StringBuilder();
            output.AppendLine($@">{Resource.ProjectPlan.Properties.Resources.Message_CircularDependencies}");
            foreach (CircularDependency<int> circularDependency in circularDependencies)
            {
                output.AppendLine(string.Join(@" -> ", circularDependency.Dependencies));
            }
            return output.ToString();
        }

        private string BuildMissingDependenciesErrorMessage(IEnumerable<int> missingDependencies)
        {
            if (missingDependencies == null)
            {
                return string.Empty;
            }
            var output = new StringBuilder();
            output.AppendLine($@">{Resource.ProjectPlan.Properties.Resources.Message_MissingDependencies}");
            foreach (int missingDependency in missingDependencies)
            {
                IList<int> activities = Activities
                    .Where(x => x.Dependencies.Contains(missingDependency))
                    .Select(x => x.Id)
                    .ToList();
                output.AppendFormat(CultureInfo.InvariantCulture, $@"{missingDependency} -> ");
                output.AppendLine(string.Join(@", ", activities));
            }
            return output.ToString();
        }

        //private string BuildActivitySchedules(IList<IResourceSchedule<int>> resourceSchedules)
        //{
        //    lock (m_Lock)
        //    {
        //        if (resourceSchedules == null)
        //        {
        //            return string.Empty;
        //        }
        //        var output = new StringBuilder();
        //        int spareResourceCount = 1;
        //        for (int resourceIndex = 0; resourceIndex < resourceSchedules.Count; resourceIndex++)
        //        {
        //            IResourceSchedule<int> resourceSchedule = resourceSchedules[resourceIndex];
        //            IList<IScheduledActivity<int>> scheduledActivities = resourceSchedule?.ScheduledActivities;
        //            if (scheduledActivities == null)
        //            {
        //                continue;
        //            }
        //            var stringBuilder = new StringBuilder(@">Resource");
        //            if (resourceSchedule.Resource != null)
        //            {
        //                stringBuilder.Append($@" {resourceSchedule.Resource.Id}");
        //                if (!string.IsNullOrWhiteSpace(resourceSchedule.Resource.Name))
        //                {
        //                    stringBuilder.Append($@" ({resourceSchedule.Resource.Name})");
        //                }
        //            }
        //            else
        //            {
        //                stringBuilder.Append($@" {spareResourceCount}");
        //                spareResourceCount++;
        //            }
        //            output.AppendLine(stringBuilder.ToString());
        //            int previousFinishTime = 0;
        //            foreach (IScheduledActivity<int> scheduledActivity in scheduledActivities)
        //            {
        //                int startTime = scheduledActivity.StartTime;
        //                int finishTime = scheduledActivity.FinishTime;
        //                if (startTime > previousFinishTime)
        //                {
        //                    string from = ChartHelper.FormatScheduleOutput(previousFinishTime, ShowDates, ProjectStart, m_DateTimeCalculator);
        //                    string to = ChartHelper.FormatScheduleOutput(startTime, ShowDates, ProjectStart, m_DateTimeCalculator);
        //                    output.AppendLine($@"*** {from} -> {to} ***");
        //                }
        //                string start = ChartHelper.FormatScheduleOutput(startTime, ShowDates, ProjectStart, m_DateTimeCalculator);
        //                string finish = ChartHelper.FormatScheduleOutput(finishTime, ShowDates, ProjectStart, m_DateTimeCalculator);
        //                output.AppendLine($@"Activity {scheduledActivity.Id}: {start} -> {finish}");
        //                previousFinishTime = finishTime;
        //            }
        //            output.AppendLine();
        //        }
        //        return output.ToString();
        //    }
        //}

        #endregion

        #region ICoreViewModel Members

        public bool IsBusy
        {
            get
            {
                return m_IsBusy;
            }
            set
            {
                lock (m_Lock)
                {
                    m_IsBusy = value;
                }
                RaisePropertyChanged();
            }
        }

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

        public IGraphCompilation<int, int, IDependentActivity<int, int>> GraphCompilation
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

        public ArrowGraphModel ArrowGraph
        {
            get
            {
                return m_ArrowGraphModel;
            }
            set
            {
                lock (m_Lock)
                {
                    m_ArrowGraphModel = value;
                }
                RaisePropertyChanged();
            }
        }

        public ObservableCollection<IManagedActivityViewModel> Activities
        {
            get;
        }

        public ArrowGraphSettingsModel ArrowGraphSettings
        {
            get
            {
                return m_ArrowGraphSettingsModel;
            }
            set
            {
                lock (m_Lock)
                {
                    m_ArrowGraphSettingsModel = value;
                }
                RaisePropertyChanged();
            }
        }

        public ResourceSettingsModel ResourceSettings
        {
            get
            {
                return m_ResourceSettingsModel;
            }
            set
            {
                lock (m_Lock)
                {
                    m_ResourceSettingsModel = value;
                }
                RaisePropertyChanged();
            }
        }

        public IList<ResourceSeriesModel> ResourceSeriesSet
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
                AddManagedActivity(new DependentActivity<int, int>(activityId, 0));
            }
        }

        public void AddManagedActivity(IDependentActivity<int, int> dependentActivity)
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
                    ResourceSettings.Resources,
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
                IEnumerable<IManagedActivityViewModel> dependentActivities = Activities.Where(x => dependentActivityIds.Contains(x.Id)).ToList();

                foreach (IManagedActivityViewModel dependentActivity in dependentActivities)
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
                foreach (IManagedActivityViewModel activity in Activities)
                {
                    activity.SetTargetResources(ResourceSettings.Resources.Select(x => x.CloneObject()));
                }
            }
        }

        public void UpdateActivitiesTargetResourceDependencies()
        {
            lock (m_Lock)
            {
                foreach (IManagedActivityViewModel activity in Activities.Where(x => x.HasUpdatedDependencies))
                {
                    m_VertexGraphCompiler.SetActivityDependencies(activity.Id, new HashSet<int>(activity.UpdatedDependencies));
                    activity.UpdatedDependencies.Clear();
                    activity.HasUpdatedDependencies = false;
                }
            }
        }

        public void UpdateActivitiesAllocatedToResources()
        {
            lock (m_Lock)
            {
                foreach (IManagedActivityViewModel activity in Activities)
                {
                    activity.UpdateAllocatedToResources();
                }
            }
        }

        public void UpdateActivitiesProjectStart()
        {
            lock (m_Lock)
            {
                foreach (IManagedActivityViewModel activity in Activities)
                {
                    activity.ProjectStart = ProjectStart;
                }
            }
        }

        public void UpdateActivitiesUseBusinessDays()
        {
            lock (m_Lock)
            {
                foreach (IManagedActivityViewModel activity in Activities)
                {
                    activity.UseBusinessDays = UseBusinessDays;
                }
            }
        }

        public int RunCalculateResourcedCyclomaticComplexity()
        {
            lock (m_Lock)
            {
                int resourcedCyclomaticComplexity = 0;

                // Cyclomatic complexity is calculated against the transitively reduced
                // vertex graph, where resource dependencies are taken into account along
                // with regular dependencies.
                if (GraphCompilation != null
                    && !HasCompilationErrors
                    && !HasStaleOutputs
                    && !ResourceSettings.AreDisabled)
                {
                    IList<IDependentActivity<int, int>> dependentActivities =
                        GraphCompilation.DependentActivities
                        .Select(x => (IDependentActivity<int, int>)x.CloneObject())
                        .ToList();

                    if (dependentActivities.Any())
                    {
                        var vertexGraphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
                        foreach (DependentActivity<int, int> dependentActivity in dependentActivities)
                        {
                            dependentActivity.Dependencies.UnionWith(dependentActivity.ResourceDependencies);
                            dependentActivity.ResourceDependencies.Clear();
                            vertexGraphCompiler.AddActivity(dependentActivity);
                        }

                        vertexGraphCompiler.TransitiveReduction();
                        resourcedCyclomaticComplexity = vertexGraphCompiler.CyclomaticComplexity;
                    }
                }

                return resourcedCyclomaticComplexity;
            }
        }

        public void RunCompile()
        {
            lock (m_Lock)
            {
                UpdateActivitiesTargetResourceDependencies();

                var availableResources = new List<IResource<int>>();
                if (!ResourceSettings.AreDisabled)
                {
                    availableResources.AddRange(ResourceSettings.Resources.Select(x => m_Mapper.Map<ResourceModel, Resource<int>>(x)));
                }

                GraphCompilation = m_VertexGraphCompiler.Compile(availableResources);

                Duration = m_VertexGraphCompiler.Duration;

                UpdateActivitiesAllocatedToResources();

                CalculateCosts();

                IsProjectUpdated = true;

                SetCompilationOutput();

                // Cyclomatic complexity is calculated against the vertex graph without resource dependencies.
                CyclomaticComplexity = null;

                if (!HasCompilationErrors)
                {
                    IList<IDependentActivity<int, int>> dependentActivities =
                        GraphCompilation.DependentActivities
                        .Select(x => (IDependentActivity<int, int>)x.CloneObject())
                        .ToList();

                    if (dependentActivities.Any())
                    {
                        var vertexGraphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
                        foreach (DependentActivity<int, int> dependentActivity in dependentActivities)
                        {
                            dependentActivity.ResourceDependencies.Clear();
                            vertexGraphCompiler.AddActivity(dependentActivity);
                        }

                        vertexGraphCompiler.TransitiveReduction();
                        CyclomaticComplexity = vertexGraphCompiler.CyclomaticComplexity;
                    }
                }

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
                IGraphCompilation<int, int, IDependentActivity<int, int>> graphCompilation = GraphCompilation;
                CompilationOutput = string.Empty;
                HasCompilationErrors = false;
                if (graphCompilation == null)
                {
                    return;
                }
                var output = new StringBuilder();

                if (graphCompilation.Errors != null)
                {
                    if (graphCompilation.Errors.AllResourcesExplicitTargetsButNotAllActivitiesTargeted)
                    {
                        HasCompilationErrors = true;
                        output.AppendLine($@">{Resource.ProjectPlan.Properties.Resources.Message_AllResourcesExplicitTargetsNotAllActivitiesTargeted}");
                    }

                    if (graphCompilation.Errors.CircularDependencies.Any())
                    {
                        HasCompilationErrors = true;
                        output.Append(BuildCircularDependenciesErrorMessage(graphCompilation.Errors.CircularDependencies));
                    }

                    if (graphCompilation.Errors.MissingDependencies.Any())
                    {
                        HasCompilationErrors = true;
                        output.Append(BuildMissingDependenciesErrorMessage(graphCompilation.Errors.MissingDependencies));
                    }
                }

                //if (graphCompilation.ResourceSchedules.Any()
                //    && !HasCompilationErrors)
                //{
                //    output.Append(BuildActivitySchedules(graphCompilation.ResourceSchedules));
                //}

                if (HasCompilationErrors)
                {
                    output.Insert(0, Environment.NewLine);
                    output.Insert(0, $@">{Resource.ProjectPlan.Properties.Resources.Message_CompilationErrors}");
                }

                CompilationOutput = output.ToString();
            }
        }

        public void CalculateCosts()
        {
            lock (m_Lock)
            {
                IEnumerable<IResourceSchedule<int, int>> resourceSchedules = GraphCompilation?.ResourceSchedules;
                IList<ResourceModel> resources = ResourceSettings.Resources;

                if (resourceSchedules == null || resources == null)
                {
                    return;
                }

                ClearCosts();

                IEnumerable<ResourceSeriesModel> resourceSeriesSet = m_ProjectService.CalculateResourceSeriesSet(resourceSchedules, resources, ResourceSettings.DefaultUnitCost);
                ResourceSeriesSet.Clear();
                foreach (ResourceSeriesModel series in resourceSeriesSet)
                {
                    ResourceSeriesSet.Add(series);
                }

                if (HasCompilationErrors)
                {
                    return;
                }

                CostsModel costs = m_ProjectService.CalculateProjectCosts(resourceSeriesSet);
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
            ArrowGraphSettings = m_SettingService.DefaultArrowGraphSettings;
            ResourceSettings = m_SettingService.DefaultResourceSettings;
        }

        #endregion
    }
}
