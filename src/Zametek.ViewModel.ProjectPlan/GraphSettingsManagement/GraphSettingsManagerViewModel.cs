using Avalonia.Controls;
using DynamicData;
using ReactiveUI;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class GraphSettingsManagerViewModel
        : ToolViewModelBase, IGraphSettingsManagerViewModel
    {
        #region Fields

        private readonly Lock m_Lock;
        private GraphSettingsModel m_Current;
        private readonly IList<EdgeTypeFormatModel> m_EdgeTypeFormats;
        private readonly IList<NodeTypeFormatModel> m_NodeTypeFormats;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;

        private readonly IDisposable? m_ReadOnlyActivitySeveritiesSub;
        private readonly IDisposable? m_ProcessGraphSettingsSub;
        private readonly IDisposable? m_UpdateGraphSettingsSub;

        #endregion

        #region Ctors

        public GraphSettingsManagerViewModel(
            ICoreViewModel coreViewModel,
            ISettingService settingService,
            IDialogService dialogService)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            m_Lock = new();
            m_Current = new GraphSettingsModel();
            m_EdgeTypeFormats = [];
            m_NodeTypeFormats = [];
            m_CoreViewModel = coreViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            SelectedActivitySeverities = new ConcurrentDictionary<Guid, IManagedActivitySeverityViewModel>();
            m_HasActivitySeverities = false;
            m_AreSettingsUpdated = false; ;

            m_ActivitySeverities = new();

            SetSelectedManagedActivitySeveritiesCommand = ReactiveCommand.Create<SelectionChangedEventArgs>(SetSelectedManagedActivitySeverities);
            AddManagedActivitySeverityCommand = ReactiveCommand.CreateFromTask(AddManagedActivitySeverityAsync);
            RemoveManagedActivitySeveritiesCommand = ReactiveCommand.CreateFromTask(RemoveManagedActivitySeveritiesAsync, this.WhenAnyValue(agsm => agsm.HasActivitySeverities));

            // Create read-only view to the source list.
            m_ReadOnlyActivitySeveritiesSub = m_ActivitySeverities.Connect()
               .ObserveOn(RxApp.MainThreadScheduler)
               .Bind(out m_ReadOnlyActivitySeverities)
               .Subscribe();

            m_IsBusy = this
                .WhenAnyValue(agsm => agsm.m_CoreViewModel.IsBusy)
                .ToProperty(this, agsm => agsm.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(agsm => agsm.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, agsm => agsm.HasStaleOutputs);

            m_HasCompilationErrors = this
                .WhenAnyValue(agsm => agsm.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, agsm => agsm.HasCompilationErrors);

            m_ProcessGraphSettingsSub = this
                .WhenAnyValue(agsm => agsm.m_CoreViewModel.GraphSettings)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(rs =>
                {
                    if (m_Current != rs)
                    {
                        ProcessSettings(rs);
                    }
                });

            m_UpdateGraphSettingsSub = this
                .WhenAnyValue(agsm => agsm.AreSettingsUpdated)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(areUpdated =>
                {
                    if (areUpdated)
                    {
                        UpdateGraphSettingsToCore();
                    }
                });

            ProcessSettings(m_SettingService.DefaultGraphSettings);

            Id = Resource.ProjectPlan.Titles.Title_GraphSettingsView;
            Title = Resource.ProjectPlan.Titles.Title_GraphSettingsView;
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
                        SelectedActivitySeverities[managedActivitySeverityViewModel.Id] = managedActivitySeverityViewModel;
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
                    m_ActivitySeverities.Edit(activitySeverities =>
                    {
                        Guid id = GetNextId();
                        activitySeverities.Add(
                            new ManagedActivitySeverityViewModel(
                                this,
                                id,
                                new ActivitySeverityModel
                                {
                                    ColorFormat = ColorHelper.Random()
                                }));
                    });
                }
                UpdateGraphSettingsToCore();
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
                    m_ActivitySeverities.Edit(activitySeverities =>
                    {
                        ICollection<IManagedActivitySeverityViewModel> selectedActivitySeverities = SelectedActivitySeverities.Values;

                        if (selectedActivitySeverities.Count == 0)
                        {
                            return;
                        }

                        foreach (IManagedActivitySeverityViewModel activitySeverity in selectedActivitySeverities)
                        {
                            activitySeverities.Remove(activitySeverity);
                            activitySeverity.Dispose();
                        }
                    });
                }

                CheckForMaxSlackLimit();
                UpdateGraphSettingsToCore();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private void UpdateGraphSettingsToCore()
        {
            lock (m_Lock)
            {
                var graphSettings = new GraphSettingsModel
                {
                    ActivitySeverities = [.. RawActivitySeverities.Select(x => new ActivitySeverityModel
                    {
                        SlackLimit = x.SlackLimit,
                        CriticalityWeight = x.CriticalityWeight,
                        FibonacciWeight = x.FibonacciWeight,
                        ColorFormat = x.ColorFormat
                    })],
                    EdgeTypeFormats = [.. m_EdgeTypeFormats.Select(x => new EdgeTypeFormatModel
                    {
                        EdgeType = x.EdgeType,
                        EdgeDashStyle = x.EdgeDashStyle,
                        EdgeWeightStyle = x.EdgeWeightStyle
                    })],
                    NodeTypeFormats = [.. m_NodeTypeFormats.Select(x => new NodeTypeFormatModel
                    {
                        NodeType = x.NodeType,
                        NodeBorderDashStyle = x.NodeBorderDashStyle,
                        NodeBorderWeightStyle = x.NodeBorderWeightStyle
                    })]
                };

                if (m_Current != graphSettings)
                {
                    m_Current = graphSettings;
                    m_CoreViewModel.GraphSettings = graphSettings;
                }
            }
            AreSettingsUpdated = false;
        }

        private void ProcessSettings(GraphSettingsModel graphSettings)
        {
            ArgumentNullException.ThrowIfNull(graphSettings);
            lock (m_Lock)
            {
                m_NodeTypeFormats.Clear();
                foreach (EdgeTypeFormatModel edgeTypeFormat in graphSettings.EdgeTypeFormats)
                {
                    m_EdgeTypeFormats.Add(new EdgeTypeFormatModel
                    {
                        EdgeType = edgeTypeFormat.EdgeType,
                        EdgeDashStyle = edgeTypeFormat.EdgeDashStyle,
                        EdgeWeightStyle = edgeTypeFormat.EdgeWeightStyle
                    });
                }
                m_NodeTypeFormats.Clear();
                foreach (NodeTypeFormatModel nodeTypeFormat in graphSettings.NodeTypeFormats)
                {
                    m_NodeTypeFormats.Add(new NodeTypeFormatModel
                    {
                        NodeType = nodeTypeFormat.NodeType,
                        NodeBorderDashStyle = nodeTypeFormat.NodeBorderDashStyle,
                        NodeBorderWeightStyle = nodeTypeFormat.NodeBorderWeightStyle
                    });
                }

                ClearManagedActivitySeverities();

                m_ActivitySeverities.Edit(activitySeverities =>
                {
                    foreach (ActivitySeverityModel activitySeverity in graphSettings.ActivitySeverities)
                    {
                        activitySeverities.Add(new ManagedActivitySeverityViewModel(
                            this,
                            GetNextId(),
                            activitySeverity));
                    }
                });

                CheckForMaxSlackLimit();
            }
            AreSettingsUpdated = false;
        }

        private void ClearManagedActivitySeverities()
        {
            lock (m_Lock)
            {
                m_ActivitySeverities.Edit(activitySeverities =>
                {
                    foreach (IManagedActivitySeverityViewModel activitySeverity in RawActivitySeverities)
                    {
                        activitySeverity.Dispose();
                    }
                    activitySeverities.Clear();
                });
            }
        }

        private void CheckForMaxSlackLimit()
        {
            lock (m_Lock)
            {
                m_ActivitySeverities.Edit(activitySeverities =>
                {
                    if (!RawActivitySeverities.Any(x => x.SlackLimit == int.MaxValue))
                    {
                        activitySeverities.Add(new ManagedActivitySeverityViewModel(
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
                });
            }
        }

        #endregion

        #region IGraphSettingsManagerViewModel Members

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

        private readonly SourceList<IManagedActivitySeverityViewModel> m_ActivitySeverities;
        public IReadOnlyList<IManagedActivitySeverityViewModel> RawActivitySeverities => m_ActivitySeverities.Items;

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
                m_ReadOnlyActivitySeveritiesSub?.Dispose();
                m_ProcessGraphSettingsSub?.Dispose();
                m_UpdateGraphSettingsSub?.Dispose();
                ClearManagedActivitySeverities();
                m_ActivitySeverities?.Dispose();
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
