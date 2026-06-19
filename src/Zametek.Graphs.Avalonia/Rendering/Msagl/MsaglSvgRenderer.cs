using System.Xml;
using Zametek.Utility;

namespace Zametek.Graphs.Avalonia
{
    // Renders a laid-out Msagl drawing graph to themed SVG bytes (the fixed-layout export path). An
    // internal Msagl implementation detail used by MsaglGraphLayoutEngine - it takes an
    // Microsoft.Msagl.Drawing.Graph, so it cannot be a framework-neutral abstraction (the former
    // public IMsaglSvgRenderer seam was removed for that reason).
    internal static class MsaglSvgRenderer
    {
        public static byte[] RenderToSvg(
            Microsoft.Msagl.Drawing.Graph graph,
            GraphTheme theme)
        {
            ArgumentNullException.ThrowIfNull(graph);

            using var ms = new MemoryStream();
            using var writer = new StreamWriter(ms);

            var svgWriter = new MsaglSvgGraphWriter(writer.BaseStream, graph);
            svgWriter.Write();
            ms.Position = 0;
            using var sr = new StreamReader(ms);

            using var xmlReader = XmlReader.Create(sr);

            XmlDocument doc = new();
            doc.Load(xmlReader);

            // Set the background to transparent so it
            string? height = doc.DocumentElement?.GetAttribute("height");
            string? width = doc.DocumentElement?.GetAttribute("width");

            var rect = doc.CreateElement(@"rect");
            rect.SetAttribute(@"height", height);
            rect.SetAttribute(@"width", width);

            if (theme == GraphTheme.Light)
            {
                rect.SetAttribute(@"fill", ColorHelper.SvgLightThemeBackground);
            }
            if (theme == GraphTheme.Dark)
            {
                rect.SetAttribute(@"fill", ColorHelper.SvgDarkThemeBackground);
            }

            // Only show the background if there is a graph to display.
            if (graph.NodeCount > 0)
            {
                rect.SetAttribute(@"fill-opacity", "1.0");
            }
            else
            {
                rect.SetAttribute(@"fill-opacity", "0.0");
            }

            // Add the background to the top of the XML tree.
            doc.DocumentElement?.PrependChild(rect);

            using var stringWriter = new StringWriter();
            using var xmlTextWriter = XmlWriter.Create(stringWriter);

            doc.WriteTo(xmlTextWriter);
            xmlTextWriter.Flush();

            return stringWriter
                .GetStringBuilder()
                .ToString()
                .StringToByteArray();
        }
    }
}
