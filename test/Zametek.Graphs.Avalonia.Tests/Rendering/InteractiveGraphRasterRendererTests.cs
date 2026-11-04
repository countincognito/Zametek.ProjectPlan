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
        // The limit of a raster image: 67,108,864 pixels (8,192 x 8,192), and no side longer than 65,500 pixels.
        private const long c_MaxPixelCount = 8_192L * 8_192L;
        private const int c_MaxSideLength = 65_500;

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

        // A graph too large for one bitmap is drawn at a smaller scale, so that the bitmap fits the limit of a raster image.
        // The graph keeps its shape, and each node shows in its place.
        [Fact]
        public async Task RenderPicture_draws_a_graph_over_the_pixel_limit_at_a_scale_that_fits()
        {
            // 5,032 x 5,022 units: 10,064 x 10,044 pixels at the scale of 2.
            await RenderTwoNodesAsync(farX: 4_960.0, farY: 4_960.0);
        }

        // As wide as the arrow graph of the sample plan docs/assets/erp-migration.zpp. At the scale of 2, its bitmap needs more
        // than 61 GB. Avalonia counts the bytes of a bitmap in 32 bits, and so the count wrapped: the bitmap got a buffer that
        // was too small, and the process crashed when Avalonia cleared it.
        [Fact]
        public async Task RenderPicture_draws_a_graph_far_too_large_for_one_bitmap_at_a_scale_that_fits()
        {
            await RenderTwoNodesAsync(farX: 148_000.0, farY: 26_000.0);
        }

        // Two red nodes of 40 x 30 units: one at the origin, and one at (farX, farY).
        private async Task RenderTwoNodesAsync(double farX, double farY)
        {
            await m_Session.Dispatch(() =>
            {
                List<GraphNodeViewModel> nodes = [BuildNode(1, 0.0, 0.0), BuildNode(2, farX, farY)];
                var nodeTemplate = new FuncDataTemplate<GraphNodeViewModel>((_, _) => new Border { Background = Brushes.Red });

                using SKPicture? picture = InteractiveGraphRasterRenderer.RenderPicture(
                    nodes, [], nodeTemplate, edgeTemplate: null, GraphTheme.Light, scale: 2.0);

                picture.ShouldNotBeNull();
                double width = picture.CullRect.Width;
                double height = picture.CullRect.Height;
                (width * height).ShouldBeLessThanOrEqualTo(c_MaxPixelCount);
                Math.Max(width, height).ShouldBeLessThanOrEqualTo(c_MaxSideLength);
                (width * height > 0.99 * c_MaxPixelCount || Math.Max(width, height) > 0.99 * c_MaxSideLength)
                    .ShouldBeTrue($@"a bitmap of {width} x {height} pixels uses most of the limit");

                // The crop is the box of the nodes, with 16 units on each side. The scale is the same on both axes.
                double cropWidth = farX + 40.0 + 32.0;
                double cropHeight = farY + 30.0 + 32.0;
                double scale = width / cropWidth;
                (height / cropHeight).ShouldBe(scale, scale * 0.01);

                ShouldBeRed(PixelAt(picture, (16.0 + 20.0) * scale, (16.0 + 15.0) * scale), @"the node at the origin");
                ShouldBeRed(PixelAt(picture, (16.0 + farX + 20.0) * scale, (16.0 + farY + 15.0) * scale), @"the far node");
            }, CancellationToken.None);
        }

        private static GraphNodeViewModel BuildNode(int id, double x, double y) =>
            new(
                new GraphNodeLayoutModel { Id = id, X = x, Y = y, Width = 40.0, Height = 30.0, Label = $@"{id}", FillColorHexCode = @"#EAF1FB", BorderColorHexCode = @"#333333", BorderThickness = 1.0 },
                GraphAppearance.Default);

        // The colour of one pixel of the picture, drawn on white.
        private static SKColor PixelAt(SKPicture picture, double x, double y)
        {
            using var bitmap = new SKBitmap(1, 1);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White);
                canvas.Translate(-(float)Math.Floor(x), -(float)Math.Floor(y));
                canvas.DrawPicture(picture);
            }

            return bitmap.GetPixel(0, 0);
        }

        private static void ShouldBeRed(SKColor colour, string what)
        {
            colour.Red.ShouldBeGreaterThan((byte)150, what);
            colour.Green.ShouldBeLessThan((byte)120, what);
            colour.Blue.ShouldBeLessThan((byte)120, what);
        }
    }
}
