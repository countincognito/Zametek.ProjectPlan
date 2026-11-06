using Microsoft.Msagl.Drawing;
using System.Globalization;
using System.Xml;

namespace Zametek.Graphs.Avalonia
{
    // Msagl's SvgGraphWriter with our label-writing override (multi-line tspans, sanitised text) and an
    // order-preserving Write. Tied to Microsoft.Msagl.Drawing - an Msagl implementation detail of the SVG
    // render path, not part of the library's public surface.
    internal class MsaglSvgGraphWriter
        : SvgGraphWriter
    {
        private readonly static Func<string, string> s_NodeSanitizer = (unescaped) => new System.Xml.Linq.XText(unescaped).ToString();

        private readonly Graph? m_Graph;

        public MsaglSvgGraphWriter()
                : base()
        {
            NodeSanitizer = s_NodeSanitizer;
        }

        public MsaglSvgGraphWriter(Stream streamPar, Graph graphP)
            : base(streamPar, graphP)
        {
            m_Graph = graphP;
            NodeSanitizer = s_NodeSanitizer;
        }

        // SvgGraphWriter.Write, but writing the edges and nodes in the order given. Write takes them from
        // Graph.Edges and Graph.Nodes, whose order follows MSAGL's Hashtable of node ids and so changes from
        // one process to the next. Write, WriteEdges and WriteNodes are not virtual, so this replays Write's
        // own steps with the same comments: flip Y, open the document, the graph label, the edges, the nodes,
        // close. (Write's other step, WriteGraphAttr, is private and does nothing.) WriteOpening switches the
        // thread to the invariant culture, so that numbers are written with a '.', and does not switch it
        // back, so the caller's culture is restored here.
        public void WriteInOrder(IEnumerable<Node> nodes, IEnumerable<Edge> edges)
        {
            ArgumentNullException.ThrowIfNull(nodes);
            ArgumentNullException.ThrowIfNull(edges);

            if (m_Graph is null)
            {
                throw new InvalidOperationException(@"This writer was created without a graph to write.");
            }

            CultureInfo callerCulture = Thread.CurrentThread.CurrentCulture;

            try
            {
                TransformGraphByFlippingY();
                WriteOpening();
                WriteLabel(m_Graph.Label);

                if (!IgnoreEdges)
                {
                    WriteComment(@"Edges");
                    foreach (Edge edge in edges)
                    {
                        WriteEdge(edge);
                    }
                }

                WriteComment(@"nodes");
                foreach (Node node in nodes)
                {
                    WriteNode(node);
                }
                WriteComment(@"end of nodes");

                Close();
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = callerCulture;
            }
        }

        protected override void WriteLabel(Label label)
        {
            if (LabelIsValid(label))
            {
                double num = label.Center.X - label.Width / 2.0;
                double num2 = label.Center.Y + label.Height / 3.0;
                WriteStartElement(@"text");
                WriteAttribute(@"x", num);
                WriteAttribute(@"y", num2);
                WriteAttribute(@"font-family", label.FontName);
                WriteAttribute(@"font-size", label.FontSize);
                WriteAttribute(@"fill", MsaglColorToSvgColor(label.FontColor));
                WriteLabelText(label.Text, num, label.FontSize);
                WriteEndElement();
            }
        }

        private static bool LabelIsValid(Label label)
        {
            if (label is null || string.IsNullOrEmpty(label.Text) || label.Width == 0.0)
            {
                return false;
            }

            return true;
        }

        private void WriteLabelText(string text, double xContainer, double fontSize)
        {
            List<string> textLines = [.. NewLineHelper.SplitLines(NodeSanitizer(text))];
            bool isFirstLine = true;
            textLines.ForEach(delegate (string line)
            {
                WriteStartElement(@"tspan");
                WriteAttribute(@"x", xContainer);
                if (isFirstLine)
                {
                    isFirstLine = false;
                    WriteAttribute(@"dy", -1.0 * fontSize * (double)(textLines.Count - 1));
                }
                else
                {
                    WriteAttribute(@"dy", fontSize);
                }

                XmlWriter.WriteRaw(line);
                WriteEndElement();
            });
        }
    }
}
