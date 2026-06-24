using System.Xml.Serialization;
using Zametek.Utility;

namespace Zametek.Graphs.Avalonia
{
    // The default IGraphSerializer: serialises a library-neutral DiagramGraphModel to GraphML or
    // GraphViz via the neutral GraphMLBuilder / GraphVizBuilder. Stateless and framework-neutral - the
    // layout/render outputs (interactive layout + fixed SVG) live on IGraphLayoutEngine
    // (MsaglGraphLayoutEngine), so all the Microsoft.Msagl code this class used to hold has moved there.
    // A single serializer serves both the arrow and vertex graphs.
    // (Replaces the parallel ArrowGraphSerializer/VertexGraphSerializer.)
    public class GraphSerializer
        : IGraphSerializer
    {
        public byte[] BuildGraphMLData(DiagramGraphModel diagramGraph)
        {
            ArgumentNullException.ThrowIfNull(diagramGraph);
            graphml graphML = GraphMLBuilder.ToGraphML(diagramGraph);
            using var ms = new MemoryStream();
            var xmlSerializer = new XmlSerializer(typeof(graphml));
            xmlSerializer.Serialize(ms, graphML);
            ms.Position = 0;
            using var sr = new StreamReader(ms);
            string content = sr.ReadToEnd();
            return content.StringToByteArray();
        }

        public byte[] BuildGraphVizData(DiagramGraphModel diagramGraph)
        {
            ArgumentNullException.ThrowIfNull(diagramGraph);
            string graphviz = GraphVizBuilder.ToGraphViz(diagramGraph);
            return graphviz.StringToByteArray();
        }
    }
}
