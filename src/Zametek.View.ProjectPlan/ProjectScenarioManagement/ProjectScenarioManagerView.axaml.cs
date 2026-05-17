using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class ProjectScenarioManagerView
        : UserControl
    {
        public ProjectScenarioManagerView()
        {
            InitializeComponent();
        }

        public ProjectScenarioManagerView(ICommitEditHandler commitEditHandler)
        {
            ArgumentNullException.ThrowIfNull(commitEditHandler);
            InitializeComponent();

            // This is to ensure that all datagrids are committed on any
            // edits that they may have had open when a user tries to load
            // a different scenario.
            // This setup will also catch pointer presses on treeview items.
            scenarioTree.AddHandler(PointerPressedEvent, (sender, e) =>
            {
                commitEditHandler.CommitEdit();
            }, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        }
    }
}
