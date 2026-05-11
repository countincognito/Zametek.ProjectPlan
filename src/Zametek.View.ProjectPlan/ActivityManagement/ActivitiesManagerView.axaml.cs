using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Xaml.Interactivity;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class ActivitiesManagerView
        : UserControl
    {
        private IDisposable? m_ScrollToActivitySub;

        public ActivitiesManagerView()
        {
            InitializeComponent();
            AttachScrollBehavior();
        }

        public ActivitiesManagerView(IDataGridManager dataGridManager)
        {
            ArgumentNullException.ThrowIfNull(dataGridManager);
            InitializeComponent();
            BehaviorCollection behaviors = Interaction.GetBehaviors(ActivitiesGrid);
            behaviors.Add(new DataGridPersistBehavior(dataGridManager));
            AttachScrollBehavior();
        }

        private void AttachScrollBehavior()
        {
            DataContextChanged += (_, _) =>
            {
                m_ScrollToActivitySub?.Dispose();
                if (DataContext is IActivitiesManagerViewModel vm)
                {
                    m_ScrollToActivitySub = vm
                        .WhenAnyValue(x => x.ScrollToActivityId)
                        .Skip(1)
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(id => ScrollGridToActivity(id));
                }
            };
        }

        private void ScrollGridToActivity(int activityId)
        {
            if (DataContext is not IActivitiesManagerViewModel vm)
            {
                return;
            }

            var item = vm.Activities.FirstOrDefault(a => a.Id == activityId);
            if (item is not null)
            {
                ActivitiesGrid.SelectedItem = item;
                ActivitiesGrid.ScrollIntoView(item, null);
            }
        }

        /// <summary>
        /// Find a DataGridColumn by its Tag string.
        /// </summary>
        private DataGridColumn? FindColumn(string tag) =>
            ActivitiesGrid.Columns.FirstOrDefault(c => c.Tag?.ToString() == tag);

        private void SetColumnsVisible(bool visible, params string[] tags)
        {
            foreach (string tag in tags)
            {
                DataGridColumn? col = FindColumn(tag);
                if (col is not null)
                {
                    col.IsVisible = visible;
                }
            }
        }

        private void ToggleTiming_IsCheckedChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            bool show = (sender as ToggleButton)?.IsChecked == true;
            SetColumnsVisible(show,
                "ColEarliestStartTime",
                "ColLatestStartTime",
                "ColEarliestFinishTime",
                "ColLatestFinishTime");
        }

        private void ToggleSlack_IsCheckedChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            bool show = (sender as ToggleButton)?.IsChecked == true;
            SetColumnsVisible(show,
                "ColTotalSlack",
                "ColFreeSlack",
                "ColInterferingSlack");
        }

        private void ToggleFlags_IsCheckedChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            bool show = (sender as ToggleButton)?.IsChecked == true;
            SetColumnsVisible(show,
                "ColIsDummy",
                "ColIsIsolated",
                "ColIsCritical",
                "ColHasNoEffort",
                "ColHasNoRisk",
                "ColHasNoCost",
                "ColHasNoBilling");
        }

        private void ToggleResourcesDetail_IsCheckedChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            bool show = (sender as ToggleButton)?.IsChecked == true;
            SetColumnsVisible(show,
                "ColResourceDependencies",
                "ColAllocatedToResources",
                "ColTargetResourceOperator");
        }

        private void ToggleNotes_IsCheckedChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            bool show = (sender as ToggleButton)?.IsChecked == true;
            SetColumnsVisible(show,
                "ColNotes",
                "ColPercentageCompleted");
        }
    }
}
