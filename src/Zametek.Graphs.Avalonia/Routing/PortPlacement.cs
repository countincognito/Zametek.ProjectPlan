using Avalonia;

namespace Zametek.Graphs.Avalonia
{
    // One edge's resolved attach points (the source and target ends, in screen coordinates), the input
    // the port-offset resolver groups by side. Taken from the route the clash resolver chose, so a
    // detour end is grouped by where it actually attaches, not the far node's direction.
    public readonly record struct PortPlacement(int EdgeId, int SourceId, int TargetId, Point SourceAttach, Point TargetAttach);
}
