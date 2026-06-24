namespace Zametek.Graphs.Avalonia
{
    // An edge in a port-resolution snapshot: its id, its source/target node ids, and the per-endpoint
    // connection axes it would use on its own (its tentative resolve, before clash avoidance /
    // offsetting). See PortNode for the companion node record.
    public readonly record struct PortEdge(
        int Id,
        int SourceId,
        int TargetId,
        GraphConnectionAxis SourceAxis,
        GraphConnectionAxis TargetAxis);
}
