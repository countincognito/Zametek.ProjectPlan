using Zametek.Graphs.Avalonia;
using CommonGraphLayoutModel = Zametek.Common.ProjectPlan.GraphLayoutModel;

namespace Zametek.ViewModel.ProjectPlan
{
    internal static class GraphLayoutMapper
    {
        public static IReadOnlyList<GraphNodePosition> ToNodePositions(this CommonGraphLayoutModel layout)
        {
            return [.. layout.Nodes.Select(n => new GraphNodePosition(n.Id, n.X, n.Y))];
        }
    }
}
