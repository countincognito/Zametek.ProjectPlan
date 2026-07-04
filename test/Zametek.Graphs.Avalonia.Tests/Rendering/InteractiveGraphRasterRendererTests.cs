using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Media;
using Shouldly;
using SkiaSharp;
using Xunit;

namespace Zametek.Graphs.Avalonia.Tests.Rendering
{
    // Verifies the high-fidelity raster export end to end: the off-screen replica must actually MATERIALISE
    // the node template (via IDataTemplate.Build) and render its pixels - the bug this guards against was a
    // completely blank image, because a detached ContentPresenter never realised its templated child. Runs
    // on a headless Avalonia UI thread with the real Skia renderer so RenderTargetBitmap produces pixels.
    [Collection("Headless rendering")]
    public class InteractiveGraphRasterRendererTests
    {
        private readonly HeadlessUnitTestSession m_Session;

        public InteractiveGraphRasterRendererTests(HeadlessSessionFixture fixture)
        {
            m_Session = fixture.Session;
        }

        [Fact]
        public async Task RenderPicture_materialises_the_node_template_pixels()
        {
            await m_Session.Dispatch(() =>
            {
                var layout = new GraphNodeLayoutModel
                {
                    Id = 1,
                    X = 0.0,
                    Y = 0.0,
                    Width = 40.0,
                    Height = 30.0,
                    Label = @"1",
                    FillColorHexCode = @"#EAF1FB",
                    BorderColorHexCode = @"#333333",
                    BorderThickness = 1.0,
                };
                var node = new GraphNodeViewModel(layout, GraphAppearance.Default);
                var nodes = new List<GraphNodeViewModel> { node };
                var edges = new List<GraphEdgeViewModel>();

                // A deliberately vivid template so a materialised node is unmistakable in the output.
                var nodeTemplate = new FuncDataTemplate<GraphNodeViewModel>((_, _) => new Border { Background = Brushes.Red });

                using SKPicture? picture = InteractiveGraphRasterRenderer.RenderPicture(
                    nodes, edges, nodeTemplate, edgeTemplate: null, GraphTheme.Light, scale: 2.0);

                picture.ShouldNotBeNull();

                int width = (int)picture.CullRect.Width;
                int height = (int)picture.CullRect.Height;
                using var bitmap = new SKBitmap(width, height);
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(SKColors.White);
                    canvas.DrawPicture(picture);
                }

                // Node centre: (padding + Width/2, padding + Height/2) * scale = ((16 + 20) * 2, (16 + 15) * 2).
                SKColor centre = bitmap.GetPixel(72, 62);
                centre.Red.ShouldBeGreaterThan((byte)150);
                centre.Blue.ShouldBeLessThan((byte)120);

                // A corner (inside the margin) should still be the light-theme background, not the node.
                SKColor corner = bitmap.GetPixel(4, 4);
                corner.Red.ShouldBeGreaterThan((byte)200);
                corner.Green.ShouldBeGreaterThan((byte)200);
                corner.Blue.ShouldBeGreaterThan((byte)200);
            }, CancellationToken.None);
        }

        // The real node/edge body templates use {StaticResource halfNegative} and element-name bindings
        // (#edgeLabel.Bounds). Building those off-screen must not throw and must produce content - this
        // exercises the actual InteractiveGraphView default templates through the raster path.
        [Fact]
        public async Task RenderPicture_with_the_default_control_templates_produces_content()
        {
            await m_Session.Dispatch(() =>
            {
                GraphAppearance appearance = GraphAppearance.Default;
                var source = new GraphNodeViewModel(
                    new GraphNodeLayoutModel { Id = 1, X = 0.0, Y = 0.0, Width = 40.0, Height = 30.0, Label = @"1", FillColorHexCode = @"#EAF1FB", BorderColorHexCode = @"#333333", BorderThickness = 1.0 },
                    appearance);
                var target = new GraphNodeViewModel(
                    new GraphNodeLayoutModel { Id = 2, X = 140.0, Y = 70.0, Width = 40.0, Height = 30.0, Label = @"2", FillColorHexCode = @"#EAF1FB", BorderColorHexCode = @"#333333", BorderThickness = 1.0 },
                    appearance);
                var nodes = new List<GraphNodeViewModel> { source, target };
                var edge = new GraphEdgeViewModel(
                    1, source, target, 1.5, false, @"#C0392B", @"A", showLabel: true, @"A",
                    GraphTheme.Light, GraphEdgeRoutingMode.StraightLine, appearance);
                var edges = new List<GraphEdgeViewModel> { edge };

                // The real control, so its default NodeTemplate/EdgeTemplate (with StaticResource +
                // element-name bindings) are exercised.
                var view = new InteractiveGraphView();
                view.NodeTemplate.ShouldNotBeNull();
                view.EdgeTemplate.ShouldNotBeNull();

                using SKPicture? picture = InteractiveGraphRasterRenderer.RenderPicture(
                    nodes, edges, view.NodeTemplate, view.EdgeTemplate, GraphTheme.Light, scale: 2.0);

                picture.ShouldNotBeNull();

                int width = (int)picture.CullRect.Width;
                int height = (int)picture.CullRect.Height;
                using var bitmap = new SKBitmap(width, height);
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(SKColors.White);
                    canvas.DrawPicture(picture);
                }

                // Content rendered if any clearly dark pixel exists (a node border #333 / label / the red edge).
                bool anyContent = false;
                for (int y = 0; y < height && !anyContent; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        SKColor p = bitmap.GetPixel(x, y);
                        if (p.Red < 120 && p.Green < 120 && p.Blue < 120)
                        {
                            anyContent = true;
                            break;
                        }
                    }
                }
                anyContent.ShouldBeTrue();
            }, CancellationToken.None);
        }
    }
}
