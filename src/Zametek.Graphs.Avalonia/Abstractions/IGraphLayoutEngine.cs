namespace Zametek.Graphs.Avalonia
{
    // The graph layout/render engine: turns a library-neutral DiagramGraphModel into either the
    // interactive GraphLayoutModel (resolved node coordinates the interactive control places its
    // controls at) or a fixed-layout SVG, under a GraphConfiguration. Framework-neutral by design -
    // the only implementation coupled to a layout library is the Msagl-backed MsaglGraphLayoutEngine,
    // so a consumer could substitute a different engine without touching the abstractions. Stateless:
    // the live GraphConfiguration is supplied per call by the caller (so one engine instance can serve
    // graphs with different configurations).
    public interface IGraphLayoutEngine
    {
        GraphLayoutModel BuildLayout(DiagramGraphModel diagramGraph, GraphConfiguration configuration, GraphTheme theme);

        byte[] RenderSvg(DiagramGraphModel diagramGraph, GraphConfiguration configuration, GraphTheme theme);
    }
}
