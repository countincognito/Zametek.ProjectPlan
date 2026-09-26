using Shouldly;
using SkiaSharp;
using Svg.Skia;
using Svg.Skia.TypefaceProviders;
using Xunit;

namespace Zametek.Graphs.Avalonia.Tests.Rendering
{
    // Guards the bundled label font: that it loads from the library's own resources (as the CLI must, with no
    // Avalonia application to ask), that the fixed-layout rasteriser takes it ahead of any system font of the
    // same name, and that the presets size the labels for its metrics. The canvas export and the on-screen
    // labels are covered in InteractiveGraphRendererTests, which have an Avalonia session to hand.
    public class GraphFontsTests
    {
        // Consolas's advance width, 1126 font units at 2048 to the em: the font the presets were tuned for.
        private const double c_ConsolasAdvance = 1126.0 / 2048.0;

        private static SKTypeface Regular => GraphFonts.TryGetLabelTypeface(GraphFonts.LabelFamilyName, 400)!;

        private static SKTypeface Bold => GraphFonts.TryGetLabelTypeface(GraphFonts.LabelFamilyName, 700)!;

        [Fact]
        public void Bundled_label_typefaces_load()
        {
            Regular.FamilyName.ShouldBe(@"Cascadia Mono");
            Regular.FontWeight.ShouldBe((int)SKFontStyleWeight.Normal);
            Regular.IsFixedPitch.ShouldBeTrue();

            Bold.FamilyName.ShouldBe(@"Cascadia Mono");
            Bold.FontWeight.ShouldBe((int)SKFontStyleWeight.Bold);
        }

        [Theory]
        [InlineData(@"Cascadia Mono", 400, false)]
        [InlineData(@"cascadia mono", 500, false)]
        [InlineData(@" 'Cascadia Mono' ", 600, true)]
        [InlineData(@"""Cascadia Mono""", 900, true)]
        public void Weights_split_between_the_two_faces_as_CSS_font_matching_does(string familyName, int weight, bool bold)
        {
            GraphFonts.TryGetLabelTypeface(familyName, weight).ShouldBeSameAs(bold ? Bold : Regular);
        }

        [Theory]
        [InlineData(@"Consolas")]
        [InlineData(@"Cascadia Code")]
        [InlineData(@"monospace")]
        [InlineData(@"")]
        [InlineData(null)]
        public void Other_families_are_left_to_the_system(string? familyName)
        {
            GraphFonts.TryGetLabelTypeface(familyName, 400).ShouldBeNull();
        }

