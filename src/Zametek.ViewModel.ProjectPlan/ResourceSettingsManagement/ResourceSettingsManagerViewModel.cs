using Avalonia.Controls;
using Avalonia.Data;
using DynamicData;
using ReactiveUI;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
            m_HasSelectedResources = false;
            m_AreSettingsUpdated = false; ;

            m_Resources = new();

            SetSelectedManagedResourcesCommand = ReactiveCommand.Create<SelectionChangedEventArgs>(SetSelectedManagedResources);
            AddManagedResourceCommand = ReactiveCommand.CreateFromTask(AddManagedResourceAsync);
            RemoveManagedResourcesCommand = ReactiveCommand.CreateFromTask(RemoveManagedResourcesAsync, this.WhenAnyValue(rm => rm.HasSelectedResources));
            EditManagedResourcesCommand = ReactiveCommand.CreateFromTask(EditManagedResourcesAsync, this.WhenAnyValue(am => am.HasSelectedResources));

            // Create read-only view to the source list.
            m_Resources.Connect()
               .ObserveOn(RxApp.MainThreadScheduler)
               .Bind(out m_ReadOnlyResources)
               .Subscribe();

            m_IsBusy = this
                .WhenAnyValue(rsm => rsm.m_CoreViewModel.IsBusy)
                .ToProperty(this, rsm => rsm.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(rsm => rsm.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, rsm => rsm.HasStaleOutputs);

            m_HasCompilationErrors = this
                .WhenAnyValue(rsm => rsm.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, rsm => rsm.HasCompilationErrors);

            m_HideCost = this
                .WhenAnyValue(rsm => rsm.m_CoreViewModel.DisplaySettingsViewModel.HideCost)
                .ToProperty(this, rsm => rsm.HideCost);

            m_HideBilling = this
                .WhenAnyValue(rsm => rsm.m_CoreViewModel.DisplaySettingsViewModel.HideBilling)
                .ToProperty(this, rsm => rsm.HideBilling);

            m_ProcessResourceSettingsSub = this
                .WhenAnyValue(rsm => rsm.m_CoreViewModel.ResourceSettings)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(rs =>
                {
                    if (m_Current != rs)
                    {
                        ProcessSettings(rs);
                    }
                });

            m_UpdateResourceSettingsSub = this
                .WhenAnyValue(rsm => rsm.AreSettingsUpdated)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(areUpdated =>
                {
                    if (areUpdated)
                    {
                        UpdateResourceSettingsToCore();
                    }
                });

            m_ReviseSettingsSub = this
                .WhenAnyValue(rsm => rsm.m_CoreViewModel.IsReadyToReviseSettings)
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

                HasSelectedResources = SelectedResources.Any();
            }
        }

        private async Task AddManagedResourceAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    m_Resources.Edit(resources =>
                    {
                        int resourceId = GetNextId();
                        resources.Add(
                            new ManagedResourceViewModel(
                                m_CoreViewModel,
                                this,
                                new ResourceModel
                                {
                                    Id = resourceId,
                                    IsExplicitTarget = false,
                                    IsInactive = false,
                                    UnitCost = DefaultUnitCost,
                                    UnitBilling = DefaultUnitBilling,
                                    ColorFormat = ColorHelper.Random(),
                                    Trackers = []
                                }));
                    });
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
                    m_Resources.Edit(resources =>
                    {
                        ICollection<IManagedResourceViewModel> selectedResources = SelectedResources.Values;

                        if (selectedResources.Count == 0)
                        {
                            return;
                        }

                        foreach (IManagedResourceViewModel resouce in selectedResources)
                        {
                            resources.Remove(resouce);
                            resouce.Dispose();
                        }
                    });
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

        private async Task EditManagedResourcesAsync()
        {
            try
            {
                var editViewModel = new ResourceEditViewModel(m_CoreViewModel.WorkStreamSettings.WorkStreams);

                bool result = await m_DialogService.ShowContextAsync(
                    Resource.ProjectPlan.Titles.Title_EditResources,
                    editViewModel);

                if (!result)
                {
                    return;
                }

                lock (m_Lock)
                {
                    ICollection<int> resourceIds = SelectedResources.Keys;

                    if (resourceIds.Count == 0)
                    {
                        return;
                    }

                    UpdateResourceModel updateModel = editViewModel.BuildUpdateModel();

                    IEnumerable<UpdateResourceModel> updateModels = resourceIds
                        .Select(x => updateModel with { Id = x })
                        .ToList();

                    UpdateManagedResources(updateModels);
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

        private void UpdateManagedResources(IEnumerable<UpdateResourceModel> updateModels)
        {
            lock (m_Lock)
            {
                Dictionary<int, IManagedResourceViewModel> resourceLookup = Resources.ToDictionary(x => x.Id);

                foreach (UpdateResourceModel updateModel in updateModels)
                {
                    if (resourceLookup.TryGetValue(updateModel.Id, out IManagedResourceViewModel? resource))
                    {
                        if (resource is IEditableObject editable)
                        {
                            resource.IsEditMuted = true;
                            editable.BeginEdit();

                            if (updateModel.IsNameEdited)
                            {
                                resource.Name = updateModel.Name;
                            }
                            if (updateModel.IsIsExplicitTargetEdited)
                            {
                                resource.IsExplicitTarget = updateModel.IsExplicitTarget;
                            }
                            if (updateModel.IsIsInactiveEdited)
                            {
                                resource.IsInactive = updateModel.IsInactive;
                            }
                            if (updateModel.IsInterActivityAllocationTypeEdited)
                            {
                                resource.InterActivityAllocationType = updateModel.InterActivityAllocationType;
                            }
                            if (updateModel.IsUnitCostEdited)
                            {
                                resource.UnitCost = updateModel.UnitCost;
                            }
                            if (updateModel.IsUnitBillingEdited)
                            {
                                resource.UnitBilling = updateModel.UnitBilling;
                            }
                            if (updateModel.IsInterActivityPhasesEdited)
                            {
                                resource.WorkStreamSelector.SetSelectedTargetWorkStreams([.. updateModel.InterActivityPhases]);
                            }

                            editable.EndEdit();
                            resource.IsEditMuted = false;
                        }
                    }
                }
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
                        UnitBilling = x.UnitBilling,
                        DisplayOrder = x.DisplayOrder,
                        ColorFormat = x.ColorFormat,
                        Trackers = x.TrackerSet.Trackers,
                    }).ToList(),

                    DefaultUnitCost = DefaultUnitCost,
                    DefaultUnitBilling = DefaultUnitBilling,
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

                m_DefaultUnitBilling = resourceSettings.DefaultUnitBilling;
                this.RaisePropertyChanged(nameof(DefaultUnitBilling));

                m_DisableResources = resourceSettings.AreDisabled;
                this.RaisePropertyChanged(nameof(DisableResources));

                ClearManagedResources();

                m_Resources.Edit(resources =>
                {
                    foreach (ResourceModel resouce in resourceSettings.Resources)
                    {
                        resources.Add(new ManagedResourceViewModel(
                            m_CoreViewModel,
                            this,
                            resouce));
                    }
                });
            }
            AreSettingsUpdated = false;
        }

        private void ClearManagedResources()
        {
            lock (m_Lock)
            {
                m_Resources.Edit(resources =>
                {
                    foreach (IManagedResourceViewModel resource in Resources)
                    {
                        resource.Dispose();
                    }
                    resources.Clear();
                });
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

        private readonly ObservableAsPropertyHelper<bool> m_HideCost;
        public bool HideCost => m_HideCost.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HideBilling;
        public bool HideBilling => m_HideBilling.Value;

        private bool m_HasSelectedResources;
        public bool HasSelectedResources
        {
            get => m_HasSelectedResources;
            set
            {
                lock (m_Lock)
                {
                    m_HasSelectedResources = value;
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
                    throw new DataValidationException(Resource.ProjectPlan.Messages.Message_UnitCostMustBeZeroOrGreater);
                }

                if (m_DefaultUnitCost != value)
                {
                    this.RaiseAndSetIfChanged(ref m_DefaultUnitCost, value);
                    AreSettingsUpdated = true;
                }
            }
        }

        private double m_DefaultUnitBilling;
        public double DefaultUnitBilling
        {
            get => m_DefaultUnitBilling;
            set
            {
                if (value < 0)
                {
                    throw new DataValidationException(Resource.ProjectPlan.Messages.Message_UnitBillingMustBeZeroOrGreater);
                }

                if (m_DefaultUnitBilling != value)
                {
                    this.RaiseAndSetIfChanged(ref m_DefaultUnitBilling, value);
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

        private readonly SourceList<IManagedResourceViewModel> m_Resources;
        private readonly ReadOnlyObservableCollection<IManagedResourceViewModel> m_ReadOnlyResources;
        public ReadOnlyObservableCollection<IManagedResourceViewModel> Resources => m_ReadOnlyResources;

        public ICommand SetSelectedManagedResourcesCommand { get; }

        public ICommand AddManagedResourceCommand { get; }

        public ICommand RemoveManagedResourcesCommand { get; }

        public ICommand EditManagedResourcesCommand { get; }

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
                m_HideCost?.Dispose();
                m_HideBilling?.Dispose();
                m_ProcessResourceSettingsSub?.Dispose();
                m_UpdateResourceSettingsSub?.Dispose();
                m_ReviseSettingsSub?.Dispose();
                ClearManagedResources();
                m_Resources?.Dispose();
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
