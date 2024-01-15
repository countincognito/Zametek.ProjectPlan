using ReactiveUI;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class TrackingManagerViewModel
        : ToolViewModelBase, ITrackingManagerViewModel, IDisposable
    {
        #region Fields

        private readonly object m_Lock;
        private readonly ObservableCollection<IColumnSelectorViewModel> m_AvailableStartColumns;
        private readonly ObservableCollection<IColumnCountViewModel> m_AvailableColumnsShown;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IDialogService m_DialogService;

        private readonly IDisposable? m_RaiseTrackerCountSub;

        private const int m_MaxAvailableColumnsShown = 30;

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

            m_AvailableStartColumns = [];
            AvailableStartColumns = new ReadOnlyObservableCollection<IColumnSelectorViewModel>(m_AvailableStartColumns);

            m_AvailableColumnsShown = [];
            AvailableColumnsShown = new ReadOnlyObservableCollection<IColumnCountViewModel>(m_AvailableColumnsShown);

            AddTrackersCommand = ReactiveCommand.CreateFromTask(AddTrackersAsync);
            RemoveTrackersCommand = ReactiveCommand.CreateFromTask(RemoveTrackersAsync);

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

            m_RaiseTrackerCountSub = this
                .WhenAnyValue(
                    tm => tm.m_CoreViewModel.GraphCompilation,
                    tm => tm.m_CoreViewModel.IsProjectUpdated,
                    tm => tm.m_DateTimeCalculator.Mode,
                    tm => tm.ShowDates,
                    tm => tm.ProjectStart)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(result =>
                {
                    ResetColumnSelection();
                });

            m_StartColumnIndex = 0;
            m_ColumnsShown = 0;
            ResetColumnSelection();

            Id = Resource.ProjectPlan.Titles.Title_TrackingView;
            Title = Resource.ProjectPlan.Titles.Title_TrackingView;
        }

        #endregion

        #region Private Methods

        private async Task RunAutoCompileAsync() => await Task.Run(() => m_CoreViewModel.RunAutoCompile());

        private void ResetColumnSelection()
        {
            lock (m_Lock)
            {
                ResetStartColumnSelection();
                ResetColumnsShownSelection();
            }
        }

        private void ResetStartColumnSelection()
        {
            lock (m_Lock)
            {
                int trackerCount = TrackerCount;

                // Update the start column selection.
                int? currentStartColumnSelectorIndex = StartColumnSelector?.ColumnIndex;

                m_AvailableStartColumns.Clear();

                string displayNamePrefix = string.Empty;

                if (!ShowDates)
                {
                    displayNamePrefix = $@"{Resource.ProjectPlan.Labels.Label_Day} ";
                }

                IColumnSelectorViewModel? nextStartColumnSelector = null;

                for (int i = 0; i < trackerCount; i++)
                {
                    string displayName = ShowDates
                        ? m_DateTimeCalculator.AddDays(ProjectStart, i).ToString(ProjectPlan.DateTimeCalculator.DateFormat)
                        : $@"{i}";

                    var startColumnSelector = new ColumnSelectorViewModel($@"{displayNamePrefix}{displayName}", i);
                    m_AvailableStartColumns.Add(startColumnSelector);

                    if (currentStartColumnSelectorIndex is not null
                        && currentStartColumnSelectorIndex.GetValueOrDefault() == i)
                    {
                        nextStartColumnSelector = startColumnSelector;
                    }
                }

                StartColumnSelector = nextStartColumnSelector;
            }
        }

        private void ResetColumnsShownSelection()
        {
            lock (m_Lock)
            {
                int trackerCount = TrackerCount;

                // Update the columns shown selection.

                int availableColumnsShown = trackerCount - StartColumnIndex.GetValueOrDefault();

                if (availableColumnsShown > m_MaxAvailableColumnsShown)
                {
                    availableColumnsShown = m_MaxAvailableColumnsShown;
                }

                int? currentColumnsShownSelectorCount = ColumnsShownSelector?.ColumnCount;

                m_AvailableColumnsShown.Clear();

                IColumnCountViewModel? nextColumnsShownSelector = null;

                for (int i = 0; i <= availableColumnsShown; i++)
                {
                    var columnsShownSelector = new ColumnCountViewModel($@"{i}", i);
                    m_AvailableColumnsShown.Add(columnsShownSelector);

                    if (currentColumnsShownSelectorCount is not null
                        && currentColumnsShownSelectorCount.GetValueOrDefault() == i)
                    {
                        nextColumnsShownSelector = columnsShownSelector;
                    }
                }

                // If we delete enough columns that the original value is no longer valid,
                // then revert to the final item in the list.
                if (currentColumnsShownSelectorCount is not null
                    && nextColumnsShownSelector is null)
                {
                    nextColumnsShownSelector = m_AvailableColumnsShown.LastOrDefault();
                }

                ColumnsShownSelector = nextColumnsShownSelector;
            }
        }

        private void RaiseTrackerNotifications()
        {
            lock (m_Lock)
            {
                this.RaisePropertyChanged(nameof(TrackerCount));
                this.RaisePropertyChanged(nameof(StartColumnIndex));
                this.RaisePropertyChanged(nameof(ColumnsShown));
                this.RaisePropertyChanged(nameof(EndColumnIndex));
                this.RaisePropertyChanged(nameof(AvailableStartColumns));
                this.RaisePropertyChanged(nameof(StartTime));
                this.RaisePropertyChanged(nameof(EndTime));
            }
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

        public string StartTime
        {
            get
            {
                int trackerCount = TrackerCount;
                string output = string.Empty;

                if (trackerCount != 0)
                {
                    if (ShowDates)
                    {
                        output = m_DateTimeCalculator.AddDays(ProjectStart, 0).ToString(ProjectPlan.DateTimeCalculator.DateFormat);
                    }
                    else
                    {
                        output = $@"{0}";
                    }
                }
                return output;
            }
        }

        public string EndTime
        {
            get
            {
                int trackerCount = TrackerCount;
                string output = string.Empty;

                if (trackerCount != 0)
                {
                    if (ShowDates)
                    {
                        output = m_DateTimeCalculator.AddDays(ProjectStart, trackerCount).ToString(ProjectPlan.DateTimeCalculator.DateFormat);
                    }
                    else
                    {
                        output = $@"{trackerCount}";
                    }
                }
                return output;
            }
        }

        private int? m_StartColumnIndex;
        public int? StartColumnIndex
        {
            get => m_StartColumnIndex;
            set
            {
                lock (m_Lock)
                {
                    m_StartColumnIndex = value;

                    int trackerCount = TrackerCount;

                    if (trackerCount == 0 || m_StartColumnIndex is null)
                    {
                        m_StartColumnIndex = null;
                        m_ColumnsShown = 0;
                    }

                    if (m_StartColumnIndex is not null)
                    {
                        if (m_StartColumnIndex < 0)
                        {
                            m_StartColumnIndex = 0;
                        }

                        if (m_StartColumnIndex >= trackerCount)
                        {
                            m_StartColumnIndex = trackerCount - 1;
                            m_ColumnsShown = 1;
                        }

                        int? endColumnIndex = EndColumnIndex;

                        if (endColumnIndex is not null
                            && endColumnIndex.GetValueOrDefault() >= trackerCount)
                        {
                            m_ColumnsShown = trackerCount - m_StartColumnIndex.GetValueOrDefault();
                        }
                    }

                    RaiseTrackerNotifications();
                }
            }
        }

        private int m_ColumnsShown;
        public int ColumnsShown
        {
            get => m_ColumnsShown;
            set
            {
                lock (m_Lock)
                {
                    m_ColumnsShown = value;

                    int trackerCount = TrackerCount;

                    if (trackerCount == 0 || m_StartColumnIndex is null)
                    {
                        m_StartColumnIndex = null;
                        m_ColumnsShown = 0;
                    }

                    if (m_ColumnsShown < 0)
                    {
                        m_ColumnsShown = 0;
                    }

                    int? endColumnIndex = EndColumnIndex;

                    if (endColumnIndex is not null
                        && endColumnIndex.GetValueOrDefault() >= trackerCount)
                    {
                        m_ColumnsShown = trackerCount - m_StartColumnIndex.GetValueOrDefault();
                    }

                    RaiseTrackerNotifications();
                }
            }
        }

        public int? EndColumnIndex
        {
            get => StartColumnIndex is null ? null : StartColumnIndex.GetValueOrDefault() + ColumnsShown - 1;
        }

        public int TrackerCount
        {
            get
            {
                Debug.Assert(Activities.Select(x => x.Trackers.Count).Distinct().Count() <= 1);
                return Activities.Select(x => x.Trackers.Count).FirstOrDefault();
            }
        }

        public ReadOnlyObservableCollection<IColumnSelectorViewModel> AvailableStartColumns { get; }

        private IColumnSelectorViewModel? m_StartColumnSelector;
        public IColumnSelectorViewModel? StartColumnSelector
        {
            get => m_StartColumnSelector;
            set
            {
                lock (m_Lock)
                {
                    if (value is null)
                    {
                        StartColumnIndex = null;
                    }
                    else
                    {
                        StartColumnIndex = value.ColumnIndex;
                    }
                    this.RaiseAndSetIfChanged(ref m_StartColumnSelector, value);
                    ResetColumnsShownSelection();
                }
            }
        }

        public ReadOnlyObservableCollection<IColumnCountViewModel> AvailableColumnsShown { get; }

        private IColumnCountViewModel? m_ColumnsShownSelector;
        public IColumnCountViewModel? ColumnsShownSelector
        {
            get => m_ColumnsShownSelector;
            set
            {
                lock (m_Lock)
                {
                    if (value is null)
                    {
                        ColumnsShown = 0;
                    }
                    else
                    {
                        ColumnsShown = value.ColumnCount;
                    }
                    this.RaiseAndSetIfChanged(ref m_ColumnsShownSelector, value);
                }
            }
        }

        public async Task AddTrackersAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.AddTrackers();
                    ResetColumnSelection();
                }
                await RunAutoCompileAsync();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    ex.Message);
            }
        }

        public async Task RemoveTrackersAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.RemoveTrackers();
                    ResetColumnSelection();
                }
                await RunAutoCompileAsync();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    ex.Message);
            }
        }

        public ICommand AddTrackersCommand { get; }

        public ICommand RemoveTrackersCommand { get; }

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
                m_RaiseTrackerCountSub?.Dispose();
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
