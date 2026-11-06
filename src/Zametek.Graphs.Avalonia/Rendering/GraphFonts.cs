using Avalonia.Media;
using Avalonia.Platform;
using SkiaSharp;
using Svg.Skia;
using Svg.Skia.TypefaceProviders;

namespace Zametek.Graphs.Avalonia
{
    // The font graph labels are drawn in: Cascadia Mono, shipped inside this assembly (Assets/Fonts, under
    // the SIL Open Font License in OFL.txt there) rather than looked up on the machine. The presets were
    // designed around Consolas, which only Windows has; anywhere else the name fell through to the system's
    // default font, which is not monospace and so broke the columns of the label tables. Every renderer is
    // pointed at the same two files: Avalonia draws the on-screen labels from LabelFontFamily, and the Skia
    // renderers - the canvas export and the rasterised fixed layout - take the typefaces below, so a graph
    // looks the same whichever machine draws it.
    internal static class GraphFonts
    {
        public const string LabelFamilyName = @"Cascadia Mono";

        // What the fixed-layout SVG names. Other programs open that file too and cannot see the bundled
        // font, so it comes first and the fallbacks keep the labels monospace where it is not installed.
        public const string SvgLabelFontFamilies = @"Cascadia Mono, Consolas, monospace";

        private const string c_FontsUri = @"avares://Zametek.Graphs.Avalonia/Assets/Fonts";

        private static readonly Lazy<SKTypeface> s_RegularLabelTypeface = new(() => LoadTypeface(@"CascadiaMono-Regular.ttf"));
        private static readonly Lazy<SKTypeface> s_BoldLabelTypeface = new(() => LoadTypeface(@"CascadiaMono-Bold.ttf"));

        public static FontFamily LabelFontFamily { get; } = new($@"{c_FontsUri}#{LabelFamilyName}");

        public static ITypefaceProvider TypefaceProvider { get; } = new BundledTypefaceProvider();

        // The bundled typeface for a family name, or null for any other family. With two faces, the weights
        // split the way CSS font matching splits them: 600 and above take the bold face.
        public static SKTypeface? TryGetLabelTypeface(string? familyName, int weight)
        {
            if (!string.Equals(familyName?.Trim().Trim('\'', '"'), LabelFamilyName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return weight >= 600 ? s_BoldLabelTypeface.Value : s_RegularLabelTypeface.Value;
        }

        // An Svg.Skia document that draws the bundled family from the bundled files. Svg.Skia asks its
        // typeface providers in turn and takes the first answer, so this one goes ahead of the system lookups
        // (which on Windows would find an installed Cascadia Mono of some other version).
        public static SKSvg CreateSvg()
        {
            var svg = new SKSvg();
            svg.Settings.TypefaceProviders ??= [];
            svg.Settings.TypefaceProviders.Insert(0, TypefaceProvider);
            return svg;
        }

        // Read through a loader of our own rather than Avalonia's static AssetLoader, which only works once an
        // Avalonia application has registered one - and the CLI never starts an application. Avalonia marks
        // StandardAssetLoader [Unstable] (outside its compatibility promise), so an Avalonia upgrade may need
        // this adjusting; GraphFontsTests would show it.
        private static SKTypeface LoadTypeface(string fileName)
        {
            var loader = new StandardAssetLoader(typeof(GraphFonts).Assembly);
            using Stream stream = loader.Open(new Uri($@"{c_FontsUri}/{fileName}"));
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            using SKData data = SKData.CreateCopy(buffer.ToArray());
            return SKTypeface.FromData(data)
                ?? throw new InvalidOperationException($@"The bundled font {fileName} could not be loaded.");
        }

        // Answers for the bundled family and leaves every other name to the providers after it. The name may
        // arrive as the whole font-family list, so each family in it is checked.
        private sealed class BundledTypefaceProvider
            : ITypefaceProvider
        {
            public SKTypeface? FromFamilyName(
                string fontFamily,
                SKFontStyleWeight fontWeight,
                SKFontStyleWidth fontWidth,
                SKFontStyleSlant fontStyle)
            {
                if (string.IsNullOrEmpty(fontFamily))
                {
                    return null;
                }

                foreach (string familyName in fontFamily.Split(','))
                {
                    SKTypeface? typeface = TryGetLabelTypeface(familyName, (int)fontWeight);
                    if (typeface is not null)
                    {
                        return typeface;
                    }
                }

                return null;
            }
        }
    }
}
