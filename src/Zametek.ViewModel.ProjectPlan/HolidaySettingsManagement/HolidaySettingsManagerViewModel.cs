using Avalonia.Controls;
using DynamicData;
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
    public class HolidaySettingsManagerViewModel
        : ToolViewModelBase, IHolidaySettingsManagerViewModel, IDisposable
    {
        #region Fields

        private readonly Lock m_Lock;
        private HolidaySettingsModel m_Current;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;

        private readonly IDisposable? m_ReadOnlyHolidaysSub;
        private readonly IDisposable? m_ProcessHolidaySettingsSub;
        private readonly IDisposable? m_UpdateHolidaySettingsSub;

        #endregion

        #region Ctors

        public HolidaySettingsManagerViewModel(
            ICoreViewModel coreViewModel,
            ISettingService settingService,
            IDialogService dialogService)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            m_Lock = new();
            m_Current = new HolidaySettingsModel();
            m_CoreViewModel = coreViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            SelectedHolidays = new ConcurrentDictionary<int, IManagedHolidayViewModel>();
            m_HasSelectedHoliday = false;
            m_HasSelectedHolidays = false;
            m_AreSettingsUpdated = false; ;

            m_Holidays = new();

            SetSelectedManagedHolidaysCommand = ReactiveCommand.Create<SelectionChangedEventArgs>(SetSelectedManagedHolidays);
            AddManagedHolidayCommand = ReactiveCommand.CreateFromTask(AddManagedHolidayAsync);
            RemoveManagedHolidaysCommand = ReactiveCommand.CreateFromTask(RemoveManagedHolidaysAsync, this.WhenAnyValue(rm => rm.HasSelectedHolidays));
            EditManagedHolidayCommand = ReactiveCommand.CreateFromTask(EditManagedHolidayAsync, this.WhenAnyValue(am => am.HasSelectedHoliday));

            // Create read-only view to the source list.
            m_ReadOnlyHolidaysSub = m_Holidays.Connect()
               .ObserveOn(RxApp.MainThreadScheduler)
               .Bind(out m_ReadOnlyHolidays)
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

            m_ProcessHolidaySettingsSub = this
                .WhenAnyValue(rsm => rsm.m_CoreViewModel.HolidaySettings)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(rs =>
                {
                    if (m_Current != rs)
                    {
                        ProcessSettings(rs);
                    }
                });

            m_UpdateHolidaySettingsSub = this
                .WhenAnyValue(rsm => rsm.AreSettingsUpdated)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(areUpdated =>
                {
                    if (areUpdated)
                    {
                        UpdateHolidaySettingsToCore();
                    }
                });

            ProcessSettings(m_SettingService.DefaultHolidaySettings);

            Id = Resource.ProjectPlan.Titles.Title_HolidaySettingsView;
            Title = Resource.ProjectPlan.Titles.Title_HolidaySettingsView;
        }

        #endregion

        #region Properties

        public IDictionary<int, IManagedHolidayViewModel> SelectedHolidays { get; }

        #endregion

        #region Private Methods

        private int GetNextId()
        {
            lock (m_Lock)
            {
                return RawHolidays.Select(x => x.Id).DefaultIfEmpty().Max() + 1;
            }
        }

        private void SetSelectedManagedHolidays(SelectionChangedEventArgs args)
        {
            lock (m_Lock)
            {
                if (args.AddedItems is not null)
                {
                    foreach (var managedHolidayViewModel in args.AddedItems.OfType<IManagedHolidayViewModel>())
                    {
                        SelectedHolidays[managedHolidayViewModel.Id] = managedHolidayViewModel;
                    }
                }
                if (args.RemovedItems is not null)
                {
                    foreach (var managedHolidayViewModel in args.RemovedItems.OfType<IManagedHolidayViewModel>())
                    {
                        SelectedHolidays.Remove(managedHolidayViewModel.Id);
                    }
                }

                HasSelectedHolidays = SelectedHolidays.Any();
                HasSelectedHoliday = HasSelectedHolidays && SelectedHolidays.Count == 1;
            }
        }

        private async Task AddManagedHolidayAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    m_Holidays.Edit(holidays =>
                    {
                        int holidayId = GetNextId();
                        holidays.Add(
                            new ManagedHolidayViewModel(
                                this,
                                new HolidayModel
                                {
                                    Id = holidayId,
                                }));
                    });
                }
                UpdateHolidaySettingsToCore();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task RemoveManagedHolidaysAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    m_Holidays.Edit(holidays =>
                    {
                        ICollection<IManagedHolidayViewModel> selectedHolidays = SelectedHolidays.Values;

                        if (selectedHolidays.Count == 0)
                        {
                            return;
                        }

                        foreach (IManagedHolidayViewModel holiday in selectedHolidays)
                        {
                            holidays.Remove(holiday);
                            holiday.Dispose();
                        }
                    });
                }
                UpdateHolidaySettingsToCore();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task EditManagedHolidayAsync()
        {



            var editViewModel = new RRuleEditorViewModel();

            bool result = await m_DialogService.ShowContextAsync(
                title: "Calendar",
                header: string.Empty,
                message: "Calendar",
                context: editViewModel,
                markdown: true);







            //try
            //{
            //    var editViewModel = new HolidayEditViewModel(m_CoreViewModel.WorkStreamSettings.WorkStreams);

            //    bool result = await m_DialogService.ShowContextAsync(
            //        title: Resource.ProjectPlan.Titles.Title_EditResources,
            //        header: string.Empty,
            //        message: $@"**{Resource.ProjectPlan.Messages.Message_EditResources}**",
            //        context: editViewModel,
            //        markdown: true);

            //    if (!result)
            //    {
            //        return;
            //    }

            //    lock (m_Lock)
            //    {
            //        ICollection<int> resourceIds = SelectedResources.Keys;

            //        if (resourceIds.Count == 0)
            //        {
            //            return;
            //        }

            //        UpdateResourceModel updateModel = editViewModel.BuildUpdateModel();

            //        IEnumerable<UpdateResourceModel> updateModels = [.. resourceIds.Select(x => updateModel with { Id = x })];

            //        UpdateManagedResources(updateModels);
            //    }
            //    UpdateResourceSettingsToCore();
            //}
            //catch (Exception ex)
            //{
            //    await m_DialogService.ShowErrorAsync(
            //        Resource.ProjectPlan.Titles.Title_Error,
            //        string.Empty,
            //        ex.Message);
            //}
        }

        private void UpdateManagedHolidays(IEnumerable<UpdateHolidayModel> updateModels)
        {
            lock (m_Lock)
            {
                Dictionary<int, IManagedHolidayViewModel> holidayLookup = RawHolidays.ToDictionary(x => x.Id);

                foreach (UpdateHolidayModel updateModel in updateModels)
                {
                    if (holidayLookup.TryGetValue(updateModel.Id, out IManagedHolidayViewModel? holiday))
                    {
                        if (holiday is IEditableObject editable)
                        {
                            holiday.IsEditMuted = true;
                            editable.BeginEdit();

                            if (updateModel.IsNameEdited)
                            {
                                holiday.Name = updateModel.Name;
                            }
                            if (updateModel.IsNotesEdited)
                            {
                                holiday.Notes = updateModel.Notes;
                            }
                            if (updateModel.IsRecurrencePatternEdited)
                            {
                                holiday.RecurrencePattern = updateModel.RecurrencePattern;
                            }

                            editable.EndEdit();
                            holiday.IsEditMuted = false;
                        }
                    }
                }
            }
        }

        private void UpdateHolidaySettingsToCore()
        {
            lock (m_Lock)
            {
                var holidaySettings = new HolidaySettingsModel
                {
                    Holidays = [.. RawHolidays.Select(x => new HolidayModel
                    {
                        Id = x.Id,
                        Name = x.Name,
                        Notes = x.Notes,
                        RecurrencePattern = x.RecurrencePattern,
                    })],
                };

                if (m_Current != holidaySettings)
                {
                    m_Current = holidaySettings;
                    m_CoreViewModel.HolidaySettings = m_Current;
                }
            }
            AreSettingsUpdated = false;
        }

        private void ProcessSettings(HolidaySettingsModel holidaySettings)
        {
            ArgumentNullException.ThrowIfNull(holidaySettings);
            lock (m_Lock)
            {
                ClearManagedHolidays();

                m_Holidays.Edit(holidays =>
                {
                    foreach (HolidayModel holiday in holidaySettings.Holidays)
                    {
                        holidays.Add(new ManagedHolidayViewModel(
                            this,
                            holiday));
                    }
                });

                //m_Current = resourceSettings;
            }
            AreSettingsUpdated = false;
        }

        private void ClearManagedHolidays()
        {
            lock (m_Lock)
            {
                m_Holidays.Edit(holidays =>
                {
                    foreach (IManagedHolidayViewModel holiday in RawHolidays)
                    {
                        holiday.Dispose();
                    }
                    holidays.Clear();
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

        private bool m_HasSelectedHoliday;
        public bool HasSelectedHoliday
        {
            get => m_HasSelectedHoliday;
            set
            {
                lock (m_Lock)
                {
                    m_HasSelectedHoliday = value;
                    this.RaisePropertyChanged();
                }
            }
        }
        private bool m_HasSelectedHolidays;
        public bool HasSelectedHolidays
        {
            get => m_HasSelectedHolidays;
            set
            {
                lock (m_Lock)
                {
                    m_HasSelectedHolidays = value;
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

        private readonly SourceList<IManagedHolidayViewModel> m_Holidays;
        public IReadOnlyList<IManagedHolidayViewModel> RawHolidays => m_Holidays.Items;

        private readonly ReadOnlyObservableCollection<IManagedHolidayViewModel> m_ReadOnlyHolidays;
        public ReadOnlyObservableCollection<IManagedHolidayViewModel> Holidays => m_ReadOnlyHolidays;

        public ICommand SetSelectedManagedHolidaysCommand { get; }

        public ICommand AddManagedHolidayCommand { get; }

        public ICommand RemoveManagedHolidaysCommand { get; }

        public ICommand EditManagedHolidayCommand { get; }

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
                m_ReadOnlyHolidaysSub?.Dispose();
                m_ProcessHolidaySettingsSub?.Dispose();
                m_UpdateHolidaySettingsSub?.Dispose();
                ClearManagedHolidays();
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
