namespace Zametek.Graphs.Avalonia
{
    // An obstacle node in a routing request: its rectangle in interactive (screen) coordinates.
    public readonly record struct EdgeRoutingNode(int Id, double X, double Y, double Width, double Height);
}
