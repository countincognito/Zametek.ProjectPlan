using Shouldly;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// The version of the application is in one place, Directory.Build.props. Before, a release changed it by hand in about
    /// seven places, and nothing made sure that they agreed (from the review of src, test and pkg). Some files must still have
    /// it as text: the two MSIX manifests, and the examples of the documents. These tests fail when one of them differs.
    /// </summary>
    public class AppVersionTests
    {
        private static readonly Regex s_Version = new(@"^(?<numeric>[0-9]+\.[0-9]+\.[0-9]+)(-(?<label>[0-9A-Za-z.-]+))?$");

        [Fact]
        public void Text_Given_TheBuild_Then_TheVersionOfDirectoryBuildPropsWithNoCommit()
        {
            XDocument props = XDocument.Load(Path.Combine(Root(), @"Directory.Build.props"));
            string prefix = props.Descendants(@"VersionPrefix").Single().Value;
            string suffix = props.Descendants(@"VersionSuffix").Single().Value;

            AppVersion.Text.ShouldBe(suffix.Length == 0 ? prefix : $@"{prefix}-{suffix}");
            AppVersion.Numeric.ShouldBe(prefix);
            s_Version.IsMatch(AppVersion.Text).ShouldBeTrue(AppVersion.Text);

            // Each program has the same version, in its file properties too.
            AppVersion.Read(typeof(Program).Assembly).ShouldBe(AppVersion.Text);
            typeof(Program).Assembly.GetName().Version.ShouldBe(new Version($@"{prefix}.0"));
        }

        [Theory]
        [InlineData(@"pkg/Zametek.ProjectPlan.Desktop.WapPackager/Package.appxmanifest")]
        [InlineData(@"pkg/Zametek.ProjectPlan.CommandLine.WapPackager/Package.appxmanifest")]
        public void Manifest_Given_AnMsixPackage_Then_TheNumericVersionAndZero(string manifest)
        {
            XDocument document = XDocument.Load(Path.Combine(Root(), manifest));
            XElement identity = document.Descendants().Single(x => x.Name.LocalName == @"Identity");

            identity.Attribute(@"Version").ShouldNotBeNull().Value.ShouldBe($@"{AppVersion.Numeric}.0");
        }

        [Fact]
        public void Msi_Given_TheInstaller_Then_TheVersionComesFromTheBuild()
        {
            string package = File.ReadAllText(Path.Combine(Root(), @"pkg", @"Zametek.ProjectPlan.MsiPackager", @"Package.wxs"));
            string project = File.ReadAllText(Path.Combine(Root(), @"pkg", @"Zametek.ProjectPlan.MsiPackager", @"Zametek.ProjectPlan.MsiPackager.wixproj"));

            package.ShouldNotContain(@"<?define Version");
            package.ShouldContain(@"Version=""$(var.Version)""");
            project.ShouldContain(@"Version=$(VersionPrefix).0");
            project.ShouldContain(@"<OutputName>projectplandotnet.$(Version).installer.$(Platform)</OutputName>");
        }

        [Theory]
        [InlineData(@"docs/API.md", @"""version"": ""{0}""")]
        [InlineData(@"docs/openapi.yaml", @"version: {0}")]
        [InlineData(@"docs/CLIENT-SERVER-QUICK-START.md", @"{{""version"":""{0}""")]
        public void Example_Given_ADocument_Then_ItHasTheVersion(string document, string format)
        {
            string text = File.ReadAllText(Path.Combine(Root(), document));

            text.ShouldContain(string.Format(System.Globalization.CultureInfo.InvariantCulture, format, AppVersion.Text));
        }

        // The notices of the other parties give the version of the release that they are for. The script that makes them reads
        // it from Directory.Build.props (pkg/notices/make-notices.py), and so a new version needs the file made again.
        [Fact]
        public void Notices_Given_TheNoticesOfTheOtherParties_Then_TheyAreForThisVersion()
        {
            string[] lines = File.ReadAllLines(Path.Combine(Root(), @"THIRD-PARTY-NOTICES.txt"));

            lines.ShouldContain($@"projectplan.net and zpp, version {AppVersion.Text}", @"Run pkg/notices/make-notices.py (docs/BUILDING.md).");
        }

        // The folder of the repository: the first folder above the tests that has the solution filter.
        internal static string Root()
        {
            for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
            {
                if (File.Exists(Path.Combine(folder.FullName, @"Zametek.ProjectPlan.slnf")))
                {
                    return folder.FullName;
                }
            }

            throw new InvalidOperationException(@"The folder of the repository was not found above " + AppContext.BaseDirectory);
        }
    }
}
