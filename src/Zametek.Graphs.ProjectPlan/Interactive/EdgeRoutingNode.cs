namespace Zametek.Graphs.ProjectPlan
{
    // An obstacle node in a routing request: its rectangle in interactive (screen) coordinates.
    internal readonly record struct EdgeRoutingNode(int Id, double X, double Y, double Width, double Height);
}
