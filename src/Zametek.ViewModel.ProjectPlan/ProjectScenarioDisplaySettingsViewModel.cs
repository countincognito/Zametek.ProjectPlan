using ReactiveUI;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ProjectScenarioDisplaySettingsViewModel
        : ViewModelBase, IProjectScenarioDisplaySettingsViewModel
    {
        #region Fields

        private readonly Lock m_Lock;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private Action<bool, bool>? m_SetIsProjectScenarioUpdated;
        private Action? m_IsReadyToCompile;

        #endregion

        #region Ctors

        public ProjectScenarioDisplaySettingsViewModel(
            IDateTimeCalculator dateTimeCalculator,
            Action<bool, bool> setIsProjectScenarioUpdated,
            Action isReadyToCompile)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(setIsProjectScenarioUpdated);
            ArgumentNullException.ThrowIfNull(isReadyToCompile);
            m_Lock = new();
            m_DateTimeCalculator = dateTimeCalculator;
            m_SetIsProjectScenarioUpdated = setIsProjectScenarioUpdated;
            m_IsReadyToCompile = isReadyToCompile;
            m_GanttChartShowConnections = [];
            m_EarnedValueShowResources = [];
        }

        #endregion

        #region Private Members

        // These only invoke the readonly core callbacks, so they are
        // deliberately lock-free - and every caller must invoke them OUTSIDE
        // its own lock (m_Lock) block. Holding m_Lock across the callback
        // creates a DS -> Core lock-order edge that deadlocked against
        // ProcessProjectScenario, which holds CoreViewModel's lock while
        // calling back into this class (SetValues) - dump-proven 2026-08-17.
        private void SetIsProjectScenarioUpdated(bool isProjectScenarioUpdated, bool trackStaleOutputs)
        {
            m_SetIsProjectScenarioUpdated?.Invoke(isProjectScenarioUpdated, trackStaleOutputs);
        }

        private void IsReadyToCompile()
        {
            m_IsReadyToCompile?.Invoke();
        }

        #endregion

        #region IProjectScenarioDisplaySettingsViewModel Members

        private bool m_ShowDates;
        public bool ShowDates
        {
            get => m_ShowDates;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_ShowDates, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }

        private bool m_UseClassicDates;
        public bool UseClassicDates
        {
            get => m_UseClassicDates;
            set
            {
                lock (m_Lock)
                {
                    m_UseClassicDates = value;
                    if (m_UseClassicDates)
                    {
                        m_DateTimeCalculator.DisplayMode = DateTimeDisplayMode.Classic;
                    }
                    else
                    {
                        m_DateTimeCalculator.DisplayMode = DateTimeDisplayMode.Default;
                    }
                    this.RaisePropertyChanged();
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }

        private NonWorkingDayMode m_NonWorkingDayMode;
        public NonWorkingDayMode NonWorkingDayMode
        {
            get => m_NonWorkingDayMode;
            set
            {
                lock (m_Lock)
                {
                    m_NonWorkingDayMode = value;
                    m_DateTimeCalculator.NonWorkingDayMode = m_NonWorkingDayMode;
                    this.RaisePropertyChanged();
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: true);
                IsReadyToCompile();
            }
        }

        private bool m_HideCost;
        public bool HideCost
        {
            get => m_HideCost;
            set
            {
                lock (m_Lock)
                {
                    m_HideCost = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        private bool m_HideBilling;
        public bool HideBilling
        {
            get => m_HideBilling;
            set
            {
                lock (m_Lock)
                {
                    m_HideBilling = value;
                    this.RaisePropertyChanged();
                }
            }
        }



        private bool m_ArrowGraphShowNames;
        public bool ArrowGraphShowNames
        {
            get => m_ArrowGraphShowNames;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_ArrowGraphShowNames, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }



        private bool m_VertexGraphShowNames;
        public bool VertexGraphShowNames
        {
            get => m_VertexGraphShowNames;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_VertexGraphShowNames, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }



        private EdgeRoutingMode m_ArrowGraphEdgeRoutingMode;
        public EdgeRoutingMode ArrowGraphEdgeRoutingMode
        {
            get => m_ArrowGraphEdgeRoutingMode;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_ArrowGraphEdgeRoutingMode, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }



        private EdgeRoutingMode m_VertexGraphEdgeRoutingMode;
        public EdgeRoutingMode VertexGraphEdgeRoutingMode
        {
            get => m_VertexGraphEdgeRoutingMode;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_VertexGraphEdgeRoutingMode, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }



        private GroupByMode m_GanttChartGroupByMode;
        public GroupByMode GanttChartGroupByMode
        {
            get => m_GanttChartGroupByMode;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_GanttChartGroupByMode, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }

        private AnnotationStyle m_GanttChartAnnotationStyle;
        public AnnotationStyle GanttChartAnnotationStyle
        {
            get => m_GanttChartAnnotationStyle;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_GanttChartAnnotationStyle, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }

        private bool m_GanttChartShowGroupLabels;
        public bool GanttChartShowGroupLabels
        {
            get => m_GanttChartShowGroupLabels;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_GanttChartShowGroupLabels, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }

        private bool m_GanttChartShowProjectFinish;
        public bool GanttChartShowProjectFinish
        {
            get => m_GanttChartShowProjectFinish;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_GanttChartShowProjectFinish, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }

        private bool m_GanttChartShowTracking;
        public bool GanttChartShowTracking
        {
            get => m_GanttChartShowTracking;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_GanttChartShowTracking, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }

        private bool m_GanttChartShowToday;
        public bool GanttChartShowToday
        {
            get => m_GanttChartShowToday;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_GanttChartShowToday, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }

        private bool m_GanttChartShowMilestones;
        public bool GanttChartShowMilestones
        {
            get => m_GanttChartShowMilestones;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_GanttChartShowMilestones, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }

        private bool m_GanttChartShowSlack;
        public bool GanttChartShowSlack
        {
            get => m_GanttChartShowSlack;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_GanttChartShowSlack, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }

        private bool m_GanttChartShowNonWorkingDays;
        public bool GanttChartShowNonWorkingDays
        {
            get => m_GanttChartShowNonWorkingDays;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_GanttChartShowNonWorkingDays, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }

        private readonly List<int> m_GanttChartShowConnections;
        public List<int> GanttChartShowConnections => m_GanttChartShowConnections;

        private ReadyToRevise m_IsReadyToReviseGanttChartShowConnections;
        public ReadyToRevise IsReadyToReviseGanttChartShowConnections
        {
            get => m_IsReadyToReviseGanttChartShowConnections;
            set
            {
                lock (m_Lock)
                {
                    //SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
                    m_IsReadyToReviseGanttChartShowConnections = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        private AllocationMode m_ResourceChartAllocationMode;
        public AllocationMode ResourceChartAllocationMode
        {
            get => m_ResourceChartAllocationMode;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_ResourceChartAllocationMode, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }

        private ScheduleMode m_ResourceChartScheduleMode;
        public ScheduleMode ResourceChartScheduleMode
        {
            get => m_ResourceChartScheduleMode;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_ResourceChartScheduleMode, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }

        private DisplayStyle m_ResourceChartDisplayStyle;
        public DisplayStyle ResourceChartDisplayStyle
        {
            get => m_ResourceChartDisplayStyle;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_ResourceChartDisplayStyle, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }

        private bool m_ResourceChartShowToday;
        public bool ResourceChartShowToday
        {
            get => m_ResourceChartShowToday;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_ResourceChartShowToday, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }

        private bool m_ResourceChartShowMilestones;
        public bool ResourceChartShowMilestones
        {
            get => m_ResourceChartShowMilestones;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_ResourceChartShowMilestones, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }



        private bool m_EarnedValueShowProjections;
        public bool EarnedValueShowProjections
        {
            get => m_EarnedValueShowProjections;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_EarnedValueShowProjections, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }

        private bool m_EarnedValueShowToday;
        public bool EarnedValueShowToday
        {
            get => m_EarnedValueShowToday;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_EarnedValueShowToday, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }

        private bool m_EarnedValueShowMilestones;
        public bool EarnedValueShowMilestones
        {
            get => m_EarnedValueShowMilestones;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_EarnedValueShowMilestones, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }

        private bool m_EarnedValueCombineResources;
        public bool EarnedValueCombineResources
        {
            get => m_EarnedValueCombineResources;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_EarnedValueCombineResources, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }

        private bool m_EarnedValueScaleToOwnPlan;
        public bool EarnedValueScaleToOwnPlan
        {
            get => m_EarnedValueScaleToOwnPlan;
            set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_EarnedValueScaleToOwnPlan, value);
                }
                SetIsProjectScenarioUpdated(isProjectScenarioUpdated: true, trackStaleOutputs: false);
            }
        }

        private readonly List<int> m_EarnedValueShowResources;
        public List<int> EarnedValueShowResources => m_EarnedValueShowResources;

        private ReadyToRevise m_IsReadyToReviseEarnedValueShowResources;
        public ReadyToRevise IsReadyToReviseEarnedValueShowResources
        {
            get => m_IsReadyToReviseEarnedValueShowResources;
            set
            {
                lock (m_Lock)
                {
                    m_IsReadyToReviseEarnedValueShowResources = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public void SetIsProjectScenarioUpdated(bool isProjectScenarioUpdated)
        {
            SetIsProjectScenarioUpdated(isProjectScenarioUpdated, trackStaleOutputs: false);
        }

        public void SetValues(ProjectScenarioDisplaySettingsModel model)
        {
            lock (m_Lock)
            {
                if (ShowDates != model.ShowDates)
                {
                    ShowDates = model.ShowDates;
                }
                if (UseClassicDates != model.UseClassicDates)
                {
                    UseClassicDates = model.UseClassicDates;
                }
                if (NonWorkingDayMode != model.NonWorkingDayMode)
                {
                    NonWorkingDayMode = model.NonWorkingDayMode;
                }
                if (HideCost != model.HideCost)
                {
                    HideCost = model.HideCost;
                }
                if (HideBilling != model.HideBilling)
                {
                    HideBilling = model.HideBilling;
                }


                if (ArrowGraphShowNames != model.ArrowGraphShowNames)
                {
                    ArrowGraphShowNames = model.ArrowGraphShowNames;
                }


                if (VertexGraphShowNames != model.VertexGraphShowNames)
                {
                    VertexGraphShowNames = model.VertexGraphShowNames;
                }


                if (ArrowGraphEdgeRoutingMode != model.ArrowGraphEdgeRoutingMode)
                {
                    ArrowGraphEdgeRoutingMode = model.ArrowGraphEdgeRoutingMode;
                }

                if (VertexGraphEdgeRoutingMode != model.VertexGraphEdgeRoutingMode)
                {
                    VertexGraphEdgeRoutingMode = model.VertexGraphEdgeRoutingMode;
                }


                if (GanttChartGroupByMode != model.GanttChartGroupByMode)
                {
                    GanttChartGroupByMode = model.GanttChartGroupByMode;
                }
                if (GanttChartAnnotationStyle != model.GanttChartAnnotationStyle)
                {
                    GanttChartAnnotationStyle = model.GanttChartAnnotationStyle;
                }
                if (GanttChartShowGroupLabels != model.GanttChartShowGroupLabels)
                {
                    GanttChartShowGroupLabels = model.GanttChartShowGroupLabels;
                }
                if (GanttChartShowProjectFinish != model.GanttChartShowProjectFinish)
                {
                    GanttChartShowProjectFinish = model.GanttChartShowProjectFinish;
                }
                if (GanttChartShowTracking != model.GanttChartShowTracking)
                {
                    GanttChartShowTracking = model.GanttChartShowTracking;
                }
                if (GanttChartShowToday != model.GanttChartShowToday)
                {
                    GanttChartShowToday = model.GanttChartShowToday;
                }
                if (GanttChartShowMilestones != model.GanttChartShowMilestones)
                {
                    GanttChartShowMilestones = model.GanttChartShowMilestones;
                }
                if (GanttChartShowSlack != model.GanttChartShowSlack)
                {
                    GanttChartShowSlack = model.GanttChartShowSlack;
                }
                if (GanttChartShowNonWorkingDays != model.GanttChartShowNonWorkingDays)
                {
                    GanttChartShowNonWorkingDays = model.GanttChartShowNonWorkingDays;
                }

                GanttChartShowConnections.Clear();
                GanttChartShowConnections.AddRange(model.GanttChartShowConnections);
                IsReadyToReviseGanttChartShowConnections = ReadyToRevise.Yes;


                if (ResourceChartAllocationMode != model.ResourceChartAllocationMode)
                {
                    ResourceChartAllocationMode = model.ResourceChartAllocationMode;
                }
                if (ResourceChartScheduleMode != model.ResourceChartScheduleMode)
                {
                    ResourceChartScheduleMode = model.ResourceChartScheduleMode;
                }
                if (ResourceChartDisplayStyle != model.ResourceChartDisplayStyle)
                {
                    ResourceChartDisplayStyle = model.ResourceChartDisplayStyle;
                }
                if (ResourceChartShowToday != model.ResourceChartShowToday)
                {
                    ResourceChartShowToday = model.ResourceChartShowToday;
                }
                if (ResourceChartShowMilestones != model.ResourceChartShowMilestones)
                {
                    ResourceChartShowMilestones = model.ResourceChartShowMilestones;
                }


                if (EarnedValueShowProjections != model.EarnedValueShowProjections)
                {
                    EarnedValueShowProjections = model.EarnedValueShowProjections;
                }
                if (EarnedValueShowToday != model.EarnedValueShowToday)
                {
                    EarnedValueShowToday = model.EarnedValueShowToday;
                }
                if (EarnedValueShowMilestones != model.EarnedValueShowMilestones)
                {
                    EarnedValueShowMilestones = model.EarnedValueShowMilestones;
                }
                if (EarnedValueCombineResources != model.EarnedValueCombineResources)
                {
                    EarnedValueCombineResources = model.EarnedValueCombineResources;
                }
                if (EarnedValueScaleToOwnPlan != model.EarnedValueScaleToOwnPlan)
                {
                    EarnedValueScaleToOwnPlan = model.EarnedValueScaleToOwnPlan;
                }

                EarnedValueShowResources.Clear();
                EarnedValueShowResources.AddRange(model.EarnedValueShowResources);
                IsReadyToReviseEarnedValueShowResources = ReadyToRevise.Yes;
            }
        }

        public ProjectScenarioDisplaySettingsModel GetValues()
        {
            lock (m_Lock)
            {
                return new ProjectScenarioDisplaySettingsModel
                {
                    ShowDates = ShowDates,
                    UseClassicDates = UseClassicDates,
                    NonWorkingDayMode = NonWorkingDayMode,
                    HideCost = HideCost,
                    HideBilling = HideBilling,

                    ArrowGraphShowNames = ArrowGraphShowNames,

                    VertexGraphShowNames = VertexGraphShowNames,

                    ArrowGraphEdgeRoutingMode = ArrowGraphEdgeRoutingMode,
                    VertexGraphEdgeRoutingMode = VertexGraphEdgeRoutingMode,

                    GanttChartGroupByMode = GanttChartGroupByMode,
                    GanttChartAnnotationStyle = GanttChartAnnotationStyle,
                    GanttChartShowGroupLabels = GanttChartShowGroupLabels,
                    GanttChartShowProjectFinish = GanttChartShowProjectFinish,
                    GanttChartShowTracking = GanttChartShowTracking,
                    GanttChartShowToday = GanttChartShowToday,
                    GanttChartShowMilestones = GanttChartShowMilestones,
                    GanttChartShowSlack = GanttChartShowSlack,
                    GanttChartShowNonWorkingDays = GanttChartShowNonWorkingDays,
                    GanttChartShowConnections = [.. GanttChartShowConnections],

                    ResourceChartAllocationMode = ResourceChartAllocationMode,
                    ResourceChartScheduleMode = ResourceChartScheduleMode,
                    ResourceChartDisplayStyle = ResourceChartDisplayStyle,
                    ResourceChartShowToday = ResourceChartShowToday,
                    ResourceChartShowMilestones = ResourceChartShowMilestones,

                    EarnedValueShowProjections = EarnedValueShowProjections,
                    EarnedValueShowToday = EarnedValueShowToday,
                    EarnedValueShowMilestones = EarnedValueShowMilestones,
                    EarnedValueCombineResources = EarnedValueCombineResources,
                    EarnedValueScaleToOwnPlan = EarnedValueScaleToOwnPlan,
                    EarnedValueShowResources = [.. EarnedValueShowResources],
                };
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
                m_SetIsProjectScenarioUpdated = null;
                m_IsReadyToCompile = null;
            }

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
