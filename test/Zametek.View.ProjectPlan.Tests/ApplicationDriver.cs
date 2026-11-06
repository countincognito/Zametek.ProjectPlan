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
using Zametek.Graphs.Avalonia;
using Zametek.ViewModel.ProjectPlan;

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
            Resources = container.Resolve<IResourceSettingsManagerViewModel>();
            Effort = container.Resolve<IEffortTrackingManagerViewModel>();
            Progress = container.Resolve<IProgressTrackingManagerViewModel>();
            ArrowGraph = (InteractiveGraphViewModel)((ArrowGraphManagerViewModel)container.Resolve<IArrowGraphManagerViewModel>()).Interactive;
            VertexGraph = (InteractiveGraphViewModel)((VertexGraphManagerViewModel)container.Resolve<IVertexGraphManagerViewModel>()).Interactive;
        }

        public IMainViewModel Main { get; }

        public ICoreViewModel Core { get; }

        public IProjectScenarioManagerViewModel Project { get; }

        public IGanttChartManagerViewModel Gantt { get; }

        public IEarnedValueChartManagerViewModel EarnedValue { get; }

        public IActivitiesManagerViewModel Activities { get; }

        public IResourceSettingsManagerViewModel Resources { get; }

        public IEffortTrackingManagerViewModel Effort { get; }

        public IProgressTrackingManagerViewModel Progress { get; }

        public InteractiveGraphViewModel ArrowGraph { get; }

        public InteractiveGraphViewModel VertexGraph { get; }

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

        public async Task UntilAsync(Func<bool> condition, string what)
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

        /// <summary>
        /// Asks for a load that is to fail, and waits for the error that says so.
        /// </summary>
        public async Task FailToLoadAsync(IManagedNodeViewModel scenario)
        {
            Select(scenario);
            Project.LoadSelectedProjectScenarioFileCommand.Execute(null);
            await UntilAsync(() => m_Dialogs.Errors.Count > 0, $@"the error of the load of {scenario.Name}");
            m_Dialogs.Errors.ShouldHaveSingleItem();
            m_Fixture.UnhandledExceptions.ShouldBeEmpty();
            ForgetErrors();
        }

        internal RoutingModeFault FailRoutingModeChange(int failingChange) => new(Core, failingChange);

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

        public Task OpenDropDownAsync(object chartViewModel) => SetDropDownAsync(chartViewModel, true);

        public Task CloseDropDownAsync(object chartViewModel) => SetDropDownAsync(chartViewModel, false);

        /// <summary>
        /// The number of items that the dropdown of the chart has from its list. The dropdown makes controls only for the items
        /// that it shows.
        /// </summary>
        public int ItemCountOfTheDropdown(object chartViewModel) => FilterBoxOnShow(chartViewModel).ItemCount;

        /// <summary>
        /// The texts that the box of the filter of the chart shows, with its dropdown closed: one for each selected item.
        /// </summary>
        public List<string> TextsOfTheBox(object chartViewModel) =>
            [.. FilterBoxOnShow(chartViewModel).GetVisualDescendants().OfType<TextBlock>().Select(x => x.Text).OfType<string>()];

        /// <summary>
        /// The text of each item that the open dropdown of the chart shows, by the id of the item. The dropdown makes controls only for
        /// the items that it shows.
        /// </summary>
        public async Task<Dictionary<int, string>> TextsOfTheItemsAsync(object chartViewModel)
        {
            await SetDropDownAsync(chartViewModel, true);

            try
            {
                return ItemsInTheDropdown(FilterBoxOnShow(chartViewModel)).ToDictionary(
                    item => IdOf(item.DataContext),
                    item => item.GetVisualDescendants().OfType<TextBlock>().Select(x => x.Text).OfType<string>().Single());
            }
            finally
            {
                await SetDropDownAsync(chartViewModel, false);
            }
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

        private static bool Shows(InteractiveGraphViewModel graph, GraphEdgeRoutingMode mode) =>
            graph.EdgeRoutingMode == mode
                && graph.GraphEdges.Count > 0
                && graph.GraphEdges.All(x => x.RoutingMode == mode);

        /// <summary>
        /// Each graph gets to this mode, on its edges too. A graph rebuilds its edges after a load, and this waits for that.
        /// </summary>
        public Task GraphsShouldShowAsync(GraphEdgeRoutingMode arrow, GraphEdgeRoutingMode vertex) =>
            UntilAsync(() => Shows(ArrowGraph, arrow) && Shows(VertexGraph, vertex), $@"the graphs with the modes {arrow} and {vertex}");

        public IManagedResourceViewModel Resource(int resourceId) => Resources.RawResources.Single(x => x.Id == resourceId);

        /// <summary>
        /// Selects an activity as a click on its row in the grid does.
        /// </summary>
        public void SelectActivity(int activityId)
        {
            var manager = (ActivitiesManagerViewModel)Activities;
            manager.SelectedActivities.Clear();
            manager.SelectedActivities[activityId] = Core.RawActivities.Single(x => x.Id == activityId);
            manager.HasSelectedActivities = true;
        }

        public async Task RemoveActivityAsync(int activityId)
        {
            SelectActivity(activityId);
            Activities.RemoveManagedActivitiesCommand.Execute(null);
            await UntilAsync(() => Core.RawActivities.All(x => x.Id != activityId), $@"activity {activityId} removed");
        }

        /// <summary>
        /// The selector of the Gantt chart and the first day of this resource have each activity of the plan, and nothing else.
        /// </summary>
        public Task SelectorsShouldFollowThePlanAsync(int resourceId)
        {
            // This reads the lists on the UI thread while revisions run on other threads. The list of the Gantt chart changes only on
            // the UI thread, and a day gives an array that a revision replaces, and so the read never meets a change.
            bool Follows()
            {
                int[] plan = [.. Core.RawActivities.Select(x => x.Id).Order()];

                return Gantt.ActivitySelector.TargetActivities.Select(x => x.Id).Order().SequenceEqual(plan)
                    && Resources.RawResources.Any(x => x.Id == resourceId)
                    && Resource(resourceId).TrackerSet.GetDay(0).TargetResourceActivities.Select(x => x.Id).Order().SequenceEqual(plan);
            }

            return UntilAsync(Follows, $@"the selectors with the activities of the plan");
        }

        /// <summary>
        /// The texts of the day cells with a value in the row of this activity, in the grid of the progress tab, day by day.
        /// </summary>
        public List<string> ProgressCellsOf(int activityId)
        {
            DataGrid grid = m_Window.GetVisualDescendants().OfType<DataGrid>().First(x => x.Name == @"TrackerActivitiesGrid");
            DataGridRow? row = grid.GetVisualDescendants().OfType<DataGridRow>()
                .FirstOrDefault(x => x.DataContext is IManagedActivityViewModel activity && activity.Id == activityId);

            // Only the day cells have this class. Another column of the row shows the last value, also with a percent sign.
            return row is null
                ? []
                : [.. row.GetVisualDescendants().OfType<Grid>()
                    .Where(x => x.Classes.Contains(@"editable"))
                    .SelectMany(x => x.Children.OfType<TextBlock>())
                    .Select(x => x.Text)
                    .OfType<string>()
                    .Where(x => x.EndsWith('%'))];
        }

        /// <summary>
        /// What a save would write for this scenario.
        /// </summary>
        public ProjectScenarioModel SavedScenario(IManagedNodeViewModel scenario) =>
            Project.BuildProject().Files.Single(x => x.NodeId == scenario.Id).Scenario;

        /// <summary>
        /// The display settings that a save would write for this scenario.
        /// </summary>
        public ProjectScenarioDisplaySettingsModel SavedDisplaySettings(IManagedNodeViewModel scenario) =>
            SavedScenario(scenario).DisplaySettings;

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
