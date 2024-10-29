using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class TrackingManagerViewModel
        : ToolViewModelBase, ITrackingManagerViewModel, IDisposable
    {
        #region Fields

        private readonly object m_Lock;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IResourceSettingsManagerViewModel m_ResourceSettingsManagerViewModel;
        private readonly IDialogService m_DialogService;

        private readonly IDisposable? m_ColumnTitleSub;

        #endregion

        #region Ctors

        public TrackingManagerViewModel(
            ICoreViewModel coreViewModel,
            IResourceSettingsManagerViewModel resourceSettingsManagerViewModel,
            IDialogService dialogService,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(resourceSettingsManagerViewModel);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            m_Lock = new object();
            m_CoreViewModel = coreViewModel;
            m_ResourceSettingsManagerViewModel = resourceSettingsManagerViewModel;
            m_DialogService = dialogService;
            m_DateTimeCalculator = dateTimeCalculator;

            m_IsBusy = this
                .WhenAnyValue(tm => tm.m_CoreViewModel.IsBusy)
                .ToProperty(this, tm => tm.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(tm => tm.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, tm => tm.HasStaleOutputs);

            m_ShowDates = this
                .WhenAnyValue(tm => tm.m_CoreViewModel.ShowDates)
                .ToProperty(this, tm => tm.ShowDates);

            m_ProjectStart = this
                .WhenAnyValue(tm => tm.m_CoreViewModel.ProjectStart)
                .ToProperty(this, tm => tm.ProjectStart);

            m_HasCompilationErrors = this
                .WhenAnyValue(tm => tm.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, tm => tm.HasCompilationErrors);

            m_ColumnTitleSub = this
                .WhenAnyValue(
                    tm => tm.m_DateTimeCalculator.CalculatorMode,
                    tm => tm.m_CoreViewModel.TrackerIndex,
                    tm => tm.m_CoreViewModel.ShowDates,
                    tm => tm.m_CoreViewModel.ProjectStart)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => RefreshDays());

            Id = Resource.ProjectPlan.Titles.Title_TrackingView;
            Title = Resource.ProjectPlan.Titles.Title_TrackingView;
        }

        #endregion

        #region Private Methods

        private string GetDayTitle(int index)
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
            this.RaisePropertyChanged(nameof(TrackerIndex));
            this.RaisePropertyChanged(nameof(PageIndex));
            this.RaisePropertyChanged(nameof(Day00Title));
            this.RaisePropertyChanged(nameof(Day01Title));
            this.RaisePropertyChanged(nameof(Day02Title));
            this.RaisePropertyChanged(nameof(Day03Title));
            this.RaisePropertyChanged(nameof(Day04Title));
            this.RaisePropertyChanged(nameof(Day05Title));
            this.RaisePropertyChanged(nameof(Day06Title));
            this.RaisePropertyChanged(nameof(Day07Title));
            this.RaisePropertyChanged(nameof(Day08Title));
            this.RaisePropertyChanged(nameof(Day09Title));
            this.RaisePropertyChanged(nameof(Day10Title));
            this.RaisePropertyChanged(nameof(Day11Title));
            this.RaisePropertyChanged(nameof(Day12Title));
            this.RaisePropertyChanged(nameof(Day13Title));
            this.RaisePropertyChanged(nameof(Day14Title));
            //this.RaisePropertyChanged(nameof(Day15Title));
            //this.RaisePropertyChanged(nameof(Day16Title));
            //this.RaisePropertyChanged(nameof(Day17Title));
            //this.RaisePropertyChanged(nameof(Day18Title));
            //this.RaisePropertyChanged(nameof(Day19Title));
        }

        #endregion

        #region ITrackingManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_ShowDates;
        public bool ShowDates => m_ShowDates.Value;

        private readonly ObservableAsPropertyHelper<DateTimeOffset> m_ProjectStart;
        public DateTimeOffset ProjectStart => m_ProjectStart.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        public ReadOnlyObservableCollection<IManagedActivityViewModel> Activities => m_CoreViewModel.Activities;

        public ReadOnlyObservableCollection<IManagedResourceViewModel> Resources => m_ResourceSettingsManagerViewModel.Resources;

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

        public string Day00Title => GetDayTitle(0);
        public string Day01Title => GetDayTitle(1);
        public string Day02Title => GetDayTitle(2);
        public string Day03Title => GetDayTitle(3);
        public string Day04Title => GetDayTitle(4);
        public string Day05Title => GetDayTitle(5);
        public string Day06Title => GetDayTitle(6);
        public string Day07Title => GetDayTitle(7);
        public string Day08Title => GetDayTitle(8);
        public string Day09Title => GetDayTitle(9);
        public string Day10Title => GetDayTitle(10);
        public string Day11Title => GetDayTitle(11);
        public string Day12Title => GetDayTitle(12);
        public string Day13Title => GetDayTitle(13);
        public string Day14Title => GetDayTitle(14);

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
                // TODO: dispose managed state (managed objects).
                KillSubscriptions();
                m_IsBusy?.Dispose();
                m_HasStaleOutputs?.Dispose();
                m_ShowDates?.Dispose();
                m_ProjectStart?.Dispose();
                m_HasCompilationErrors?.Dispose();
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
