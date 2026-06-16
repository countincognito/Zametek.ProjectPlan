using SkiaSharp;

namespace Zametek.Graphs.ProjectPlan
{
    // Writes a recorded graph picture to an image file. Lives in the control library so the
    // reusable interactive graph view-models can export images without the application's help.
    public interface IGraphImageExporter
    {
        Task SaveGraphImageAsync(SKPicture picture, string filename, int scaleX = 2, int scaleY = 2);
    }
}
