using Autofac;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Model.Core;
using Shouldly;
using Ursa.Controls;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan.Tests
{
    /// <summary>
    /// Works the application through the commands and the properties that the mouse reaches. A click on a scenario, a tab and a
    /// dropdown change these, and a click on an item of a dropdown sets its IsSelected.
    /// </summary>
    public sealed class ApplicationDriver
    {
        private static readonly TimeSpan s_Patience = TimeSpan.FromSeconds(60);

        private readonly Window m_Window;
        private readonly ApplicationFixture m_Fixture;
        private readonly TestDialogService m_Dialogs;

        public ApplicationDriver(Window window, IContainer container, ApplicationFixture fixture)
        {
            m_Window = window;
            m_Fixture = fixture;
            m_Dialogs = container.Resolve<TestDialogService>();
            Main = container.Resolve<IMainViewModel>();
            Core = container.Resolve<ICoreViewModel>();
            Project = container.Resolve<IProjectScenarioManagerViewModel>();
            Gantt = container.Resolve<IGanttChartManagerViewModel>();
            EarnedValue = container.Resolve<IEarnedValueChartManagerViewModel>();
            Activities = container.Resolve<IActivitiesManagerViewModel>();
        }

        public IMainViewModel Main { get; }

        public ICoreViewModel Core { get; }

        public IProjectScenarioManagerViewModel Project { get; }

        public IGanttChartManagerViewModel Gantt { get; }

        public IEarnedValueChartManagerViewModel EarnedValue { get; }

        public IActivitiesManagerViewModel Activities { get; }

        public List<IManagedNodeViewModel> Scenarios => [.. Project.RawFlattenedNodes.Where(x => !x.IsFolder)];

        public async Task SettleAsync()
        {
            for (int quiet = 0; quiet < 6;)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                await Task.Delay(25);
                quiet = Core.IsBusy || Core.IsBulkUpdating ? 0 : quiet + 1;
            }
        }

        private async Task UntilAsync(Func<bool> condition, string what)
        {
            var patience = new CancellationTokenSource(s_Patience);

            while (!condition())
            {
                patience.IsCancellationRequested.ShouldBeFalse($@"The application did not get to this: {what}");
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                await Task.Delay(25);
            }

            await SettleAsync();
        }

        public async Task OpenPlanAsync()
        {
            await Main.OpenProjectFileAsync(Path.Combine(AppContext.BaseDirectory, @"TestFiles", @"sample_v0_6_1.zpp"));
            await UntilAsync(() => Scenarios.Count == 1 && Scenarios[0].IsLoaded, @"the plan open");
            ShouldHaveNoErrors();
        }

        private void Select(IManagedNodeViewModel scenario)
        {
            Project.SelectedNodes.Clear();
            Project.SelectedNodes.Add(scenario);
            Project.SetSelectedManagedNodesCommand.Execute(null);
        }

        public async Task LoadAsync(IManagedNodeViewModel scenario)
        {
            Select(scenario);
            Project.LoadSelectedProjectScenarioFileCommand.Execute(null);
            await UntilAsync(() => scenario.IsLoaded, $@"{scenario.Name} loaded");
            ShouldHaveNoErrors();
        }

        public async Task<IManagedNodeViewModel> DuplicateAsync(IManagedNodeViewModel scenario)
        {
            List<Guid> known = [.. Scenarios.Select(x => x.Id)];
            Select(scenario);
            Project.DuplicateProjectScenarioNodeCommand.Execute(null);
            await UntilAsync(() => Scenarios.Count > known.Count, @"the duplicate made");
            return Scenarios.First(x => !known.Contains(x.Id));
        }

        public async Task<IManagedNodeViewModel> CreateEmptyAsync()
        {
            List<Guid> known = [.. Scenarios.Select(x => x.Id)];
            Project.CreateEmptyProjectScenarioFileCommand.Execute(null);
            await UntilAsync(() => Scenarios.Count > known.Count, @"the empty scenario made");
            return Scenarios.First(x => !known.Contains(x.Id));
        }

        public async Task ShowTabAsync(object toolViewModel)
        {
            var dockable = (IDockable)toolViewModel;
            dockable.Factory.ShouldNotBeNull().SetActiveDockable(dockable);
            await SettleAsync();
        }

        private MultiComboBox FilterBoxOnShow(object chartViewModel) =>
            m_Window.GetVisualDescendants().OfType<MultiComboBox>().FirstOrDefault(x => ReferenceEquals(x.DataContext, chartViewModel))
                ?? throw new InvalidOperationException(@"The tab of this chart is not on show.");

        private static List<MultiComboBoxItem> ItemsInTheDropdown(MultiComboBox box) =>
            [.. box.ItemsPanelRoot.ShouldNotBeNull().Children.OfType<MultiComboBoxItem>().Where(x => x.IsVisible)];

        private static int IdOf(object? item) => item switch
        {
            ISelectableActivityViewModel activity => activity.Id,
            ISelectableResourceViewModel resource => resource.Id,
            _ => throw new InvalidOperationException(@"This is not an item of a chart filter."),
        };

        private async Task SetDropDownAsync(object chartViewModel, bool isOpen)
        {
            FilterBoxOnShow(chartViewModel).SetCurrentValue(MultiComboBox.IsDropDownOpenProperty, isOpen);
            await SettleAsync();
        }

        /// <summary>
        /// Opens the dropdown, clicks the items with these ids, and closes the dropdown.
        /// </summary>
        public async Task ClickItemsAsync(object chartViewModel, params int[] ids)
        {
            await SetDropDownAsync(chartViewModel, true);

            foreach (int id in ids)
            {
                MultiComboBoxItem item = ItemsInTheDropdown(FilterBoxOnShow(chartViewModel)).First(x => IdOf(x.DataContext) == id);
                item.SetCurrentValue(MultiComboBoxItem.IsSelectedProperty, !item.IsSelected);
                await SettleAsync();
            }

            await SetDropDownAsync(chartViewModel, false);
        }

        /// <summary>
        /// An item of the dropdown shows its text and its tick, and the box shows the text of the selected items. With the wrong
        /// style key, a subclass of the control does not get its theme: a box then shows nothing, and an item shows no tick.
        /// </summary>
        public async Task ItemsShouldShowTheirTextsAndTicksAsync(object chartViewModel, params int[] ids)
        {
            await SetDropDownAsync(chartViewModel, true);
            MultiComboBox box = FilterBoxOnShow(chartViewModel);

            foreach (int id in ids)
            {
                MultiComboBoxItem item = ItemsInTheDropdown(box).First(x => IdOf(x.DataContext) == id);
                item.GetVisualDescendants().OfType<TextBlock>().Select(x => x.Text).ShouldContain(x => x != null && x.StartsWith($@"{id} - ", StringComparison.Ordinal), @"the text of an item in the dropdown");
                PathIcon tick = item.GetVisualDescendants().OfType<PathIcon>().ShouldHaveSingleItem(@"the tick of an item in the dropdown");
                tick.Opacity.ShouldBe(0, @"an item that is not selected shows no tick");

                item.SetCurrentValue(MultiComboBoxItem.IsSelectedProperty, true);
                await SettleAsync();

                tick.Opacity.ShouldBe(1, @"a selected item shows its tick");
            }

            await SetDropDownAsync(chartViewModel, false);

            foreach (int id in ids)
            {
                box.GetVisualDescendants().OfType<TextBlock>().Select(x => x.Text).ShouldContain(x => x != null && x.StartsWith($@"{id} - ", StringComparison.Ordinal), @"the text of a selected item in the box");
            }
        }

        private int[] Selection(object chartViewModel) =>
            [.. (ReferenceEquals(chartViewModel, Gantt) ? Gantt.ActivitySelector.SelectedActivityIds : EarnedValue.ResourceSelector.SelectedResourceIds).Order()];

        private int[] Persisted(object chartViewModel) =>
            [.. (ReferenceEquals(chartViewModel, Gantt) ? Core.DisplaySettingsViewModel.GanttChartShowConnections : Core.DisplaySettingsViewModel.EarnedValueShowResources).Order()];

        /// <summary>
        /// The chart on show has this filter, and opening its dropdown shows this filter ticked and changes nothing. The check
        /// on the update says that nothing took the items for an edit by the user.
        /// </summary>
        public async Task FilterShouldBeAsync(object chartViewModel, bool isUpdated, params int[] expected)
        {
            Selection(chartViewModel).ShouldBe(expected, @"the selection after the load");

            await SetDropDownAsync(chartViewModel, true);

            int[] ticked = [.. ItemsInTheDropdown(FilterBoxOnShow(chartViewModel)).Where(x => x.IsSelected).Select(x => IdOf(x.DataContext)).Order()];
            ticked.ShouldBe(expected, @"the items ticked in the open dropdown");
            Selection(chartViewModel).ShouldBe(expected, @"the selection with the dropdown open");
            Persisted(chartViewModel).ShouldBe(expected, @"the persisted filter with the dropdown open");
            Core.IsProjectScenarioUpdated.ShouldBe(isUpdated, @"opening a dropdown marks the scenario as updated only if it changes something");

            await SetDropDownAsync(chartViewModel, false);

            Selection(chartViewModel).ShouldBe(expected, @"the selection with the dropdown closed");
            ShouldHaveNoErrors();
        }

        /// <summary>
        /// What a save would write for this scenario.
        /// </summary>
        public ProjectScenarioDisplaySettingsModel SavedDisplaySettings(IManagedNodeViewModel scenario) =>
            Project.BuildProject().Files.Single(x => x.NodeId == scenario.Id).Scenario.DisplaySettings;

        public void ShouldHaveNoErrors()
        {
            m_Dialogs.Errors.ShouldBeEmpty();
            m_Fixture.UnhandledExceptions.ShouldBeEmpty();
        }

        public void ForgetErrors()
        {
            m_Dialogs.Errors.Clear();
            m_Fixture.UnhandledExceptions.Clear();
        }
    }
}
