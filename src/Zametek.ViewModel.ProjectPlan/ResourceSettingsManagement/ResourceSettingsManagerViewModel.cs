using Avalonia.Controls;
using Avalonia.Data;
using ReactiveUI;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ResourceSettingsManagerViewModel
        : ToolViewModelBase, IResourceSettingsManagerViewModel, IDisposable
    {
        #region Fields

        private readonly object m_Lock;
        private ResourceSettingsModel m_Current;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;

        private readonly IDisposable? m_ProcessResourceSettingsSub;
        private readonly IDisposable? m_UpdateResourceSettingsSub;
        private readonly IDisposable? m_ReviseSettingsSub;

        #endregion

        #region Ctors

        public ResourceSettingsManagerViewModel(
            ICoreViewModel coreViewModel,
            ISettingService settingService,
            IDialogService dialogService)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            m_Lock = new object();
            m_Current = new ResourceSettingsModel();
            m_CoreViewModel = coreViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            SelectedResources = new ConcurrentDictionary<int, IManagedResourceViewModel>();
            m_HasResources = false;
            m_AreSettingsUpdated = false; ;

            m_Resources = [];
            m_ReadOnlyResources = new ReadOnlyObservableCollection<IManagedResourceViewModel>(m_Resources);

            SetSelectedManagedResourcesCommand = ReactiveCommand.Create<SelectionChangedEventArgs>(SetSelectedManagedResources);
            AddManagedResourceCommand = ReactiveCommand.CreateFromTask(AddManagedResourceAsync);
            RemoveManagedResourcesCommand = ReactiveCommand.CreateFromTask(RemoveManagedResourcesAsync, this.WhenAnyValue(rm => rm.HasResources));

            m_IsBusy = this
                .WhenAnyValue(rm => rm.m_CoreViewModel.IsBusy)
                .ToProperty(this, rm => rm.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(rm => rm.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, rm => rm.HasStaleOutputs);

            m_HasCompilationErrors = this
                .WhenAnyValue(rm => rm.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, rm => rm.HasCompilationErrors);

            m_ProcessResourceSettingsSub = this
                .WhenAnyValue(rm => rm.m_CoreViewModel.ResourceSettings)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(rs =>
                {
                    if (m_Current != rs)
                    {
                        ProcessSettings(rs);
                    }
                });

            m_UpdateResourceSettingsSub = this
                .WhenAnyValue(rm => rm.AreSettingsUpdated)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(areUpdated =>
                {
                    if (areUpdated)
                    {
                        UpdateResourceSettingsToCore();
                    }
                });

            m_ReviseSettingsSub = this
                .WhenAnyValue(x => x.m_CoreViewModel.IsReadyToReviseSettings)
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(isReadyToRevise =>
                {
                    if (isReadyToRevise == ReadyToRevise.Yes)
                    {
                        UpdateResourceSettingsToCore();
                    }
                });

            ProcessSettings(m_SettingService.DefaultResourceSettings);

            Id = Resource.ProjectPlan.Titles.Title_ResourceSettingsView;
            Title = Resource.ProjectPlan.Titles.Title_ResourceSettingsView;
        }

        #endregion

        #region Properties

        public IDictionary<int, IManagedResourceViewModel> SelectedResources { get; }

        #endregion

        #region Private Methods

        private int GetNextId()
        {
            lock (m_Lock)
            {
                return Resources.Select(x => x.Id).DefaultIfEmpty().Max() + 1;
            }
        }

        private void SetSelectedManagedResources(SelectionChangedEventArgs args)
        {
            lock (m_Lock)
            {
                if (args.AddedItems is not null)
                {
                    foreach (var managedResourceViewModel in args.AddedItems.OfType<IManagedResourceViewModel>())
                    {
                        SelectedResources.TryAdd(managedResourceViewModel.Id, managedResourceViewModel);
                    }
                }
                if (args.RemovedItems is not null)
                {
                    foreach (var managedResourceViewModel in args.RemovedItems.OfType<IManagedResourceViewModel>())
                    {
                        SelectedResources.Remove(managedResourceViewModel.Id);
                    }
                }

                HasResources = SelectedResources.Any();
            }
        }

        private async Task AddManagedResourceAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    int resourceId = GetNextId();
                    m_Resources.Add(
                        new ManagedResourceViewModel(
                            m_CoreViewModel,
                            this,
                            new ResourceModel
                            {
                                Id = resourceId,
                                IsExplicitTarget = true,
                                IsInactive = false,
                                UnitCost = DefaultUnitCost,
                                ColorFormat = ColorHelper.Random(),
                                Trackers = []
                            }));
                }
                UpdateResourceSettingsToCore();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task RemoveManagedResourcesAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    ICollection<IManagedResourceViewModel> resources = SelectedResources.Values;

                    if (resources.Count == 0)
                    {
                        return;
                    }

                    foreach (IManagedResourceViewModel resouce in resources)
                    {
                        m_Resources.Remove(resouce);
                        resouce.Dispose();
                    }
                }
                UpdateResourceSettingsToCore();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private void UpdateResourceSettingsToCore()
        {
            lock (m_Lock)
            {
                var resourceSettings = new ResourceSettingsModel
                {
                    Resources = Resources.Select(x => new ResourceModel
                    {
                        Id = x.Id,
                        Name = x.Name,
                        IsExplicitTarget = x.IsExplicitTarget,
                        IsInactive = x.IsInactive,
                        InterActivityAllocationType = x.InterActivityAllocationType,
                        InterActivityPhases = [.. x.InterActivityPhases],
                        UnitCost = x.UnitCost,
                        DisplayOrder = x.DisplayOrder,
                        ColorFormat = x.ColorFormat,
                        Trackers = x.TrackerSet.Trackers,
                    }).ToList(),

                    DefaultUnitCost = DefaultUnitCost,
                    AreDisabled = DisableResources
                };

                if (m_Current != resourceSettings)
                {
                    m_Current = resourceSettings;
                    m_CoreViewModel.ResourceSettings = m_Current;
                }
            }
            AreSettingsUpdated = false;
        }

        private void ProcessSettings(ResourceSettingsModel resourceSettings)
        {
            ArgumentNullException.ThrowIfNull(resourceSettings);
            lock (m_Lock)
            {
                m_DefaultUnitCost = resourceSettings.DefaultUnitCost;
                this.RaisePropertyChanged(nameof(DefaultUnitCost));

                m_DisableResources = resourceSettings.AreDisabled;
                this.RaisePropertyChanged(nameof(DisableResources));

                ClearManagedResources();

                foreach (ResourceModel resouce in resourceSettings.Resources)
                {
                    m_Resources.Add(new ManagedResourceViewModel(
                        m_CoreViewModel,
                        this,
                        resouce));
                }
            }
            AreSettingsUpdated = false;
        }

        private void ClearManagedResources()
        {
            lock (m_Lock)
            {
                foreach (IManagedResourceViewModel resource in m_Resources)
                {
                    resource.Dispose();
                }
                m_Resources.Clear();
            }
        }

        #endregion

        #region IResourceSettingsManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        private bool m_HasResources;
        public bool HasResources
        {
            get => m_HasResources;
            set
            {
                lock (m_Lock)
                {
                    m_HasResources = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        private double m_DefaultUnitCost;
        public double DefaultUnitCost
        {
            get => m_DefaultUnitCost;
            set
            {
                if (value < 0)
                {
                    throw new DataValidationException(Resource.ProjectPlan.Messages.Message_UnitCostMustBeGreaterThanZero);
                }

                if (m_DefaultUnitCost != value)
                {
                    this.RaiseAndSetIfChanged(ref m_DefaultUnitCost, value);
                    AreSettingsUpdated = true;
                }
            }
        }

        private bool m_DisableResources;
        public bool DisableResources
        {
            get => m_DisableResources;
            set
            {
                if (m_DisableResources != value)
                {
                    this.RaiseAndSetIfChanged(ref m_DisableResources, value);
                    AreSettingsUpdated = true;
                }
            }
        }

        private bool m_AreSettingsUpdated;
        public bool AreSettingsUpdated
        {
            get => m_AreSettingsUpdated;
            set => this.RaiseAndSetIfChanged(ref m_AreSettingsUpdated, value);
        }

        private readonly ObservableCollection<IManagedResourceViewModel> m_Resources;
        private readonly ReadOnlyObservableCollection<IManagedResourceViewModel> m_ReadOnlyResources;
        public ReadOnlyObservableCollection<IManagedResourceViewModel> Resources => m_ReadOnlyResources;

        public ICommand SetSelectedManagedResourcesCommand { get; }

        public ICommand AddManagedResourceCommand { get; }

        public ICommand RemoveManagedResourcesCommand { get; }

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
                m_ProcessResourceSettingsSub?.Dispose();
                m_UpdateResourceSettingsSub?.Dispose();
                m_ReviseSettingsSub?.Dispose();
                ClearManagedResources();
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
