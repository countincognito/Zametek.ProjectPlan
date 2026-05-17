using Avalonia.Controls;
using Avalonia.Data;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

        private readonly Lock m_Lock;
        private ResourceSettingsModel m_Current;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;

        private readonly IDisposable? m_ReadOnlyResourcesSub;
        private readonly IDisposable? m_OrderableResourcesSub;
        private readonly IDisposable? m_ProcessResourceSettingsSub;
        private readonly IDisposable? m_UpdateResourceSettingsSub;

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
            m_Lock = new();
            m_Current = new ResourceSettingsModel();
            m_CoreViewModel = coreViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            SelectedResources = new ConcurrentDictionary<int, IManagedResourceViewModel>();
            m_HasSelectedResource = false;
            m_HasSelectedResources = false;
            m_AreSettingsUpdated = false; ;

            m_Resources = new();

            m_OrderableResources = [];

            SetSelectedManagedResourcesCommand = ReactiveCommand.Create<SelectionChangedEventArgs>(SetSelectedManagedResources);
            AddManagedResourceCommand = ReactiveCommand.CreateFromTask(
                AddManagedResourceAsync,
                this.WhenAnyValue(
                    rm => rm.DisableResources,
                    (disabled) => !disabled));
            RemoveManagedResourcesCommand = ReactiveCommand.CreateFromTask(
                RemoveManagedResourcesAsync,
                this.WhenAnyValue(
                    rm => rm.HasSelectedResources,
                    rm => rm.DisableResources,
                    (hasSelectedResources, disabled) => hasSelectedResources && !disabled));
            DuplicateManagedResourceCommand = ReactiveCommand.CreateFromTask(
                DuplicateManagedResourceAsync,
                this.WhenAnyValue(
                    rm => rm.HasSelectedResource,
                    rm => rm.DisableResources,
                    (hasSelectedResource, disabled) => hasSelectedResource && !disabled));
            EditManagedResourcesCommand = ReactiveCommand.CreateFromTask(
                EditManagedResourcesAsync,
                this.WhenAnyValue(
                    rm => rm.HasSelectedResources,
                    rm => rm.DisableResources,
                    (hasSelectedResources, disabled) => hasSelectedResources && !disabled));
            RenumberResourcesCommand = ReactiveCommand.CreateFromTask(
                RenumberResourcesAsync,
                this.WhenAnyValue(
                    rm => rm.DisableResources,
                    (disabled) => !disabled));

            // Create read-only view to the source list.
            m_ReadOnlyResourcesSub = m_Resources.Connect()
               .ObserveOn(RxSchedulers.MainThreadScheduler)
               .Bind(out m_ReadOnlyResources)
               .Subscribe();

            m_OrderableResourcesSub = m_Resources.Connect()
               //.ObserveOn(Scheduler.CurrentThread)
               .ObserveOn(RxSchedulers.MainThreadScheduler) // Ensure UI thread safety
               .Bind(m_OrderableResources)          // Bind to the mutable collection
               .DisposeMany()                        // Clean up resources
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
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(rs =>
                {
                    if (m_Current != rs)
                    {
                        ProcessSettings(rs);
                    }
                });

            m_UpdateResourceSettingsSub = this
                .WhenAnyValue(rsm => rsm.AreSettingsUpdated)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(areUpdated =>
                {
                    if (areUpdated)
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
                return RawResources.Select(x => x.Id).DefaultIfEmpty().Max() + 1;
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
                        SelectedResources[managedResourceViewModel.Id] = managedResourceViewModel;
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
                HasSelectedResource = HasSelectedResources && SelectedResources.Count == 1;
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
                                    DisplayOrder = -1,
                                    IsExplicitTarget = false,
                                    IsInactive = false,
                                    UnitCost = DefaultUnitCost,
                                    UnitBilling = DefaultUnitBilling,
                                    FixedCost = 0.0,
                                    FixedBilling = 0.0,
                                    ColorFormat = ColorHelper.Random(),
                                    Trackers = []
                                }));
                    });

                    UpdateDisplayOrders();
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

                        foreach (IManagedResourceViewModel resource in selectedResources)
                        {
                            resources.Remove(resource);
                            resource.Dispose();
                        }
                    });

                    UpdateDisplayOrders();
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

        private async Task DuplicateManagedResourceAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    SelectedResources.TryGetValue(SelectedResources.Keys.First(), out IManagedResourceViewModel? selectedResource);

                    if (selectedResource is null)
                    {
                        return;
                    }

                    ResourceModel duplicateModel = selectedResource.DeepCopy();

                    m_Resources.Edit(resources =>
                    {
                        int id = GetNextId();

                        // Clear the trackers because otherwise we would need to alter
                        // all the IDs to correspond with the new ID.
                        duplicateModel = duplicateModel with
                        {
                            Id = id,
                            Trackers = [],
                        };

                        resources.Add(
                            new ManagedResourceViewModel(
                                m_CoreViewModel,
                                this,
                                duplicateModel));
                    });

                    UpdateDisplayOrders();
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
                    title: Resource.ProjectPlan.Titles.Title_EditResources,
                    header: string.Empty,
                    message: $@"**{Resource.ProjectPlan.Messages.Message_EditResources}**",
                    context: editViewModel,
                    markdown: true);

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

                    IEnumerable<UpdateResourceModel> updateModels = [.. resourceIds.Select(x => updateModel with { Id = x })];

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

        private async Task RenumberResourcesAsync()
        {
            try
            {
                await RenumberResourcesInternalAsync();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task RenumberResourcesInternalAsync() =>
            await Task.Run(RenumberResourcesInternal);

        private void RenumberResourcesInternal()
        {
            lock (m_Lock)
            {
                UpdateDisplayOrders();

                List<(int oldId, int newId)> mappedIds = [];

                int count = OrderableResources.Count;

                for (int i = 0; i < count; i++)
                {
                    int oldId = OrderableResources[i].Id;
                    int newId = i + 1;
                    mappedIds.Add((oldId, newId));
                }

                m_CoreViewModel.UpdateManagedResourceIds(mappedIds);
                m_CoreViewModel.IsReadyToReviseTrackers = ReadyToRevise.Yes;
            }
            m_CoreViewModel.RunAutoCompile();
        }

        private void UpdateManagedResources(IEnumerable<UpdateResourceModel> updateModels)
        {
            lock (m_Lock)
            {
                Dictionary<int, IManagedResourceViewModel> resourceLookup = RawResources.ToDictionary(x => x.Id);

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
                            if (updateModel.IsNotesEdited)
                            {
                                resource.Notes = updateModel.Notes;
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
                            if (updateModel.IsFixedCostEdited)
                            {
                                resource.FixedCost = updateModel.FixedCost;
                            }
                            if (updateModel.IsFixedBillingEdited)
                            {
                                resource.FixedBilling = updateModel.FixedBilling;
                            }
                            if (updateModel.IsColorFormatActive)
                            {
                                resource.ColorFormat = updateModel.ColorFormat;
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
                UpdateDisplayOrders();

                var resourceSettings = new ResourceSettingsModel
                {
                    Resources = [.. RawResources.Select(x => x.DeepCopy())],
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

        private void UpdateDisplayOrders()
        {
            // Mark the display order in reverse order of the list because
            // the UI renders in that order and we want to reflect that.
            int resourceCount = OrderableResources.Count;

            for (int i = 0; i < resourceCount; i++)
            {
                OrderableResources[i].DisplayOrder = resourceCount - i - 1;
            }
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

                // Add the resources in descending order because this is how
                // the UI renders in that order and we want to reflect that.
                IOrderedEnumerable<ResourceModel> orderedResourceModels = resourceSettings.Resources
                     .OrderByDescending(x => x.DisplayOrder)
                     .ThenByDescending(x => x.Id);

                m_Resources.Edit(resources =>
                {
                    foreach (ResourceModel resouce in orderedResourceModels)
                    {
                        resources.Add(new ManagedResourceViewModel(
                            m_CoreViewModel,
                            this,
                            resouce));
                    }
                });

                UpdateDisplayOrders();
            }
            AreSettingsUpdated = false;
        }

        private void ClearManagedResources()
        {
            lock (m_Lock)
            {
                m_Resources.Edit(resources =>
                {
                    foreach (IManagedResourceViewModel resource in RawResources)
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

        private bool m_HasSelectedResource;
        public bool HasSelectedResource
        {
            get => m_HasSelectedResource;
            set
            {
                lock (m_Lock)
                {
                    m_HasSelectedResource = value;
                    this.RaisePropertyChanged();
                }
            }
        }

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
        public IReadOnlyList<IManagedResourceViewModel> RawResources => m_Resources.Items;

        private readonly ReadOnlyObservableCollection<IManagedResourceViewModel> m_ReadOnlyResources;
        public ReadOnlyObservableCollection<IManagedResourceViewModel> Resources => m_ReadOnlyResources;

        private readonly ObservableCollectionExtended<IManagedResourceViewModel> m_OrderableResources;
        public ObservableCollection<IManagedResourceViewModel> OrderableResources => m_OrderableResources;

        public ICommand SetSelectedManagedResourcesCommand { get; }

        public ICommand AddManagedResourceCommand { get; }

        public ICommand RemoveManagedResourcesCommand { get; }

        public ICommand DuplicateManagedResourceCommand { get; }

        public ICommand EditManagedResourcesCommand { get; }

        public ICommand RenumberResourcesCommand { get; }

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
                m_IsBusy?.Dispose();
                m_HasStaleOutputs?.Dispose();
                m_HasCompilationErrors?.Dispose();
                m_HideCost?.Dispose();
                m_HideBilling?.Dispose();
                m_ReadOnlyResourcesSub?.Dispose();
                m_OrderableResourcesSub?.Dispose();
                m_ProcessResourceSettingsSub?.Dispose();
                m_UpdateResourceSettingsSub?.Dispose();
                ClearManagedResources();
                m_Resources?.Dispose();
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
