using Zametek.Graphs.Avalonia;
using Zametek.Graphs.Avalonia.TestApp.Graphs;

namespace Zametek.Graphs.Avalonia.TestApp.ViewModels
{
    // Tab 2: an activity-on-node graph in the library's default presentation. Its paired view is
    // VertexDefaultGraphTabView. The diagram factory ignores the show-names flag (the vertex graph has no
    // edge labels, so GraphConfigurations.Vertex hides that toggle).
    public sealed class VertexDefaultGraphTabViewModel
        : GraphTabViewModelBase
    {
        public VertexDefaultGraphTabViewModel(GraphTheme initialTheme)
            : base(
                title: @"Vertex · default",
                buildDiagram: _ => SampleGraphs.BuildVertex(),
                configuration: GraphConfigurations.Vertex,
                appearance: null,
                initialTheme: initialTheme,
                suggestedFileName: @"vertex-graph-default.png")
        {
        }
    }
}
