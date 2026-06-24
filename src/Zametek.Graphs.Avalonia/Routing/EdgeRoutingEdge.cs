namespace Zametek.Graphs.Avalonia
{
    // An edge to route, identified by its endpoints' node ids.
    public readonly record struct EdgeRoutingEdge(int Id, int SourceId, int TargetId);
}
