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
    public class WorkStreamSettingsManagerViewModel
        : ToolViewModelBase, IWorkStreamSettingsManagerViewModel
    {
        #region Fields

        private readonly object m_Lock;
        private WorkStreamSettingsModel m_Current;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IResourceSettingsManagerViewModel m_ResourceSettingsManagerViewModel;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;

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
            m_Lock = new object();
            m_Current = new WorkStreamSettingsModel();
            m_CoreViewModel = coreViewModel;
            m_ResourceSettingsManagerViewModel = resourceSettingsManagerViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            SelectedWorkStreams = new ConcurrentDictionary<int, IManagedWorkStreamViewModel>();
            m_HasWorkStreams = false;
            m_AreSettingsUpdated = false; ;

            m_WorkStreams = [];
            m_ReadOnlyWorkStreams = new ReadOnlyObservableCollection<IManagedWorkStreamViewModel>(m_WorkStreams);

            SetSelectedManagedWorkStreamsCommand = ReactiveCommand.Create<SelectionChangedEventArgs>(SetSelectedManagedWorkStreams);
            AddManagedWorkStreamCommand = ReactiveCommand.CreateFromTask(AddManagedWorkStreamAsync);
            RemoveManagedWorkStreamsCommand = ReactiveCommand.CreateFromTask(RemoveManagedWorkStreamsAsync, this.WhenAnyValue(rm => rm.HasWorkStreams));

            m_IsBusy = this
                .WhenAnyValue(rm => rm.m_CoreViewModel.IsBusy)
                .ToProperty(this, rm => rm.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(rm => rm.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, rm => rm.HasStaleOutputs);

            m_HasCompilationErrors = this
                .WhenAnyValue(rm => rm.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, rm => rm.HasCompilationErrors);

            m_ProcessWorkStreamSettingsSub = this
                .WhenAnyValue(rm => rm.m_CoreViewModel.WorkStreamSettings)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(rs =>
                {
                    if (m_Current != rs)
                    {
                        ProcessSettings(rs);
                    }
                });

            m_UpdateWorkStreamSettingsSub = this
                .WhenAnyValue(rm => rm.AreSettingsUpdated)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(areUpdated =>
                {
                    if (areUpdated)
                    {
                        UpdateWorkStreamSettingsToCore();
                    }
                });

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
                return WorkStreams.Select(x => x.Id).DefaultIfEmpty().Max() + 1;
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
                        SelectedWorkStreams.TryAdd(managedWorkStreamViewModel.Id, managedWorkStreamViewModel);
                    }
                }
                if (args.RemovedItems is not null)
                {
                    foreach (var managedWorkStreamViewModel in args.RemovedItems.OfType<IManagedWorkStreamViewModel>())
                    {
                        SelectedWorkStreams.Remove(managedWorkStreamViewModel.Id);
                    }
                }

                HasWorkStreams = SelectedWorkStreams.Any();
            }
        }

        private async Task AddManagedWorkStreamAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    int id = GetNextId();
                    m_WorkStreams.Add(
                        new ManagedWorkStreamViewModel(
                            this,
                            new WorkStreamModel
                            {
                                Id = id,
                                ColorFormat = ColorHelper.Random()
                            }));
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
                    ICollection<IManagedWorkStreamViewModel> workStreams = SelectedWorkStreams.Values;

                    if (workStreams.Count == 0)
                    {
                        return;
                    }

                    foreach (IManagedWorkStreamViewModel workStream in workStreams)
                    {
                        m_WorkStreams.Remove(workStream);
                        workStream.Dispose();
                    }
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

        private void UpdateWorkStreamSettingsToCore()
        {
            lock (m_Lock)
            {
                var workStreamSettings = new WorkStreamSettingsModel
                {
                    WorkStreams = WorkStreams.Select(x => new WorkStreamModel
                    {
                        Id = x.Id,
                        Name = x.Name,
                        IsPhase = x.IsPhase,
                        DisplayOrder = x.DisplayOrder,
                        ColorFormat = x.ColorFormat
                    }).ToList()
                };

                if (m_Current != workStreamSettings)
                {
                    m_Current = workStreamSettings;
                    m_CoreViewModel.WorkStreamSettings = workStreamSettings;
                    m_ResourceSettingsManagerViewModel.AreSettingsUpdated = true; // This cascades the call to update settings to core for resource settings.
                }
            }
            AreSettingsUpdated = false;
        }

        private void ProcessSettings(WorkStreamSettingsModel workStreamSettings)
        {
            ArgumentNullException.ThrowIfNull(workStreamSettings);
            lock (m_Lock)
            {
                ClearManagedWorkStreams();

                foreach (WorkStreamModel workStream in workStreamSettings.WorkStreams)
                {
                    m_WorkStreams.Add(new ManagedWorkStreamViewModel(
                        this,
                        workStream));
                }
            }
            AreSettingsUpdated = false;
        }

        private void ClearManagedWorkStreams()
        {
            lock (m_Lock)
            {
                foreach (IManagedWorkStreamViewModel workStream in m_WorkStreams)
                {
                    workStream.Dispose();
                }
                m_WorkStreams.Clear();
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

        private bool m_HasWorkStreams;
        public bool HasWorkStreams
        {
            get => m_HasWorkStreams;
            set
            {
                lock (m_Lock)
                {
                    m_HasWorkStreams = value;
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

        private readonly ObservableCollection<IManagedWorkStreamViewModel> m_WorkStreams;
        private readonly ReadOnlyObservableCollection<IManagedWorkStreamViewModel> m_ReadOnlyWorkStreams;
        public ReadOnlyObservableCollection<IManagedWorkStreamViewModel> WorkStreams => m_ReadOnlyWorkStreams;

        public ICommand SetSelectedManagedWorkStreamsCommand { get; }

        public ICommand AddManagedWorkStreamCommand { get; }

        public ICommand RemoveManagedWorkStreamsCommand { get; }

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
                m_ProcessWorkStreamSettingsSub?.Dispose();
                m_UpdateWorkStreamSettingsSub?.Dispose();
                ClearManagedWorkStreams();
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
