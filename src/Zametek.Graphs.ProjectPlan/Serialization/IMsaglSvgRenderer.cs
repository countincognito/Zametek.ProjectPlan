using Zametek.Common.ProjectPlan;

namespace Zametek.Graphs.ProjectPlan
{
    public interface IMsaglSvgRenderer
    {
        byte[] RenderToSvg(Microsoft.Msagl.Drawing.Graph graph, BaseTheme theme);
    }
}