        [Fact]
        public void Svg_typeface_provider_answers_for_the_bundled_family_anywhere_in_a_list()
        {
            ITypefaceProvider provider = GraphFonts.TypefaceProvider;

            provider.FromFamilyName(GraphFonts.SvgLabelFontFamilies, SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
                .ShouldBeSameAs(Regular);
            provider.FromFamilyName(@"Consolas, 'Cascadia Mono'", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
                .ShouldBeSameAs(Bold);
            provider.FromFamilyName(@"Consolas, monospace", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
                .ShouldBeNull();
        }

        [Fact]
        public void Fixed_layout_rasteriser_asks_for_the_bundled_font_before_any_system_font()
        {
            using SKSvg svg = GraphFonts.CreateSvg();
            IList<ITypefaceProvider> providers = svg.Settings.TypefaceProviders.ShouldNotBeNull();

            // Svg.Skia takes the first answer, so the bundled file wins even where Cascadia Mono is installed...
            providers
                .Select(x => x.FromFamilyName(GraphFonts.SvgLabelFontFamilies, SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright))
                .First(x => x is not null)
                .ShouldBeSameAs(Regular);

            // ...and any other family still reaches the system lookups behind it.
            providers.Count.ShouldBeGreaterThan(1);
        }

        [Fact]
        public void Fixed_layout_labels_are_drawn_from_the_bundled_file()
        {
            // The engine's own SVG, rasterised by the document the fixed-layout export uses and again by one
            // that can see nothing but the bundled font. The same pixels mean the export's labels came from the
            // bundled file: a system lookup would draw them in whatever font the machine has, or in its own
            // copy of Cascadia Mono.
            byte[] svgData = new MsaglGraphLayoutEngine().RenderSvg(BuildDiagram(), GraphConfigurations.Arrow, GraphTheme.Light);

            using SKSvg exported = GraphFonts.CreateSvg();
            using var bundledOnly = new SKSvg();
            bundledOnly.Settings.TypefaceProviders = [GraphFonts.TypefaceProvider];

            Rasterise(exported, svgData).ShouldBe(Rasterise(bundledOnly, svgData));
        }

        // The presets' factors were worked out by eye for Consolas. Each node label line must come out exactly
        // as wide in the bundled font as it did in Consolas, so that the node boxes look as they always have.
        [Theory]
        [InlineData(nameof(GraphConfigurations.Arrow), 1, 1.0 * 34.0 / 14.0)]
        [InlineData(nameof(GraphConfigurations.Vertex), 3, 3.0 * 30.0 / 11.5)]
        public void Node_label_lines_are_as_wide_as_they_were_in_Consolas(string configurationName, int lineCount, double consolasFactor)
        {
            GraphConfiguration configuration = ConfigurationNamed(configurationName);
            const string line = @"12345";
            string label = string.Join('\n', Enumerable.Repeat(line, lineCount));

            double width = MeasureInBundledFont(line, MsaglGraphLayoutEngine.NodeLabelFontSize(label, configuration));

            double consolasFontSize = MsaglGraphLayoutEngine.NodeLabelFontSize(
                label,
                configuration with { LabelWidthCorrectionFactor = consolasFactor });
            double consolasWidth = line.Length * c_ConsolasAdvance * consolasFontSize;

            width.ShouldBe(consolasWidth, tolerance: consolasWidth * 0.001);
        }

        // The arrow graph's edge labels are laid out in boxes sized from their length, so each box must hold its
        // text as drawn in the bundled font - or the text spills over the edges and nodes the layout kept clear.
        [Theory]
        [InlineData(@"A")]
        [InlineData(@"E12")]
        [InlineData(@"Activity 17")]
        public void Arrow_edge_label_boxes_fit_their_text_in_the_bundled_font(string label)
        {
            GraphConfiguration arrow = GraphConfigurations.Arrow;

            double box = MsaglGraphLayoutEngine.EdgeLabelWidth(label, arrow);
            double width = MeasureInBundledFont(label, arrow.EdgeLabelFontSize);

            width.ShouldBeInRange(box * 0.99, box * 1.005);
        }

        private static GraphConfiguration ConfigurationNamed(string name) => name switch
        {
            nameof(GraphConfigurations.Arrow) => GraphConfigurations.Arrow,
            nameof(GraphConfigurations.Vertex) => GraphConfigurations.Vertex,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };

        // Unhinted advances, measured at the font's em size and scaled to the size asked for, so that the measure is
        // the font's own and the same on every platform. Measuring at the size itself is not: FreeType, which Skia
        // uses on Linux, first truncates the size to a 1/64 of a pixel, while DirectWrite on Windows keeps it exact.
        private static double MeasureInBundledFont(string text, double size)
        {
            using var font = new SKFont(Regular, Regular.UnitsPerEm)
            {
                Hinting = SKFontHinting.None,
                Subpixel = true,
                LinearMetrics = true,
            };
            return font.MeasureText(text) * size / Regular.UnitsPerEm;
        }

        private static byte[] Rasterise(SKSvg svg, byte[] svgData)
        {
            using (var stream = new MemoryStream(svgData))
            {
                svg.Load(stream);
            }

            SKPicture picture = svg.Picture.ShouldNotBeNull();
            var info = new SKImageInfo(
                (int)Math.Ceiling(picture.CullRect.Width) * 2,
                (int)Math.Ceiling(picture.CullRect.Height) * 2,
                SKColorType.Rgba8888,
                SKAlphaType.Premul);

            using var bitmap = new SKBitmap(info);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White);
                canvas.Scale(2.0f);
                canvas.DrawPicture(picture);
            }
            return bitmap.Bytes;
        }

        // A few nodes and labelled edges - enough text to tell one font from another.
        private static DiagramGraphModel BuildDiagram()
        {
            List<DiagramNodeModel> nodes = [.. Enumerable.Range(0, 4).Select(i => new DiagramNodeModel
            {
                Id = i,
                Text = $@"{i * 11}",
                FillColorHexCode = @"#D3D3D3",
                BorderColorHexCode = @"#000000",
                BorderThickness = 1.0,
            })];

            List<DiagramEdgeModel> edges = [.. new[] { (0, 1), (0, 2), (1, 3), (2, 3) }.Select((link, index) => new DiagramEdgeModel
            {
                Id = index,
                SourceId = link.Item1,
                TargetId = link.Item2,
                ForegroundColorHexCode = @"#000000",
                StrokeThickness = 1.0,
                Label = $@"Activity {index}",
                ShowLabel = true,
            })];

            return new DiagramGraphModel { Nodes = nodes, Edges = edges };
        }
    }
}
