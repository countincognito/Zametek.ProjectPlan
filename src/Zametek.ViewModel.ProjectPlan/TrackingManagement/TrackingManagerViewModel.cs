using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class TrackingManagerViewModel
        : ToolViewModelBase, ITrackingManagerViewModel
    {
        #region Fields

        private readonly Lock m_Lock;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IResourceSettingsManagerViewModel m_ResourceSettingsManagerViewModel;
        private readonly List<DayTitleViewModel> m_DayTitles;

        private readonly IDisposable? m_ColumnTitleSub;

        #endregion

        #region Ctors

        public TrackingManagerViewModel(
            ICoreViewModel coreViewModel,
            IResourceSettingsManagerViewModel resourceSettingsManagerViewModel,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(resourceSettingsManagerViewModel);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            m_Lock = new();
            m_CoreViewModel = coreViewModel;
            m_ResourceSettingsManagerViewModel = resourceSettingsManagerViewModel;
            m_DateTimeCalculator = dateTimeCalculator;
            m_DayTitles = [.. Enumerable.Range(0, TrackingHelper.DayCount)
                .Select(dayOffset => new DayTitleViewModel(this, dayOffset))];

            SyncTodayCommand = ReactiveCommand.Create(SyncToday);

            m_IsBusy = this
                .WhenAnyValue(tm => tm.m_CoreViewModel.IsBusy)
                .ToProperty(this, tm => tm.IsBusy);

            m_HasActivities = this
                .WhenAnyValue(tm => tm.m_CoreViewModel.HasActivities)
                .ToProperty(this, tm => tm.HasActivities);

            m_HasResources = this
                .WhenAnyValue(tm => tm.m_CoreViewModel.HasResources)
                .ToProperty(this, tm => tm.HasResources);

            m_HasStaleOutputs = this
                .WhenAnyValue(tm => tm.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, tm => tm.HasStaleOutputs);

            m_ShowDates = this
                .WhenAnyValue(tm => tm.m_CoreViewModel.DisplaySettingsViewModel.ShowDates)
                .ToProperty(this, tm => tm.ShowDates);

            m_ProjectStart = this
                .WhenAnyValue(tm => tm.m_CoreViewModel.ProjectStart)
                .ToProperty(this, tm => tm.ProjectStart);

            m_HasCompilationErrors = this
                .WhenAnyValue(tm => tm.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, tm => tm.HasCompilationErrors);

            m_ColumnTitleSub = this
                .WhenAnyValue(
                    tm => tm.m_DateTimeCalculator.NonWorkingDayMode,
                    tm => tm.m_CoreViewModel.TrackerIndex,
                    tm => tm.m_CoreViewModel.DisplaySettingsViewModel.ShowDates,
                    tm => tm.m_CoreViewModel.HolidaySettings,
                    tm => tm.m_CoreViewModel.ProjectStart)
                .MuteWhile(this.WhenAnyValue(tm => tm.m_CoreViewModel.IsBulkUpdating)) // Conflate redundant notifications while a project scenario is loaded/reset.
                .ObserveOn(RxSchedulers.TaskpoolScheduler)
                .Subscribe(_ => RefreshDays());
        }

        #endregion

        #region Private Methods

        internal string GetDayTitle(int index)
        {
            lock (m_Lock)
            {
                if (index < 0)
                {
                    return string.Empty;
                }
                int indexOffset = index + TrackerIndex;

                if (ShowDates)
                {
                    return m_DateTimeCalculator.AddDays(ProjectStart, indexOffset).ToString("d");
                }
                return $@"{indexOffset}";
            }
        }

        private void RefreshDays()
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(TrackingManagerViewModel)}.{nameof(RefreshDays)}");
            this.RaisePropertyChanged(nameof(TrackerIndex));
            this.RaisePropertyChanged(nameof(PageIndex));

            foreach (DayTitleViewModel dayTitle in m_DayTitles)
            {
                dayTitle.RefreshTitle();
            }
        }

        private void SyncToday()
        {
            int? intValue;

            lock (m_Lock)
            {
                (intValue, _) = m_DateTimeCalculator
                    .CalculateTimeAndDateTime(
                        m_CoreViewModel.ProjectStart,
                        m_CoreViewModel.Today);
            }

            // Setting the tracker index triggers property-change cascades
            // across the app (some of which re-enter this view model from
            // other threads), so it must happen outside the lock.
            TrackerIndex = intValue.GetValueOrDefault();
        }

        #endregion

        #region ITrackingManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasActivities;
        public bool HasActivities => m_HasActivities.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasResources;
        public bool HasResources => m_HasResources.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_ShowDates;
        public bool ShowDates => m_ShowDates.Value;

        private readonly ObservableAsPropertyHelper<DateTimeOffset> m_ProjectStart;
        public DateTimeOffset ProjectStart => m_ProjectStart.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        public IReadOnlyList<IManagedActivityViewModel> RawActivities => m_CoreViewModel.RawActivities;

        public ReadOnlyObservableCollection<IManagedActivityViewModel> Activities => m_CoreViewModel.Activities;

        public ObservableCollection<IManagedActivityViewModel> OrderableActivities => m_CoreViewModel.OrderableActivities;

        public IReadOnlyList<IManagedResourceViewModel> RawResources => m_ResourceSettingsManagerViewModel.RawResources;

        public ReadOnlyObservableCollection<IManagedResourceViewModel> Resources => m_ResourceSettingsManagerViewModel.Resources;

        public ObservableCollection<IManagedResourceViewModel> OrderableResources => m_ResourceSettingsManagerViewModel.OrderableResources;

        private readonly IDateTimeCalculator m_DateTimeCalculator;
        public IDateTimeCalculator DateTimeCalculator => m_DateTimeCalculator;

        public int TrackerIndex
        {
            get => m_CoreViewModel.TrackerIndex;
            set
            {
                if (m_CoreViewModel.TrackerIndex != value)
                {
                    m_CoreViewModel.TrackerIndex = value;
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged(nameof(PageIndex));
                }
            }
        }

        public int? PageIndex
        {
            get => TrackerIndex + 1;
            set
            {
                int input = value.GetValueOrDefault();
                if (input > 0)
                {
                    TrackerIndex = input - 1;
                }
                else
                {
                    TrackerIndex = 0;
                }
                this.RaisePropertyChanged();
            }
        }

        public IReadOnlyList<IDayTitleViewModel> DayTitles => m_DayTitles;

        public ICommand SyncTodayCommand { get; }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
            m_ColumnTitleSub?.Dispose();
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
                KillSubscriptions();
                m_IsBusy?.Dispose();
                m_HasActivities?.Dispose();
                m_HasResources?.Dispose();
                m_HasStaleOutputs?.Dispose();
                m_ShowDates?.Dispose();
                m_ProjectStart?.Dispose();
                m_HasCompilationErrors?.Dispose();
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
