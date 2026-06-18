namespace Zametek.Graphs.ProjectPlan
{
    // The immutable snapshot the drag-time port resolvers work on: nodes and edges in screen
    // coordinates, taken on the UI thread so the pure resolvers (GraphClashResolver clash avoidance and
    // GraphPortOffsetResolver port offsetting) can run on it without touching the live view-models.

    // A node: its id and centre.
    internal readonly record struct PortNode(int Id, double CentreX, double CentreY);

    // An edge: its id, its source/target node ids, and the per-endpoint connection axes it would use on
    // its own (its tentative resolve, before clash avoidance / offsetting).
    internal readonly record struct PortEdge(
        int Id,
        int SourceId,
        int TargetId,
        GraphConnectionAxis SourceAxis,
        GraphConnectionAxis TargetAxis);
}
