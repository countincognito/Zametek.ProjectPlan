using SkiaSharp;

namespace Zametek.Graphs.Avalonia
{
    // A source of a rendered image of the interactive graph in its neutral state (no selection, dimming,
    // pan or zoom), cropped to the node bounding box. Implemented by the view (InteractiveGraphView), which
    // owns the node/edge templates, and registered on the view-model so the view-model's Save command can
    // obtain a picture WITHOUT referencing any Avalonia view or template type - only the SkiaSharp
    // primitive crosses the boundary. When no provider is registered (e.g. a headless caller with no live
    // control) the view-model falls back to the built-in vector renderer.
    public interface IGraphImageProvider
    {
        // The export mode the built-in Copy/Save actions use unless a specific mode is requested.
        GraphExportMode DefaultMode { get; }

        // Render the current graph to a recorded picture in the given mode: Vector draws crisp vector
        // shapes (a true vector picture, so SVG/PDF stay scalable); Raster rasterises the real templates
        // and wraps the bitmap in a picture (so SVG/PDF embed it). Returns null for an empty graph.
        SKPicture? RenderPicture(GraphExportMode mode);
    }
}
