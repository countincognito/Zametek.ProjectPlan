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
    public class ActivitiesManagerViewModel
        : ToolViewModelBase, IActivitiesManagerViewModel
    {
        #region Fields

        private readonly Lock m_Lock;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IDialogService m_DialogService;

        #endregion

        #region Ctors

        public ActivitiesManagerViewModel(
            ICoreViewModel coreViewModel,
            IDialogService dialogService)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(dialogService);
            m_Lock = new();
            m_CoreViewModel = coreViewModel;
            m_DialogService = dialogService;
            SelectedActivities = new ConcurrentDictionary<int, IManagedActivityViewModel>();
            m_HasSelectedActivity = false;
            m_HasSelectedActivities = false;

            SetSelectedManagedActivitiesCommand = ReactiveCommand.Create<SelectionChangedEventArgs>(SetSelectedManagedActivities);
            AddManagedActivityCommand = ReactiveCommand.CreateFromTask(AddManagedActivityAsync);
            InsertManagedActivityCommand = ReactiveCommand.CreateFromTask(InsertManagedActivityAsync, this.WhenAnyValue(am => am.HasSelectedActivity));
            RemoveManagedActivitiesCommand = ReactiveCommand.CreateFromTask(RemoveManagedActivitiesAsync, this.WhenAnyValue(am => am.HasSelectedActivities));
            EditManagedActivitiesCommand = ReactiveCommand.CreateFromTask(EditManagedActivitiesAsync, this.WhenAnyValue(am => am.HasSelectedActivities));
            DuplicateManagedActivityCommand = ReactiveCommand.CreateFromTask(DuplicateManagedActivityAsync, this.WhenAnyValue(am => am.HasSelectedActivity));

            m_IsBusy = this
                .WhenAnyValue(am => am.m_CoreViewModel.IsBusy)
                .ToProperty(this, am => am.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(am => am.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, am => am.HasStaleOutputs);

            m_ShowDates = this
                .WhenAnyValue(am => am.m_CoreViewModel.DisplaySettingsViewModel.ShowDates)
                .ToProperty(this, am => am.ShowDates);

            m_HasCompilationErrors = this
                .WhenAnyValue(am => am.m_CoreViewModel.HasCompilationErrors)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, am => am.HasCompilationErrors);

            m_HideCost = this
                .WhenAnyValue(am => am.m_CoreViewModel.DisplaySettingsViewModel.HideCost)
                .ToProperty(this, am => am.HideCost);

            m_HideBilling = this
                .WhenAnyValue(am => am.m_CoreViewModel.DisplaySettingsViewModel.HideBilling)
                .ToProperty(this, am => am.HideBilling);

            RenumberActivitiesCommand = ReactiveCommand.CreateFromTask(RenumberActivitiesAsync);

            AddMilestoneCommand = ReactiveCommand.CreateFromTask(
                AddMilestoneAsync,
                this.WhenAnyValue(
                    am => am.HasSelectedActivities,
                    am => am.HasCompilationErrors,
                    (hasActivities, hasCompilationErrors) => hasActivities && !hasCompilationErrors),
                RxApp.MainThreadScheduler);

            Id = Resource.ProjectPlan.Titles.Title_ActivitiesView;
            Title = Resource.ProjectPlan.Titles.Title_ActivitiesView;
        }

        #endregion

        #region Properties

        public IDictionary<int, IManagedActivityViewModel> SelectedActivities { get; }

        #endregion

        #region Private Methods

        private void SetSelectedManagedActivities(SelectionChangedEventArgs args)
        {
            lock (m_Lock)
            {
                if (args.AddedItems is not null)
                {
                    foreach (var managedActivityViewModel in args.AddedItems.OfType<ManagedActivityViewModel>())
                    {
                        SelectedActivities[managedActivityViewModel.Id] = managedActivityViewModel;
                    }
                }
                if (args.RemovedItems is not null)
                {
                    foreach (var managedActivityViewModel in args.RemovedItems.OfType<ManagedActivityViewModel>())
                    {
                        SelectedActivities.Remove(managedActivityViewModel.Id);
                    }
                }

                HasSelectedActivities = SelectedActivities.Any();
                HasSelectedActivity = HasSelectedActivities && SelectedActivities.Count == 1;
            }
        }

        private async Task AddManagedActivityAsync()
        {
            try
            {
                await AddManagedActivityInternalAsync();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task AddManagedActivityInternalAsync() => await Task.Run(AddManagedActivityInternal);

        private void AddManagedActivityInternal()
        {
            lock (m_Lock)
            {
                int displayOrder = m_CoreViewModel
                    .RawActivities
                    .DefaultIfEmpty()
                    .Max(x => x?.DisplayOrder ?? 0) + 1;

                m_CoreViewModel.AddManagedActivity(displayOrder);
                m_CoreViewModel.IsReadyToReviseTrackers = ReadyToRevise.Yes;
            }
            m_CoreViewModel.RunAutoCompile();
        }

        private async Task InsertManagedActivityAsync()
        {
            try
            {
                await InsertManagedActivityInternalAsync();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task InsertManagedActivityInternalAsync() => await Task.Run(InsertManagedActivityInternal);

        private void InsertManagedActivityInternal()
        {
            lock (m_Lock)
            {
                SelectedActivities.TryGetValue(SelectedActivities.Keys.FirstOrDefault(), out IManagedActivityViewModel? selectedActivity);

                if (selectedActivity is null)
                {
                    return;
                }

                int selectedId = selectedActivity.Id;
                int newDisplayOrder = selectedActivity.DisplayOrder - 1;
                int newId = m_CoreViewModel.AddManagedActivity(newDisplayOrder);

                m_CoreViewModel.UpdateManagedActivityIds([(newId, selectedId)]);
                m_CoreViewModel.IsReadyToReviseTrackers = ReadyToRevise.Yes;
            }
            m_CoreViewModel.RunAutoCompile();
        }

        private async Task DuplicateManagedActivityAsync()
        {
            try
            {
                await DuplicateManagedActivityInternalAsync();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task DuplicateManagedActivityInternalAsync() => await Task.Run(DuplicateManagedActivityInternal);

        private void DuplicateManagedActivityInternal()
        {
            lock (m_Lock)
            {
                SelectedActivities.TryGetValue(SelectedActivities.Keys.FirstOrDefault(), out IManagedActivityViewModel? selectedActivity);

                if (selectedActivity is null)
                {
                    return;
                }

                int newId = m_CoreViewModel.GetNextActivityId();

                DependentActivityModel duplicateModel = selectedActivity.DeepCopy();

                // Clear the trackers because otherwise we would need to alter
                // all the IDs to correspond with the new ID.
                var activityModel = duplicateModel.Activity with
                {
                    Id = newId,
                    Trackers = [],
                };

                duplicateModel = duplicateModel with
                {
                    Activity = activityModel,
                };

                m_CoreViewModel.AddManagedActivities([duplicateModel]);
                m_CoreViewModel.IsReadyToReviseTrackers = ReadyToRevise.Yes;
            }

            m_CoreViewModel.RunAutoCompile();
        }

        private async Task RemoveManagedActivitiesAsync()
        {
            try
            {
                await RemoveManagedActivitiesInternalAsync();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task RemoveManagedActivitiesInternalAsync() => await Task.Run(RemoveManagedActivitiesInternal);

        private void RemoveManagedActivitiesInternal()
        {
            lock (m_Lock)
            {
                ICollection<int> activityIds = SelectedActivities.Keys;

                if (activityIds.Count == 0)
                {
                    return;
                }

                m_CoreViewModel.RemoveManagedActivities(activityIds);
                m_CoreViewModel.IsReadyToReviseTrackers = ReadyToRevise.Yes;
            }
            m_CoreViewModel.RunAutoCompile();
        }

        private async Task EditManagedActivitiesAsync()
        {
            try
            {
                var editViewModel = new ActivityEditViewModel(
                    m_CoreViewModel.ResourceSettings.Resources,
                    m_CoreViewModel.WorkStreamSettings.WorkStreams);

                bool result = await m_DialogService.ShowContextAsync(
                    title: Resource.ProjectPlan.Titles.Title_EditActivities,
                    header: string.Empty,
                    message: $@"**{Resource.ProjectPlan.Messages.Message_EditActivities}**",
                    context: editViewModel,
                    markdown: true);

                if (!result)
                {
                    return;
                }

                UpdateDependentActivityModel updateModel = editViewModel.BuildUpdateModel();
                await EditManagedActivitiesInternalAsync(updateModel);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task EditManagedActivitiesInternalAsync(UpdateDependentActivityModel updateModel) =>
            await Task.Run(() => EditManagedActivitiesInternal(updateModel));

        private void EditManagedActivitiesInternal(UpdateDependentActivityModel updateModel)
        {
            lock (m_Lock)
            {
                ICollection<int> activityIds = SelectedActivities.Keys;

                if (activityIds.Count == 0)
                {
                    return;
                }

                IEnumerable<UpdateDependentActivityModel> updateModels = [.. activityIds.Select(x => updateModel with { Id = x })];

                m_CoreViewModel.UpdateManagedActivities(updateModels);
            }
            m_CoreViewModel.RunAutoCompile();
        }

        private async Task RenumberActivitiesAsync()
        {
            try
            {
                await RenumberActivitiesInternalAsync();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task RenumberActivitiesInternalAsync() =>
            await Task.Run(RenumberActivitiesInternal);

        private void RenumberActivitiesInternal()
        {
            lock (m_Lock)
            {
                m_CoreViewModel.UpdateActivityDisplayOrders();

                List<(int oldId, int newId)> mappedIds = [];

                int count = OrderableActivities.Count;

                for (int i = 0; i < count; i++)
                {
                    int oldId = OrderableActivities[i].Id;
                    int newId = i + 1;
                    mappedIds.Add((oldId, newId));
                }

                m_CoreViewModel.UpdateManagedActivityIds(mappedIds);
                m_CoreViewModel.IsReadyToReviseTrackers = ReadyToRevise.Yes;
            }
            m_CoreViewModel.RunAutoCompile();
        }

        private async Task AddMilestoneAsync()
        {
            try
            {
                await AddMilestoneInternalAsync();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task AddMilestoneInternalAsync() =>
            await Task.Run(AddMilestoneInternal);

        private void AddMilestoneInternal()
        {
            lock (m_Lock)
            {
                ICollection<int> activityIds = SelectedActivities.Keys;

                if (activityIds.Count == 0)
                {
                    return;
                }

                m_CoreViewModel.AddMilestone(activityIds);
                m_CoreViewModel.IsReadyToReviseTrackers = ReadyToRevise.Yes;
            }
            m_CoreViewModel.RunAutoCompile();
        }

        #endregion

        #region IActivityManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_ShowDates;
        public bool ShowDates => m_ShowDates.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HideCost;
        public bool HideCost => m_HideCost.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HideBilling;
        public bool HideBilling => m_HideBilling.Value;

        private bool m_HasSelectedActivity;
        public bool HasSelectedActivity
        {
            get => m_HasSelectedActivity;
            set
            {
                lock (m_Lock)
                {
                    m_HasSelectedActivity = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        private bool m_HasSelectedActivities;
        public bool HasSelectedActivities
        {
            get => m_HasSelectedActivities;
            set
            {
                lock (m_Lock)
                {
                    m_HasSelectedActivities = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public IReadOnlyList<IManagedActivityViewModel> RawActivities => m_CoreViewModel.RawActivities;

        public ReadOnlyObservableCollection<IManagedActivityViewModel> Activities => m_CoreViewModel.Activities;

        public ObservableCollection<IManagedActivityViewModel> OrderableActivities => m_CoreViewModel.OrderableActivities;

        private int m_ScrollToActivityId;
        public int ScrollToActivityId
        {
            get => m_ScrollToActivityId;
            private set => this.RaiseAndSetIfChanged(ref m_ScrollToActivityId, value);
        }

        public void SelectActivityById(int activityId)
        {
            lock (m_Lock)
            {
                IManagedActivityViewModel? activity = RawActivities.FirstOrDefault(a => a.Id == activityId);
                if (activity is not null)
                {
                    SelectedActivities.Clear();
                    SelectedActivities[activityId] = activity;
                    HasSelectedActivities = true;
                    HasSelectedActivity = true;
                    ScrollToActivityId = activityId;
                }
            }
        }

        public ICommand SetSelectedManagedActivitiesCommand { get; }

        public ICommand AddManagedActivityCommand { get; }

        public ICommand InsertManagedActivityCommand { get; }

        public ICommand RemoveManagedActivitiesCommand { get; }

        public ICommand EditManagedActivitiesCommand { get; }

        public ICommand DuplicateManagedActivityCommand { get; }

        public ICommand RenumberActivitiesCommand { get; }

        public ICommand AddMilestoneCommand { get; }

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
                // Dispose managed state (managed objects).
                m_IsBusy?.Dispose();
                m_HasStaleOutputs?.Dispose();
                m_ShowDates?.Dispose();
                m_HasCompilationErrors?.Dispose();
                m_HideCost?.Dispose();
                m_HideBilling?.Dispose();
            }

            // Free unmanaged resources (unmanaged objects) and override a finalizer below.
            // Set large fields to null.

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
