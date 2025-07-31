using ReactiveUI;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class DisplaySettingsViewModel
        : ViewModelBase, IDisplaySettingsViewModel
    {
        #region Fields

        private readonly object m_Lock;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private Action<bool, bool>? m_SetIsProjectUpdated;
        private Action? m_IsReadyToCompile;

        #endregion

        #region Ctors

        public DisplaySettingsViewModel(
            IDateTimeCalculator dateTimeCalculator,
            Action<bool, bool> setIsProjectUpdated,
            Action isReadyToCompile)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(setIsProjectUpdated);
            ArgumentNullException.ThrowIfNull(isReadyToCompile);
            m_Lock = new object();
            m_DateTimeCalculator = dateTimeCalculator;
            m_SetIsProjectUpdated = setIsProjectUpdated;
            m_IsReadyToCompile = isReadyToCompile;
        }

        #endregion

        #region Private Members

        private void SetIsProjectUpdated(bool isProjectUpdated, bool trackStaleOutputs)
        {
            lock (m_Lock)
            {
                if (m_SetIsProjectUpdated is not null)
                {
                    m_SetIsProjectUpdated(isProjectUpdated, trackStaleOutputs);
                }
            }
        }

        private void IsReadyToCompile()
        {
            lock (m_Lock)
            {
                if (m_IsReadyToCompile is not null)
                {
                    m_IsReadyToCompile();
                }
            }
        }

        #endregion

        #region IDisplaySettingsViewModel Members

        private bool m_ShowDates;
        public bool ShowDates
        {
            get => m_ShowDates;
            set
            {
                lock (m_Lock)
                {
                    SetIsProjectUpdated(isProjectUpdated: true, trackStaleOutputs: false);
                    this.RaiseAndSetIfChanged(ref m_ShowDates, value);
                }
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
                    SetIsProjectUpdated(isProjectUpdated: true, trackStaleOutputs: false);
                    this.RaisePropertyChanged();
                }
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
                        m_DateTimeCalculator.CalculatorMode = DateTimeCalculatorMode.BusinessDays;
                    }
                    else
                    {
                        m_DateTimeCalculator.CalculatorMode = DateTimeCalculatorMode.AllDays;
                    }
                    SetIsProjectUpdated(isProjectUpdated: true, trackStaleOutputs: true);
                    this.RaisePropertyChanged();
                    IsReadyToCompile();
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
                    SetIsProjectUpdated(isProjectUpdated: true, trackStaleOutputs: false);
                    this.RaiseAndSetIfChanged(ref m_ArrowGraphShowNames, value);
                }
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
                    SetIsProjectUpdated(isProjectUpdated: true, trackStaleOutputs: false);
                    this.RaiseAndSetIfChanged(ref m_GanttChartGroupByMode, value);
                }
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
                    SetIsProjectUpdated(isProjectUpdated: true, trackStaleOutputs: false);
                    this.RaiseAndSetIfChanged(ref m_GanttChartAnnotationStyle, value);
                }
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
                    SetIsProjectUpdated(isProjectUpdated: true, trackStaleOutputs: false);
                    this.RaiseAndSetIfChanged(ref m_GanttChartShowGroupLabels, value);
                }
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
                    SetIsProjectUpdated(isProjectUpdated: true, trackStaleOutputs: false);
                    this.RaiseAndSetIfChanged(ref m_GanttChartShowProjectFinish, value);
                }
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
                    SetIsProjectUpdated(isProjectUpdated: true, trackStaleOutputs: false);
                    this.RaiseAndSetIfChanged(ref m_GanttChartShowTracking, value);
                }
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
                    SetIsProjectUpdated(isProjectUpdated: true, trackStaleOutputs: false);
                    this.RaiseAndSetIfChanged(ref m_GanttChartShowToday, value);
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
                    SetIsProjectUpdated(isProjectUpdated: true, trackStaleOutputs: false);
                    this.RaiseAndSetIfChanged(ref m_ResourceChartAllocationMode, value);
                }
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
                    SetIsProjectUpdated(isProjectUpdated: true, trackStaleOutputs: false);
                    this.RaiseAndSetIfChanged(ref m_ResourceChartScheduleMode, value);
                }
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
                    SetIsProjectUpdated(isProjectUpdated: true, trackStaleOutputs: false);
                    this.RaiseAndSetIfChanged(ref m_ResourceChartDisplayStyle, value);
                }
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
                    SetIsProjectUpdated(isProjectUpdated: true, trackStaleOutputs: false);
                    this.RaiseAndSetIfChanged(ref m_ResourceChartShowToday, value);
                }
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
                    SetIsProjectUpdated(isProjectUpdated: true, trackStaleOutputs: false);
                    this.RaiseAndSetIfChanged(ref m_EarnedValueShowProjections, value);
                }
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
                    SetIsProjectUpdated(isProjectUpdated: true, trackStaleOutputs: false);
                    this.RaiseAndSetIfChanged(ref m_EarnedValueShowToday, value);
                }
            }
        }



        public void SetValues(DisplaySettingsModel model)
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
                if (UseBusinessDays != model.UseBusinessDays)
                {
                    UseBusinessDays = model.UseBusinessDays;
                }


                if (ArrowGraphShowNames != model.ArrowGraphShowNames)
                {
                    ArrowGraphShowNames = model.ArrowGraphShowNames;
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


                if (EarnedValueShowProjections != model.EarnedValueShowProjections)
                {
                    EarnedValueShowProjections = model.EarnedValueShowProjections;
                }
                if (EarnedValueShowToday != model.EarnedValueShowToday)
                {
                    EarnedValueShowToday = model.EarnedValueShowToday;
                }
            }
        }

        public DisplaySettingsModel GetValues()
        {
            lock (m_Lock)
            {
                return new DisplaySettingsModel
                {
                    ShowDates = ShowDates,
                    UseClassicDates = UseClassicDates,
                    UseBusinessDays = UseBusinessDays,

                    ArrowGraphShowNames = ArrowGraphShowNames,

                    GanttChartGroupByMode = GanttChartGroupByMode,
                    GanttChartAnnotationStyle = GanttChartAnnotationStyle,
                    GanttChartShowGroupLabels = GanttChartShowGroupLabels,
                    GanttChartShowProjectFinish = GanttChartShowProjectFinish,
                    GanttChartShowTracking = GanttChartShowTracking,
                    GanttChartShowToday = GanttChartShowToday,

                    ResourceChartAllocationMode = ResourceChartAllocationMode,
                    ResourceChartScheduleMode = ResourceChartScheduleMode,
                    ResourceChartDisplayStyle = ResourceChartDisplayStyle,
                    ResourceChartShowToday = ResourceChartShowToday,

                    EarnedValueShowProjections = EarnedValueShowProjections,
                    EarnedValueShowToday = EarnedValueShowToday,
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
                // TODO: dispose managed state (managed objects).
                m_SetIsProjectUpdated = null;
                m_IsReadyToCompile = null;
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
