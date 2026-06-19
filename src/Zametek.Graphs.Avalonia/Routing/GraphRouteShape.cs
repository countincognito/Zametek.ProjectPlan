namespace Zametek.Graphs.Avalonia
{
    // The orthogonal route family an edge draws (rectilinear modes only). Direct is the everyday L
    // (mixed axes, one bend) or Z (matching axes, two bends, corner between the endpoints). Bracket and
    // Saucepan are the clash-avoidance detours the resolver reaches for when an obstacle sits where a
    // Direct route would cross it:
    //   - Bracket ("U"): matching axes, the cross leg slid OUTSIDE the endpoints (above/below or
    //     left/right of the blocking node) - two bends, both ends leaving the same way.
    //   - Saucepan: a "U" bowl that dips around the obstacle, with a short "handle" stub on the source
    //     end, the target end, or BOTH. A handled end leaves on a side perpendicular to the bowl's arms
    //     (so it can keep a horizontal entry/exit while the bowl detours vertically); a direct end
    //     attaches straight onto an arm. So three bends is the minimum (one handle) and four the next
    //     (two handles) - the both-handle form is what the settled MSAGL route shows for an obstacle
    //     squarely between two level nodes.
    public enum GraphRouteShape
    {
        Direct,
        Bracket,
        Saucepan,
    }
}
