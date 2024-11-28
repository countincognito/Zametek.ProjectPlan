using Avalonia.Controls;
using ReactiveUI;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ArrowGraphSettingsManagerViewModel
        : ToolViewModelBase, IArrowGraphSettingsManagerViewModel
    {
        #region Fields

        private readonly object m_Lock;
        private ArrowGraphSettingsModel m_Current;
        private readonly IList<EdgeTypeFormatModel> m_EdgeTypeFormats;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;

        private readonly IDisposable? m_ProcessArrowGraphSettingsSub;
        private readonly IDisposable? m_UpdateArrowGraphSettingsSub;

        #endregion

        #region Ctors

        public ArrowGraphSettingsManagerViewModel(
            ICoreViewModel coreViewModel,
            ISettingService settingService,
            IDialogService dialogService)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            m_Lock = new object();
            m_Current = new ArrowGraphSettingsModel();
            m_EdgeTypeFormats = [];
            m_CoreViewModel = coreViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            SelectedActivitySeverities = new ConcurrentDictionary<Guid, IManagedActivitySeverityViewModel>();
            m_HasActivitySeverities = false;
            m_AreSettingsUpdated = false; ;

            m_ActivitySeverities = [];
            m_ReadOnlyActivitySeverities = new ReadOnlyObservableCollection<IManagedActivitySeverityViewModel>(m_ActivitySeverities);

            SetSelectedManagedActivitySeveritiesCommand = ReactiveCommand.Create<SelectionChangedEventArgs>(SetSelectedManagedActivitySeverities);
            AddManagedActivitySeverityCommand = ReactiveCommand.CreateFromTask(AddManagedActivitySeverityAsync);
            RemoveManagedActivitySeveritiesCommand = ReactiveCommand.CreateFromTask(RemoveManagedActivitySeveritiesAsync, this.WhenAnyValue(rm => rm.HasActivitySeverities));

            m_IsBusy = this
                .WhenAnyValue(rm => rm.m_CoreViewModel.IsBusy)
                .ToProperty(this, rm => rm.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(rm => rm.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, rm => rm.HasStaleOutputs);

            m_HasCompilationErrors = this
                .WhenAnyValue(rm => rm.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, rm => rm.HasCompilationErrors);

            m_ProcessArrowGraphSettingsSub = this
                .WhenAnyValue(rm => rm.m_CoreViewModel.ArrowGraphSettings)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(rs =>
                {
                    if (m_Current != rs)
                    {
                        ProcessSettings(rs);
                    }
                });

            m_UpdateArrowGraphSettingsSub = this
                .WhenAnyValue(rm => rm.AreSettingsUpdated)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(areUpdated =>
                {
                    if (areUpdated)
                    {
                        UpdateArrowGraphSettingsToCore();
                    }
                });

            ProcessSettings(m_SettingService.DefaultArrowGraphSettings);

            Id = Resource.ProjectPlan.Titles.Title_ArrowGraphSettingsView;
            Title = Resource.ProjectPlan.Titles.Title_ArrowGraphSettingsView;
        }

        #endregion

        #region Properties

        public IDictionary<Guid, IManagedActivitySeverityViewModel> SelectedActivitySeverities { get; }

        #endregion

        #region Private Methods

        private static Guid GetNextId() => Guid.NewGuid();

        private void SetSelectedManagedActivitySeverities(SelectionChangedEventArgs args)
        {
            lock (m_Lock)
            {
                if (args.AddedItems is not null)
                {
                    foreach (var managedActivitySeverityViewModel in args.AddedItems.OfType<IManagedActivitySeverityViewModel>())
                    {
                        SelectedActivitySeverities.TryAdd(managedActivitySeverityViewModel.Id, managedActivitySeverityViewModel);
                    }
                }
                if (args.RemovedItems is not null)
                {
                    foreach (var managedActivitySeverityViewModel in args.RemovedItems.OfType<IManagedActivitySeverityViewModel>())
                    {
                        SelectedActivitySeverities.Remove(managedActivitySeverityViewModel.Id);
                    }
                }

                HasActivitySeverities = SelectedActivitySeverities.Any();
            }
        }

        private async Task AddManagedActivitySeverityAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    Guid id = GetNextId();
                    m_ActivitySeverities.Add(
                        new ManagedActivitySeverityViewModel(
                            this,
                            id,
                            new ActivitySeverityModel
                            {
                                ColorFormat = ColorHelper.Random()
                            }));
                }
                UpdateArrowGraphSettingsToCore();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task RemoveManagedActivitySeveritiesAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    ICollection<IManagedActivitySeverityViewModel> activitySeverities = SelectedActivitySeverities.Values;

                    if (activitySeverities.Count == 0)
                    {
                        return;
                    }

                    foreach (IManagedActivitySeverityViewModel activitySeverity in activitySeverities)
                    {
                        m_ActivitySeverities.Remove(activitySeverity);
                        activitySeverity.Dispose();
                    }
                }

                CheckForMaxSlackLimit();
                UpdateArrowGraphSettingsToCore();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private void UpdateArrowGraphSettingsToCore()
        {
            lock (m_Lock)
            {
                var arrowGraphSettings = new ArrowGraphSettingsModel
                {
                    ActivitySeverities = ActivitySeverities.Select(x => new ActivitySeverityModel
                    {
                        SlackLimit = x.SlackLimit,
                        CriticalityWeight = x.CriticalityWeight,
                        FibonacciWeight = x.FibonacciWeight,
                        ColorFormat = x.ColorFormat
                    }).ToList(),
                    EdgeTypeFormats = m_EdgeTypeFormats.Select(x => new EdgeTypeFormatModel
                    {
                        EdgeType = x.EdgeType,
                        EdgeDashStyle = x.EdgeDashStyle,
                        EdgeWeightStyle = x.EdgeWeightStyle
                    }).ToList()
                };

                if (m_Current != arrowGraphSettings)
                {
                    m_Current = arrowGraphSettings;
                    m_CoreViewModel.ArrowGraphSettings = arrowGraphSettings;
                }
            }
            AreSettingsUpdated = false;
        }

        private void ProcessSettings(ArrowGraphSettingsModel arrowGraphSettings)
        {
            ArgumentNullException.ThrowIfNull(arrowGraphSettings);
            lock (m_Lock)
            {
                m_EdgeTypeFormats.Clear();
                foreach (EdgeTypeFormatModel edgeTypeFormat in arrowGraphSettings.EdgeTypeFormats)
                {
                    m_EdgeTypeFormats.Add(new EdgeTypeFormatModel
                    {
                        EdgeType = edgeTypeFormat.EdgeType,
                        EdgeDashStyle = edgeTypeFormat.EdgeDashStyle,
                        EdgeWeightStyle = edgeTypeFormat.EdgeWeightStyle
                    });
                }

                ClearManagedActivitySeverities();

                foreach (ActivitySeverityModel activitySeverity in arrowGraphSettings.ActivitySeverities)
                {
                    m_ActivitySeverities.Add(new ManagedActivitySeverityViewModel(
                        this,
                        GetNextId(),
                        activitySeverity));
                }

                CheckForMaxSlackLimit();
            }
            AreSettingsUpdated = false;
        }

        private void ClearManagedActivitySeverities()
        {
            lock (m_Lock)
            {
                foreach (IManagedActivitySeverityViewModel activitySeverity in m_ActivitySeverities)
                {
                    activitySeverity.Dispose();
                }
                m_ActivitySeverities.Clear();
            }
        }

        private void CheckForMaxSlackLimit()
        {
            lock (m_Lock)
            {
                if (!m_ActivitySeverities.Any(x => x.SlackLimit == int.MaxValue))
                {
                    m_ActivitySeverities.Add(new ManagedActivitySeverityViewModel(
                        this,
                        GetNextId(),
                        new ActivitySeverityModel
                        {
                            SlackLimit = int.MaxValue,
                            CriticalityWeight = 1.0,
                            FibonacciWeight = 1.0,
                            ColorFormat = ColorHelper.Green()
                        }));
                }
            }
        }

        #endregion

        #region IArrowGraphSettingsManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        private bool m_HasActivitySeverities;
        public bool HasActivitySeverities
        {
            get => m_HasActivitySeverities;
            set
            {
                lock (m_Lock)
                {
                    m_HasActivitySeverities = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        private bool m_AreSettingsUpdated;
        public bool AreSettingsUpdated
        {
            get => m_AreSettingsUpdated;
            set => this.RaiseAndSetIfChanged(ref m_AreSettingsUpdated, value);
        }

        private readonly ObservableCollection<IManagedActivitySeverityViewModel> m_ActivitySeverities;
        private readonly ReadOnlyObservableCollection<IManagedActivitySeverityViewModel> m_ReadOnlyActivitySeverities;
        public ReadOnlyObservableCollection<IManagedActivitySeverityViewModel> ActivitySeverities => m_ReadOnlyActivitySeverities;

        public ICommand SetSelectedManagedActivitySeveritiesCommand { get; }

        public ICommand AddManagedActivitySeverityCommand { get; }

        public ICommand RemoveManagedActivitySeveritiesCommand { get; }

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
                m_IsBusy?.Dispose();
                m_HasStaleOutputs?.Dispose();
                m_HasCompilationErrors?.Dispose();
                m_ProcessArrowGraphSettingsSub?.Dispose();
                m_UpdateArrowGraphSettingsSub?.Dispose();
                ClearManagedActivitySeverities();
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
