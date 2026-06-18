namespace Zametek.Graphs.ProjectPlan
{
    // An edge to route, identified by its endpoints' node ids.
    internal readonly record struct EdgeRoutingEdge(int Id, int SourceId, int TargetId);
}
