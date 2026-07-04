namespace Zametek.Graphs.Avalonia
{
    // Chooses how an interactive-canvas export (copy or save) draws its nodes and edges.
    public enum GraphExportMode
    {
        // Draw the nodes and edges imperatively with SkiaSharp, following a GraphVectorExportStyle. The
        // result is a true vector picture, so PNG/JPEG stay sharp AND SVG/PDF remain crisp scalable vectors.
        // It does not reproduce a custom NodeTemplate/EdgeTemplate exactly - only the configured shapes.
        Vector,

        // Render the actual on-screen node/edge templates (the real Avalonia visuals - gradients, shadows,
        // arbitrary shapes) to a bitmap. Highest fidelity, pixel-exact to the interactive canvas, but
        // raster: SVG/PDF embed the bitmap rather than staying vector. Requires a live control.
        Raster
    }
}
