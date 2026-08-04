using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Shouldly;
using SkiaSharp;
using Xunit;

namespace Zametek.Graphs.Avalonia.Tests.Rendering
{
    // Guards the vector export's node fill - in particular the ellipse silhouette (which regressed to an
    // unfilled outline) and the NodeFillOverride used when a template's fill is not data-driven. The renderer
    // reads brush colours (AvaloniaObject properties), so it must run on the shared headless UI thread - as
    // it does in the app (the copy/save paths render on the UI thread).
    [Collection("Headless rendering")]
    public class InteractiveGraphRendererTests
    {
        private readonly HeadlessUnitTestSession m_Session;

        public InteractiveGraphRendererTests(HeadlessSessionFixture fixture)
        {
            m_Session = fixture.Session;
        }

        [Fact]
        public async Task Vector_ellipse_node_is_filled_with_its_data_colour()
        {
            await m_Session.Dispatch(() =>
            {
                SKColor centre = RenderNodeCentre(
                    fillHexCode: @"#3F86C8",
                    GraphVectorExportStyle.Default with { NodeShape = GraphExportNodeShape.Ellipse });

                // The data fill (#3F86C8) - a mid blue, clearly not the white background.
                centre.Red.ShouldBeInRange((byte)0x2F, (byte)0x4F);
                centre.Green.ShouldBeInRange((byte)0x76, (byte)0x96);
                centre.Blue.ShouldBeInRange((byte)0xB8, (byte)0xD8);
            }, CancellationToken.None);
        }

        [Fact]
        public async Task Vector_node_fill_override_replaces_the_data_fill()
        {
            await m_Session.Dispatch(() =>
            {
                SKColor centre = RenderNodeCentre(
                    fillHexCode: @"#EAF1FB", // light data fill (as the bespoke arrow nodes carry)
                    GraphVectorExportStyle.Default with
                    {
                        NodeShape = GraphExportNodeShape.Ellipse,
                        NodeFillOverride = new ImmutableSolidColorBrush(Color.Parse(@"#2E4194")),
                    });

                // The override (indigo #2E4194) wins over the light data fill.
                centre.Red.ShouldBeLessThan((byte)0x60);
                centre.Blue.ShouldBeGreaterThan((byte)0x80);
            }, CancellationToken.None);
        }

        [Fact]
        public async Task Vector_gradient_fill_override_renders_as_a_gradient()
        {
            await m_Session.Dispatch(() =>
            {
                // The bespoke arrow's on-screen indigo radial gradient, supplied to the vector export.
                var style = GraphVectorExportStyle.Default with
                {
                    NodeShape = GraphExportNodeShape.Ellipse,
                    NodeFillOverride = new RadialGradientBrush
                    {
                        GradientStops =
                        {
                            new GradientStop(Color.Parse(@"#6D8BFF"), 0.0),
                            new GradientStop(Color.Parse(@"#2A3F9D"), 1.0),
                        },
                    },
                };

                using SKBitmap bitmap = RenderNodeBitmap(fillHexCode: @"#EAF1FB", style);

                // The gradient origin is the node centre (46, 36) - the bright inner stop (#6D8BFF).
                SKColor centre = bitmap.GetPixel(46, 36);
                // A point nearer the rim (still well inside the ellipse) - the darker indigo.
                SKColor rim = bitmap.GetPixel(68, 36);

                // Inner stop: a bright periwinkle (near-max blue), clearly not the light data fill.
                centre.Blue.ShouldBeGreaterThan((byte)0xD0);
                // It is a genuine gradient, not a flat fill: the rim is measurably darker/less blue.
                rim.Blue.ShouldBeLessThan((byte)(centre.Blue - 20));
                rim.Red.ShouldBeLessThan(centre.Red);
            }, CancellationToken.None);
        }

        // Render a single label-less node and return the colour at its centre.
        // Node centre: padding(16) + Width/2, padding + Height/2 = (46, 36).
        private static SKColor RenderNodeCentre(string fillHexCode, GraphVectorExportStyle style)
        {
            using SKBitmap bitmap = RenderNodeBitmap(fillHexCode, style);
            return bitmap.GetPixel(46, 36);
        }

        // Render a single 60x40 label-less node at the origin and return the raster (caller disposes). The
        // node spans (16,16)-(76,56) in the image; its centre is (46, 36).
        private static SKBitmap RenderNodeBitmap(string fillHexCode, GraphVectorExportStyle style)
        {
            var node = new GraphNodeViewModel(
                new GraphNodeLayoutModel
                {
                    Id = 1,
                    X = 0.0,
                    Y = 0.0,
                    Width = 60.0,
                    Height = 40.0,
                    Label = string.Empty,
                    FillColorHexCode = fillHexCode,
                    BorderColorHexCode = @"#333333",
                    BorderThickness = 1.0,
                },
                GraphAppearance.Default);
            var nodes = new List<GraphNodeViewModel> { node };
            var edges = new List<GraphEdgeViewModel>();

            using SKPicture? picture = InteractiveGraphRenderer.Render(nodes, edges, style, GraphAppearance.Default, GraphTheme.Light);
            picture.ShouldNotBeNull();

            int width = (int)picture.CullRect.Width;
            int height = (int)picture.CullRect.Height;
            var bitmap = new SKBitmap(width, height);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White);
                canvas.DrawPicture(picture);
            }
            return bitmap;
        }
    }
}
