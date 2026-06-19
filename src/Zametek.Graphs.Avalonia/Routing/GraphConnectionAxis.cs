namespace Zametek.Graphs.Avalonia
{
    // Which axis an interactive edge leaves the source / enters the target along: Horizontal = the
    // left/right node sides, Vertical = the top/bottom sides. Chosen per endpoint (see the interactive
    // view-model's hybrid resolve) and fed into the spline/rectilinear builders so a vertically-stacked
    // arrangement connects top-to-bottom instead of always sideways.
    public enum GraphConnectionAxis
    {
        Horizontal,
        Vertical,
    }
}
