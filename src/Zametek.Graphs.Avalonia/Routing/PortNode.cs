namespace Zametek.Graphs.Avalonia
{
    // A node in a port-resolution snapshot (taken on the UI thread for the drag-time resolvers): just
    // its id and centre, in screen coordinates. See PortEdge for the companion edge record.
    public readonly record struct PortNode(int Id, double CentreX, double CentreY);
}
