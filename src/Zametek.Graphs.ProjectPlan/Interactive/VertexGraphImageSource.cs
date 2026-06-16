namespace Zametek.Graphs.ProjectPlan
{
    // Chooses which rendering an image export is produced from. The on-screen Save-As uses the
    // interactive canvas (what the user has dragged); a headless caller (e.g. the CLI) uses the
    // fixed MSAGL layout, which does not require a populated interactive surface.
    public enum VertexGraphImageSource
    {
        // The interactive node/edge canvas, exactly as currently arranged on screen.
        InteractiveCanvas,

        // The default MSAGL layout, built straight from the diagram (no interactive surface needed).
        FixedLayout
    }
}
