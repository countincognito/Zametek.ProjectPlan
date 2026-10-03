using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using static Zametek.ViewModel.ProjectPlan.Tests.SequencerScopes;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The view model that a holiday settings manager makes for the user to edit a holiday in is started if the manager is,
    /// and not otherwise.
    /// </summary>
    /// <remarks>
    /// The pumps stand in for the thread pool and the main thread, so each test says when a delivery runs. An edit view
    /// model builds its recurrence rule from what the user chooses when its pipeline delivers, which is what is looked for.
    /// </remarks>
    public class HolidaySettingsManagerStartTests
    {
        [Fact]
        public void EditManagedHolidayCommand_Given_AStartedManager_Then_TheEditViewModelIsStarted()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Manager.StartSubscriptions();
            Drain(main, pool);

            HolidayEditViewModel edit = harness.OpenEditViewModel();
            edit.RecurrenceFrequency = RecurrenceFrequency.Weekly;
            Drain(main, pool);

            edit.RecurrenceRule.Frequency.ShouldBe(RecurrenceFrequency.Weekly);
        }

        [Fact]
        public void EditManagedHolidayCommand_Given_AManagerNotStarted_Then_TheEditViewModelIsNot()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();

            HolidayEditViewModel edit = harness.OpenEditViewModel();
            edit.RecurrenceFrequency = RecurrenceFrequency.Weekly;
            Drain(main, pool);

            // Nothing builds the rule from the choice.
            edit.RecurrenceRule.Frequency.ShouldBe(RecurrenceFrequency.None);
        }

        [Fact]
        public void KillSubscriptions_Given_AStartedEditViewModel_Then_ItsPipelinesDeliverNothingMore()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Manager.StartSubscriptions();
            Drain(main, pool);
            HolidayEditViewModel edit = harness.OpenEditViewModel();
            edit.RecurrenceFrequency = RecurrenceFrequency.Weekly;
            (main.Pump.PendingCount + pool.Pump.PendingCount).ShouldBeGreaterThan(0, "the pipelines were not running to begin with");
            Drain(main, pool);

            edit.KillSubscriptions();
            edit.RecurrenceFrequency = RecurrenceFrequency.Monthly;

            main.Pump.PendingCount.ShouldBe(0);
            pool.Pump.PendingCount.ShouldBe(0);
        }

        // Records the view model that a dialog is asked to show, and answers that the user cancelled it.
        private sealed class RecordingDialogService
            : IDialogService
        {
            public object? LastContext { get; private set; }

            public object Parent { set { } }

            public Task ShowNotificationAsync(string title, string header, string message) =>
                throw new NotSupportedException(message);

            public Task ShowErrorAsync(string title, string header, string message) =>
                throw new NotSupportedException(message);

            public Task ShowWarningAsync(string title, string header, string message) =>
                throw new NotSupportedException(message);

            public Task ShowInfoAsync(string title, string header, string message, bool showMainPageLink = false) =>
                throw new NotSupportedException(message);

            public Task ShowInfoAsync(string title, string header, string message, double height, double width, bool showMainPageLink = false) =>
                throw new NotSupportedException(message);

            public Task<bool> ShowContextAsync(string title, string header, string message, object context)
            {
                LastContext = context;
                return Task.FromResult(false);
            }

            public Task<bool> ShowContextAsync(string title, string header, string message, object context, double height, double width) =>
                ShowContextAsync(title, header, message, context);

            public Task<bool> ShowConfirmationAsync(string title, string header, string message) =>
                throw new NotSupportedException(message);

            public Task<string?> ShowOpenFileDialogAsync(string initialDirectory, IList<IFileFilter> fileFilters) =>
                throw new NotSupportedException();

            public Task<string?> ShowSaveFileDialogAsync(string initialFilename, string initialDirectory, IList<IFileFilter> fileFilters) =>
                throw new NotSupportedException();
        }

        // A holiday settings manager over a core, with the services a test never reaches stood in for.
        private sealed class Harness
            : IDisposable
        {
            private readonly CoreViewModel m_Core;
            private readonly RecordingDialogService m_Dialogs;

            private Harness(CoreViewModel core, RecordingDialogService dialogs, HolidaySettingsManagerViewModel manager)
            {
                m_Core = core;
                m_Dialogs = dialogs;
                Manager = manager;
            }

            public HolidaySettingsManagerViewModel Manager { get; }

            public static Harness Make()
            {
                (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
                var dialogs = new RecordingDialogService();

                return new Harness(
                    core,
                    dialogs,
                    new HolidaySettingsManagerViewModel(core, new DateTimeCalculator(TimeProvider.System), settings, dialogs));
            }

            // As the user does: adds a holiday, selects it, and chooses to edit it. The dialog is shown the view model
            // that the manager made for the purpose, and is cancelled.
            public HolidayEditViewModel OpenEditViewModel()
            {
                Manager.AddManagedHolidayCommand.Execute(null);

                IManagedHolidayViewModel holiday = Manager.RawHolidays[^1];
                Manager.SelectedHolidays.Clear();
                Manager.SelectedHolidays[holiday.Id] = holiday;
                Manager.HasSelectedHoliday = true;

                Manager.EditManagedHolidayCommand.Execute(null);

                return m_Dialogs.LastContext.ShouldBeOfType<HolidayEditViewModel>();
            }

            public void Dispose()
            {
                Manager.Dispose();
                m_Core.Dispose();
            }
        }
    }
}
