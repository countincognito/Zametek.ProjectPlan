using Zametek.Common.ProjectPlan;
using Zametek.Graphs.Avalonia;
using CommonGraphLayoutModel = Zametek.Common.ProjectPlan.GraphLayoutModel;

namespace Zametek.ViewModel.ProjectPlan
{
    // Maps the interactive graph's node positions (the Graphs library's GraphNodePosition, in layout
    // space) to and from the persisted Common.GraphLayoutModel at the boundary, so neither side depends
    // on the other's model. (Both projects happen to use the name GraphLayoutModel, hence the alias.)
    internal static class GraphLayoutMapper
    {
        public static CommonGraphLayoutModel ToGraphLayoutModel(this IReadOnlyList<GraphNodePosition> positions)
        {
            return new CommonGraphLayoutModel
            {
                Nodes = [.. positions.Select(p => new NodeLayoutModel { Id = p.Id, X = p.X, Y = p.Y })],
            };
        }

        public static IReadOnlyList<GraphNodePosition> ToNodePositions(this CommonGraphLayoutModel layout)
        {
            return [.. layout.Nodes.Select(n => new GraphNodePosition(n.Id, n.X, n.Y))];
        }
    }
}
