using Avalonia.Platform;
using ScottPlot;
using Shouldly;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    // Guards the charts' font. ScottPlot's font settings are process-wide, so these tests leave Inter
    // registered for the rest of the run - which is what every head does at start-up anyway.
    public class ChartFontsTests
    {
        [Fact]
        public void Plots_built_after_registering_label_everything_in_Inter()
        {
            ChartFonts.Register();

            Fonts.Default.ShouldBe(ChartFonts.FamilyName);

            using var plot = new Plot();
            plot.Add.Scatter(new[] { 1.0, 2.0 }, new[] { 3.0, 4.0 }).LegendText = @"Series";

            plot.Axes.Title.Label.FontName.ShouldBe(ChartFonts.FamilyName);
            plot.Axes.Bottom.Label.FontName.ShouldBe(ChartFonts.FamilyName);
            plot.Axes.Bottom.TickLabelStyle.FontName.ShouldBe(ChartFonts.FamilyName);
            plot.Legend.GetItems().ShouldAllBe(x => x.LabelFontName == ChartFonts.FamilyName);
        }

        [Theory]
        [InlineData(FontWeight.Normal, @"Inter-Regular.ttf")]
        [InlineData(FontWeight.Medium, @"Inter-Regular.ttf")]
        [InlineData(FontWeight.SemiBold, @"Inter-Bold.ttf")]
        [InlineData(FontWeight.Bold, @"Inter-Bold.ttf")]
        public void Inter_comes_from_the_files_the_user_interface_ships(FontWeight weight, string fileName)
        {
            ChartFonts.Register();

            SKTypeface typeface = Fonts.GetTypeface(ChartFonts.FamilyName, weight, FontSlant.Upright, FontSpacing.Normal);

            // The very file Avalonia.Fonts.Inter carries, not a system font that happens to be called Inter.
            typeface.FamilyName.ShouldBe(ChartFonts.FamilyName);
            FontData(typeface).ShouldBe(InterFile(fileName));
        }

        [Fact]
        public void Registering_again_changes_nothing()
        {
            ChartFonts.Register();
            List<IFontResolver> resolvers = [.. Fonts.FontResolvers];

            ChartFonts.Register();

            Fonts.FontResolvers.ShouldBe(resolvers);
            Fonts.Default.ShouldBe(ChartFonts.FamilyName);
        }

        private static byte[] FontData(SKTypeface typeface)
        {
            using SKStreamAsset stream = typeface.OpenStream().ShouldNotBeNull();
            var data = new byte[stream.Length];
            stream.Read(data, data.Length).ShouldBe(data.Length);
            return data;
        }

        private static byte[] InterFile(string fileName)
        {
            var loader = new StandardAssetLoader(typeof(ChartFontsTests).Assembly);
            using Stream stream = loader.Open(new Uri($@"avares://Avalonia.Fonts.Inter/Assets/{fileName}"));
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
    }
}
