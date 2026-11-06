using Avalonia.Platform;
using ScottPlot;
using SkiaSharp;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The charts' font: Inter, read from the same files that Avalonia.Fonts.Inter gives the user
    /// interface and handed to ScottPlot directly. Left to itself ScottPlot takes a system font -
    /// Segoe UI on Windows, DejaVu Sans on Linux, none at all in a browser - so the same chart came
    /// out differently from one machine to the next.
    /// </summary>
    public static class ChartFonts
    {
        public const string FamilyName = @"Inter";

        private const string c_FontsUri = @"avares://Avalonia.Fonts.Inter/Assets";

        private static readonly Lazy<bool> s_Registration = new(RegisterOnce);

        /// <summary>
        /// Makes Inter the font of every chart built from now on. Process-wide, and it must run before
        /// the first <see cref="Plot"/> is constructed, because ScottPlot copies its default font into
        /// each label as the label is created - so every head calls it at start-up, before any chart
        /// view-model exists. Later calls do nothing, and a call racing the first waits for it to finish.
        /// </summary>
        public static void Register() => _ = s_Registration.Value;

        private static bool RegisterOnce()
        {
            // First in the list, so that no system font that happens to be called Inter answers instead.
            Fonts.FontResolvers.Insert(0, new BundledInterResolver());
            Fonts.Default = FamilyName;
            return true;
        }

        // Read through a loader of our own rather than Avalonia's static AssetLoader, which only works once
        // an Avalonia application has registered one - and the CLI never starts an application. Avalonia
        // marks StandardAssetLoader [Unstable] (outside its compatibility promise), so an Avalonia upgrade
        // may need this adjusting; ChartFontsTests would show it.
        private static SKTypeface LoadTypeface(string fileName)
        {
            var loader = new StandardAssetLoader(typeof(ChartFonts).Assembly);
            using Stream stream = loader.Open(new Uri($@"{c_FontsUri}/{fileName}"));
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            using SKData data = SKData.CreateCopy(buffer.ToArray());
            return SKTypeface.FromData(data)
                ?? throw new InvalidOperationException($@"The bundled font {fileName} could not be loaded.");
        }

        // ScottPlot caches each typeface a resolver returns and never disposes it, so the two faces are
        // loaded once and shared by every chart. With two faces, the weights split the way CSS font matching
        // splits them: semi-bold and heavier take the bold face.
        private sealed class BundledInterResolver
            : IFontResolver
        {
            private static readonly Lazy<SKTypeface> s_Regular = new(() => LoadTypeface(@"Inter-Regular.ttf"));
            private static readonly Lazy<SKTypeface> s_Bold = new(() => LoadTypeface(@"Inter-Bold.ttf"));

            public SKTypeface? CreateTypeface(string fontName, bool bold, bool italic) =>
                CreateTypeface(fontName, bold ? FontWeight.Bold : FontWeight.Normal, FontSlant.Upright, FontSpacing.Normal);

            public SKTypeface? CreateTypeface(string fontName, FontWeight weight, FontSlant slant, FontSpacing spacing)
            {
                if (!string.Equals(fontName, FamilyName, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return weight >= FontWeight.SemiBold ? s_Bold.Value : s_Regular.Value;
            }
        }
    }
}
