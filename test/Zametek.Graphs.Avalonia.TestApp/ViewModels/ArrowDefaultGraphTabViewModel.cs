using Zametek.Graphs.Avalonia;
using Zametek.Graphs.Avalonia.TestApp.Graphs;

namespace Zametek.Graphs.Avalonia.TestApp.ViewModels
{
    // Tab 1: an activity-on-arrow graph in the library's default presentation. No NodeTemplate /
    // EdgeTemplate and no GraphAppearance are supplied, so it renders exactly as ProjectPlan's arrow
    // graph does. Its paired view is ArrowDefaultGraphTabView.
    public sealed class ArrowDefaultGraphTabViewModel
        : GraphTabViewModelBase
    {
        public ArrowDefaultGraphTabViewModel(GraphTheme initialTheme)
            : base(
                title: @"Arrow · default",
                buildDiagram: SampleGraphs.BuildArrow,
                configuration: GraphConfigurations.Arrow,
                appearance: null,
                initialTheme: initialTheme,
                suggestedFileName: @"arrow-graph-default.png")
        {
        }
    }
}
