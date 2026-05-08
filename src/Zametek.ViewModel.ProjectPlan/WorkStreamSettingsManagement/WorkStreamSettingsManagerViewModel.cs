using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class WorkStreamSettingsManagerViewModel
        : ToolViewModelBase, IWorkStreamSettingsManagerViewModel
    {
        #region Fields

        private readonly Lock m_Lock;
        private WorkStreamSettingsModel m_Current;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IResourceSettingsManagerViewModel m_ResourceSettingsManagerViewModel;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;

        private readonly IDisposable? m_ReadOnlyWorkStreamsSub;
        private readonly IDisposable? m_OrderableWorkStreamsSub;
        private readonly IDisposable? m_ProcessWorkStreamSettingsSub;
        private readonly IDisposable? m_UpdateWorkStreamSettingsSub;

        #endregion

        #region Ctors

        public WorkStreamSettingsManagerViewModel(
            ICoreViewModel coreViewModel,
            IResourceSettingsManagerViewModel resourceSettingsManagerViewModel,
            ISettingService settingService,
            IDialogService dialogService)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(resourceSettingsManagerViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            m_Lock = new();
            m_Current = new WorkStreamSettingsModel();
            m_CoreViewModel = coreViewModel;
            m_ResourceSettingsManagerViewModel = resourceSettingsManagerViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            SelectedWorkStreams = new ConcurrentDictionary<int, IManagedWorkStreamViewModel>();
            m_HasSelectedWorkStreams = false;
            m_AreSettingsUpdated = false; ;

            m_WorkStreams = new();

            m_OrderableWorkStreams = [];

            SetSelectedManagedWorkStreamsCommand = ReactiveCommand.Create<SelectionChangedEventArgs>(SetSelectedManagedWorkStreams);
            AddManagedWorkStreamCommand = ReactiveCommand.CreateFromTask(AddManagedWorkStreamAsync);
            RemoveManagedWorkStreamsCommand = ReactiveCommand.CreateFromTask(RemoveManagedWorkStreamsAsync, this.WhenAnyValue(wssm => wssm.HasSelectedWorkStreams));
            DuplicateManagedWorkStreamCommand = ReactiveCommand.CreateFromTask(DuplicateManagedWorkStreamAsync, this.WhenAnyValue(wssm => wssm.HasSelectedWorkStreams));

            // Create read-only view to the source list.
            m_ReadOnlyWorkStreamsSub = m_WorkStreams.Connect()
               .ObserveOn(RxApp.MainThreadScheduler)
               .Bind(out m_ReadOnlyWorkStreams)
               .Subscribe();

            m_OrderableWorkStreamsSub = m_WorkStreams.Connect()
               .ObserveOn(RxApp.MainThreadScheduler) // Ensure UI thread safety
               .Bind(m_OrderableWorkStreams)         // Bind to the mutable collection
               .DisposeMany()                        // Clean up resources
               .Subscribe();

            m_IsBusy = this
                .WhenAnyValue(wssm => wssm.m_CoreViewModel.IsBusy)
                .ToProperty(this, wssm => wssm.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(wssm => wssm.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, wssm => wssm.HasStaleOutputs);

            m_HasCompilationErrors = this
                .WhenAnyValue(wssm => wssm.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, wssm => wssm.HasCompilationErrors);

            m_ProcessWorkStreamSettingsSub = this
                .WhenAnyValue(wssm => wssm.m_CoreViewModel.WorkStreamSettings)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(rs =>
                {
                    if (m_Current != rs)
                    {
                        ProcessSettings(rs);
                    }
                });

            m_UpdateWorkStreamSettingsSub = this
                .WhenAnyValue(wssm => wssm.AreSettingsUpdated)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(areUpdated =>
                {
                    if (areUpdated)
                    {
                        UpdateWorkStreamSettingsToCore();
                    }
                });

            RenumberWorkStreamsCommand = ReactiveCommand.CreateFromTask(RenumberWorkStreamsAsync);

            ProcessSettings(m_SettingService.DefaultWorkStreamSettings);

            Id = Resource.ProjectPlan.Titles.Title_WorkStreamSettingsView;
            Title = Resource.ProjectPlan.Titles.Title_WorkStreamSettingsView;
        }

        #endregion

        #region Properties

        public IDictionary<int, IManagedWorkStreamViewModel> SelectedWorkStreams { get; }

        #endregion

        #region Private Methods

        private int GetNextId()
        {
            lock (m_Lock)
            {
                return RawWorkStreams.Select(x => x.Id).DefaultIfEmpty().Max() + 1;
            }
        }

        private void SetSelectedManagedWorkStreams(SelectionChangedEventArgs args)
        {
            lock (m_Lock)
            {
                if (args.AddedItems is not null)
                {
                    foreach (var managedWorkStreamViewModel in args.AddedItems.OfType<IManagedWorkStreamViewModel>())
                    {
                        SelectedWorkStreams[managedWorkStreamViewModel.Id] = managedWorkStreamViewModel;
                    }
                }
                if (args.RemovedItems is not null)
                {
                    foreach (var managedWorkStreamViewModel in args.RemovedItems.OfType<IManagedWorkStreamViewModel>())
                    {
                        SelectedWorkStreams.Remove(managedWorkStreamViewModel.Id);
                    }
                }

                HasSelectedWorkStreams = SelectedWorkStreams.Any();
            }
        }

        private async Task AddManagedWorkStreamAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    m_WorkStreams.Edit(workStreams =>
                    {
                        int id = GetNextId();
                        workStreams.Add(
                            new ManagedWorkStreamViewModel(
                                this,
                                new WorkStreamModel
                                {
                                    Id = id,
                                    DisplayOrder = -1,
                                    ColorFormat = ColorHelper.Random()
                                }));
                    });
                    
                    UpdateDisplayOrders();
                }
                UpdateWorkStreamSettingsToCore();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task RemoveManagedWorkStreamsAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    m_WorkStreams.Edit(workStreams =>
                    {
                        ICollection<IManagedWorkStreamViewModel> selectedWorkStreams = SelectedWorkStreams.Values;

                        if (selectedWorkStreams.Count == 0)
                        {
                            return;
                        }

                        foreach (IManagedWorkStreamViewModel workStream in selectedWorkStreams)
                        {
                            workStreams.Remove(workStream);
                            workStream.Dispose();
                        }
                    });

                    UpdateDisplayOrders();
                }

                UpdateWorkStreamSettingsToCore();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task DuplicateManagedWorkStreamAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    IManagedWorkStreamViewModel? source = SelectedWorkStreams.Values.FirstOrDefault();

                    if (source is null)
                    {
                        return;
                    }

                    m_WorkStreams.Edit(workStreams =>
                    {
                        int id = GetNextId();
                        workStreams.Add(
                            new ManagedWorkStreamViewModel(
                                this,
                                new WorkStreamModel
                                {
                                    Id = id,
                                    Name = source.Name,
                                    IsPhase = source.IsPhase,
                                    DisplayOrder = -1,
                                    ColorFormat = ColorHelper.Random()
                                }));
                    });

                    UpdateDisplayOrders();
                }

                UpdateWorkStreamSettingsToCore();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task RenumberWorkStreamsAsync()
        {
            try
            {
                await RenumberWorkStreamsInternalAsync();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task RenumberWorkStreamsInternalAsync() =>
            await Task.Run(RenumberWorkStreamsInternal);

        private void RenumberWorkStreamsInternal()
        {
            lock (m_Lock)
            {
                UpdateDisplayOrders();

                List<(int oldId, int newId)> mappedIds = [];

                int count = OrderableWorkStreams.Count;

                for (int i = 0; i < count; i++)
                {
                    int oldId = OrderableWorkStreams[i].Id;
                    int newId = i + 1;
                    mappedIds.Add((oldId, newId));
                }

                m_CoreViewModel.UpdateManagedWorkStreamIds(mappedIds);
            }
            m_CoreViewModel.RunAutoCompile();
        }

        private void UpdateWorkStreamSettingsToCore()
        {
            lock (m_Lock)
            {
                UpdateDisplayOrders();

                var workStreamSettings = new WorkStreamSettingsModel
                {
                    WorkStreams = [.. RawWorkStreams.Select(x => new WorkStreamModel
                    {
                        Id = x.Id,
                        Name = x.Name,
                        IsPhase = x.IsPhase,
                        DisplayOrder = x.DisplayOrder,
                        ColorFormat = x.ColorFormat
                    })]
                };

                if (m_Current != workStreamSettings)
                {
                    m_Current = workStreamSettings;
                    m_CoreViewModel.WorkStreamSettings = m_Current;
                    m_ResourceSettingsManagerViewModel.AreSettingsUpdated = true; // This cascades the call to update settings to core for resource settings.
                }
            }
            AreSettingsUpdated = false;
        }

        private void UpdateDisplayOrders()
        {
            // Mark the display order in reverse order of the list because
            // the UI renders in that order and we want to reflect that.
            int resourceCount = OrderableWorkStreams.Count;

            for (int i = 0; i < resourceCount; i++)
            {
                OrderableWorkStreams[i].DisplayOrder = resourceCount - i - 1;
            }
        }

        private void ProcessSettings(WorkStreamSettingsModel workStreamSettings)
        {
            ArgumentNullException.ThrowIfNull(workStreamSettings);
            lock (m_Lock)
            {
                ClearManagedWorkStreams();

                // Add the work streams in descending order because this is how
                // the UI renders in that order and we want to reflect that.
                IOrderedEnumerable<WorkStreamModel> orderedWorkStreamModels = workStreamSettings.WorkStreams
                     .OrderByDescending(x => x.DisplayOrder)
                     .ThenByDescending(x => x.Id);

                m_WorkStreams.Edit(workStreams =>
                {
                    foreach (WorkStreamModel workStream in orderedWorkStreamModels)
                    {
                        workStreams.Add(new ManagedWorkStreamViewModel(
                            this,
                            workStream));
                    }
                });

                UpdateDisplayOrders();
            }
            AreSettingsUpdated = false;
        }

        private void ClearManagedWorkStreams()
        {
            lock (m_Lock)
            {

                m_WorkStreams.Edit(workStreams =>
                {
                    foreach (IManagedWorkStreamViewModel workStream in RawWorkStreams)
                    {
                        workStream.Dispose();
                    }
                    workStreams.Clear();
                });
            }
        }

        #endregion

        #region IWorkStreamSettingsManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        private bool m_HasSelectedWorkStreams;
        public bool HasSelectedWorkStreams
        {
            get => m_HasSelectedWorkStreams;
            set
            {
                lock (m_Lock)
                {
                    m_HasSelectedWorkStreams = value;
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

        private readonly SourceList<IManagedWorkStreamViewModel> m_WorkStreams;
        public IReadOnlyList<IManagedWorkStreamViewModel> RawWorkStreams => m_WorkStreams.Items;

        private readonly ReadOnlyObservableCollection<IManagedWorkStreamViewModel> m_ReadOnlyWorkStreams;
        public ReadOnlyObservableCollection<IManagedWorkStreamViewModel> WorkStreams => m_ReadOnlyWorkStreams;

        private readonly ObservableCollectionExtended<IManagedWorkStreamViewModel> m_OrderableWorkStreams;
        public ObservableCollection<IManagedWorkStreamViewModel> OrderableWorkStreams => m_OrderableWorkStreams;

        public ICommand SetSelectedManagedWorkStreamsCommand { get; }

        public ICommand AddManagedWorkStreamCommand { get; }

        public ICommand RemoveManagedWorkStreamsCommand { get; }

        public ICommand DuplicateManagedWorkStreamCommand { get; }

        public ICommand RenumberWorkStreamsCommand { get; }

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
                m_ReadOnlyWorkStreamsSub?.Dispose();
                m_OrderableWorkStreamsSub?.Dispose();
                m_ProcessWorkStreamSettingsSub?.Dispose();
                m_UpdateWorkStreamSettingsSub?.Dispose();
                ClearManagedWorkStreams();
                m_WorkStreams?.Dispose();
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
