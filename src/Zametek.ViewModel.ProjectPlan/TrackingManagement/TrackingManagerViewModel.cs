using ReactiveUI;
using System.Collections.ObjectModel;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class TrackingManagerViewModel
        : ToolViewModelBase, ITrackingManagerViewModel, IDisposable
    {
        #region Fields

        private readonly object m_Lock;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IDialogService m_DialogService;

        #endregion

        #region Ctors

        public TrackingManagerViewModel(
            ICoreViewModel coreViewModel,
            IDialogService dialogService,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            m_Lock = new object();
            m_CoreViewModel = coreViewModel;
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

            Id = Resource.ProjectPlan.Titles.Title_TrackingView;
            Title = Resource.ProjectPlan.Titles.Title_TrackingView;
        }

        #endregion

        #region Private Methods

        private string GetDayTitle(int index)
        {
            lock (m_Lock)
            {
                int indexOffset = index + TrackerIndex;
                return $@"Day {indexOffset}"; // TODO replace
            }
        }

        private void RefreshDays()
        {
            this.RaisePropertyChanged(nameof(Day00Title));
            this.RaisePropertyChanged(nameof(Day01Title));
            this.RaisePropertyChanged(nameof(Day02Title));
            this.RaisePropertyChanged(nameof(Day03Title));
            this.RaisePropertyChanged(nameof(Day04Title));
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

        private readonly IDateTimeCalculator m_DateTimeCalculator;
        public IDateTimeCalculator DateTimeCalculator => m_DateTimeCalculator;


        // TODO
        public int TrackerIndex
        {
            get => m_CoreViewModel.TrackerIndex;
            set
            {
                m_CoreViewModel.TrackerIndex = value;
                this.RaisePropertyChanged();
                RefreshDays();
            }
        }

        public string Day00Title => GetDayTitle(0);

        public string Day01Title => GetDayTitle(1);

        public string Day02Title => GetDayTitle(2);

        public string Day03Title => GetDayTitle(3);

        public string Day04Title => GetDayTitle(4);

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
