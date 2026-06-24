namespace Zametek.Graphs.Avalonia
{
    // One node's position in layout space (the interactive workspace margin removed), used to save and
    // restore an interactive arrangement. Layout space - rather than the raw on-screen workspace
    // coordinates - keeps a persisted layout independent of the workspace margin.
    public readonly record struct GraphNodePosition(int Id, double X, double Y);
}
