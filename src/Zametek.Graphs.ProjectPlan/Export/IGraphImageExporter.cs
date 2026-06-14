using SkiaSharp;

namespace Zametek.Graphs.ProjectPlan
{
    public interface IGraphImageExporter
    {
        Task SaveGraphImageAsync(SKPicture picture, string filename, int scaleX = 2, int scaleY = 2);
    }
}
