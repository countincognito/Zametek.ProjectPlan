using Shouldly;
using System.Text.Json;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// zpp draws its charts and graphs with Avalonia.Skia and SkiaSharp. The desktop application gets these packages from
    /// Avalonia.Desktop, with the version of Avalonia. zpp has no Avalonia.Desktop: it got Avalonia.Skia 12.0.0 from
    /// Svg.Controls.Skia.Avalonia, because NuGet takes the lowest version that a package allows, and with it a preview of
    /// SkiaSharp. It also had the native Linux packages of two versions (found by the script of the notices). These tests
    /// read the build of zpp.
    /// </summary>
    public class DrawingPackagesTests
    {
        [Fact]
        public void Packages_Given_TheBuildOfZpp_Then_AvaloniaSkiaHasTheVersionOfAvalonia()
        {
            ILookup<string, string> versions = PackageVersions();

            versions[@"Avalonia"].ShouldHaveSingleItem();
            versions[@"Avalonia.Skia"].ShouldBe(versions[@"Avalonia"]);
        }

        [Fact]
        public void Packages_Given_TheBuildOfZpp_Then_SkiaSharpAndItsNativePackagesHaveOneVersionWithNoPreReleaseLabel()
        {
            ILookup<string, string> versions = PackageVersions();
            string[] skiaSharp = [.. versions
                .Where(x => x.Key == @"SkiaSharp" || x.Key.StartsWith(@"SkiaSharp.NativeAssets.", StringComparison.Ordinal))
                .SelectMany(x => x.Select(version => $@"{x.Key} {version}"))
                .Order(StringComparer.Ordinal)];

            skiaSharp.ShouldContain(x => x.StartsWith(@"SkiaSharp ", StringComparison.Ordinal));
            skiaSharp.Select(x => x.Split(' ')[1]).Distinct().ShouldHaveSingleItem(string.Join(@", ", skiaSharp));
            skiaSharp.ShouldAllBe(x => !x.Contains('-'), string.Join(@", ", skiaSharp));
        }

        // The id and the version of each package in zpp.deps.json.
        private static ILookup<string, string> PackageVersions()
        {
            using JsonDocument deps = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, @"zpp.deps.json")));

            return deps.RootElement.GetProperty(@"libraries").EnumerateObject()
                .Where(x => x.Value.GetProperty(@"type").GetString() == @"package")
                .Select(x => x.Name.Split('/'))
                .ToLookup(x => x[0], x => x[1], StringComparer.Ordinal);
        }
    }
}
