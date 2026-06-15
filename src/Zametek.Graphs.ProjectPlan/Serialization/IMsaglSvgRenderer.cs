namespace Zametek.Graphs.ProjectPlan
{
    public interface IMsaglSvgRenderer
    {
        byte[] RenderToSvg(Microsoft.Msagl.Drawing.Graph graph, GraphTheme theme);
    }
}
