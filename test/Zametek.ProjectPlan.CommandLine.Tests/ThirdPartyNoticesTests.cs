using Shouldly;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// THIRD-PARTY-NOTICES.txt gives each package that a release ships, with its version, and a script makes it
    /// (pkg/notices/make-notices.py). Nothing made sure that the file had each package of a build: a bump of a package made the
    /// file wrong with no word (from the review of src, test and pkg). This test reads the build of zpp. No test project has
    /// the build of the desktop application, and so a package that only the desktop application ships has no test.
    /// </summary>
    public class ThirdPartyNoticesTests
    {
        // The kinds of files in a deps.json that go into a program, as the script reads them.
        private static readonly string[] s_AssetKinds = [@"runtime", @"native", @"runtimeTargets", @"resources"];

        private static readonly Regex s_Package = new(@"^(?<id>\S+) (?<version>[0-9]\S*)$");

        [Fact]
        public void Packages_Given_TheBuildOfZpp_Then_EachPackageWithFilesForTheRuntimeIsInTheFileWithItsVersion()
        {
            HashSet<string> listed = ListedPackages();
            using JsonDocument deps = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, @"zpp.deps.json")));
            JsonElement[] targets = [.. deps.RootElement.GetProperty(@"targets").EnumerateObject().Select(x => x.Value)];
            var shipped = new List<string>();

            foreach (JsonProperty library in deps.RootElement.GetProperty(@"libraries").EnumerateObject())
            {
                if (library.Value.GetProperty(@"type").GetString() == @"package"
                    && targets.Any(x => x.TryGetProperty(library.Name, out JsonElement assets) && s_AssetKinds.Any(kind => assets.TryGetProperty(kind, out _))))
                {
                    shipped.Add(library.Name.Replace('/', ' '));
                }
            }

            shipped.ShouldNotBeEmpty();
            shipped.Where(x => !listed.Contains(x)).ShouldBeEmpty(@"THIRD-PARTY-NOTICES.txt does not have these packages of zpp. Run pkg/notices/make-notices.py (docs/BUILDING.md).");
        }

        // The lines "<id> <version>" of the section PACKAGES.
        private static HashSet<string> ListedPackages()
        {
            string[] lines = File.ReadAllLines(Path.Combine(AppVersionTests.Root(), @"THIRD-PARTY-NOTICES.txt"));
            int start = Array.IndexOf(lines, @"PACKAGES") + 2;
            int end = Array.IndexOf(lines, @"FONTS", start) - 1;

            start.ShouldBeGreaterThan(1);
            end.ShouldBeGreaterThan(start);

            return [.. lines[start..end].Where(x => s_Package.IsMatch(x))];
        }
    }
}
