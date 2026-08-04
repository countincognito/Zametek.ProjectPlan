using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using SkiaSharp;

namespace Zametek.Graphs.Avalonia
{
    // Renders the interactive graph to a raster image at HIGH fidelity by building a throwaway, off-screen
    // copy of just the node/edge BODIES (the swappable NodeTemplate/EdgeTemplate) at their layout
    // positions. Selection, dimming, drag and pan/zoom live in the control chrome - not the body - so the
    // replica is neutral by construction, with no need to touch or mutate the live on-screen control. The
    // template visuals are materialised directly via IDataTemplate.Build (a detached ContentPresenter does
    // not realise its templated child, so it must be built explicitly), then the tree is laid out and
    // rendered to a RenderTargetBitmap. The bitmap is wrapped in an SKPicture so the shared ImageExporter
    // can emit it as PNG/JPEG or embed it in SVG/PDF, exactly like the vector path. Whatever the real
    // templates draw (gradients, drop shadows, arbitrary shapes) appears verbatim. Must be called on the UI
    // thread.
    internal static class InteractiveGraphRasterRenderer
    {
        // The same crop margin the vector renderer uses, so both exports frame the graph identically.
        private const double c_Padding = 16.0;

        public static SKPicture? RenderPicture(
            IReadOnlyList<GraphNodeViewModel> nodes,
            IReadOnlyList<GraphEdgeViewModel> edges,
            IDataTemplate? nodeTemplate,
            IDataTemplate? edgeTemplate,
            GraphTheme theme,
            double scale)
        {
            ArgumentNullException.ThrowIfNull(nodes);
            ArgumentNullException.ThrowIfNull(edges);

            if (nodes.Count == 0 || nodeTemplate is null || scale <= 0.0)
            {
                return null;
            }

            // Node bounding box (matches the vector renderer's crop).
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;
            foreach (GraphNodeViewModel node in nodes)
            {
                minX = Math.Min(minX, node.X);
                minY = Math.Min(minY, node.Y);
                maxX = Math.Max(maxX, node.X + node.Width);
                maxY = Math.Max(maxY, node.Y + node.Height);
            }

            double width = (maxX - minX) + (2.0 * c_Padding);
            double height = (maxY - minY) + (2.0 * c_Padding);

            // Shift every item so the bounding box starts after the margin. Node X/Y and edge geometry share
            // one workspace coordinate space, so the same offset applies to both (a node carries its own
            // position; an edge body draws in absolute coordinates, so its host carries the shift).
            double offsetX = c_Padding - minX;
            double offsetY = c_Padding - minY;

            var content = new Canvas
            {
                Width = width,
                Height = height,
                ClipToBounds = false,
            };

            // Edges first so the nodes sit on top, mirroring the on-screen z-order.
            if (edgeTemplate is not null)
            {
                foreach (GraphEdgeViewModel edge in edges)
                {
                    if (edgeTemplate.Build(edge) is Control edgeVisual)
                    {
                        edgeVisual.DataContext = edge;
                        Canvas.SetLeft(edgeVisual, offsetX);
                        Canvas.SetTop(edgeVisual, offsetY);
                        content.Children.Add(edgeVisual);
                    }
                }
            }

            foreach (GraphNodeViewModel node in nodes)
            {
                if (nodeTemplate.Build(node) is Control nodeVisual)
                {
                    nodeVisual.DataContext = node;
                    // Size the body to the node's bounds (on screen the control chrome does this; here the
                    // body template root is sized directly).
                    nodeVisual.Width = node.Width;
                    nodeVisual.Height = node.Height;
                    Canvas.SetLeft(nodeVisual, node.X + offsetX);
                    Canvas.SetTop(nodeVisual, node.Y + offsetY);
                    content.Children.Add(nodeVisual);
                }
            }

            // Theme background behind the content, baked into the raster so it matches the on-screen
            // canvas. Immutable like every brush the library creates (see the THREADING note on
            // GraphAppearance).
            var background = new ImmutableSolidColorBrush(theme == GraphTheme.Dark
                ? ColorHelper.DarkThemeBackground
                : ColorHelper.LightThemeBackground);

            var root = new Border
            {
                Width = width,
                Height = height,
                Background = background,
                ClipToBounds = true,
                Child = content,
            };

            // Lay the throwaway tree out (so the built visuals measure/arrange and their bindings settle),
            // then render it to a bitmap at the requested supersample scale.
            var size = new Size(width, height);
            root.Measure(size);
            root.Arrange(new Rect(size));

            var pixelSize = new PixelSize(
                Math.Max(1, (int)Math.Ceiling(width * scale)),
                Math.Max(1, (int)Math.Ceiling(height * scale)));
            var dpi = new Vector(96.0 * scale, 96.0 * scale);

            using var renderBitmap = new RenderTargetBitmap(pixelSize, dpi);
            renderBitmap.Render(root);

            // Wrap the bitmap in a picture so the shared exporter handles every format (embedding the raster
            // into SVG/PDF). PNG-encode the Avalonia bitmap, then decode into a Skia image.
            using var stream = new MemoryStream();
            renderBitmap.Save(stream, PngBitmapEncoderOptions.Default);
            using SKImage? image = SKImage.FromEncodedData(stream.ToArray());
            if (image is null)
            {
                return null;
            }

            using var recorder = new SKPictureRecorder();
            SKCanvas skCanvas = recorder.BeginRecording(new SKRect(0, 0, pixelSize.Width, pixelSize.Height));
            skCanvas.DrawImage(image, 0, 0);
            return recorder.EndRecording();
        }
    }
}
