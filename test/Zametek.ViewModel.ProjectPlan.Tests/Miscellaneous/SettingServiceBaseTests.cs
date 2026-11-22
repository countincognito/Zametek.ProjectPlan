using Shouldly;
using System;
using System.IO;
using Xunit;
using Zametek.Data.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A settings file that the service cannot read does not stop the start. The service starts with the default settings,
    /// it moves the file out of the way, and it says what happened, so that the application can tell the user one time.
    /// Before, the exception left the constructor, and the application ended at the start with no message.
    /// </summary>
    public class SettingServiceBaseTests
        : IDisposable
    {
        private readonly string m_Directory;
        private readonly string m_Filename;

        public SettingServiceBaseTests()
        {
            m_Directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $@"zpp-settings-tests-{Guid.NewGuid():N}")).FullName;
            m_Filename = Path.Combine(m_Directory, @"UserSettings.json");
        }

        public void Dispose()
        {
            Directory.Delete(m_Directory, recursive: true);
        }

        // Text that is not JSON, a write that did not finish, JSON that is not an object, and a value of the wrong kind.
        [Theory]
        [InlineData(@"These are not settings.")]
        [InlineData(@"{ ""Version"": ""v0.6.1"", ""SelectedTheme"": ")]
        [InlineData(@"[1, 2, 3]")]
        [InlineData(@"{ ""Version"": ""v0.6.1"", ""MaxRecentProjectFilePaths"": ""many"" }")]
        public void Constructor_Given_ASettingsFileThatItCannotRead_Then_ItStartsWithTheDefaultsAndMovesTheFile(string content)
        {
            File.WriteAllText(m_Filename, content);
            CoreViewModelFixture.TestSettingService? service = null;

            Should.NotThrow(() => service = new CoreViewModelFixture.TestSettingService(m_Filename));

            service.ShouldNotBeNull();
            service.SettingsReadFailure.ShouldNotBeNull();
            service.UnreadableSettingsFilename.ShouldBe($@"{m_Filename}.bad");
            File.Exists(m_Filename).ShouldBeFalse(@"the file that cannot be read is still in its place");
            File.ReadAllText($@"{m_Filename}.bad").ShouldBe(content);
        }

        [Fact]
        public void Constructor_Given_AnEarlierFileThatWasMoved_Then_TheNewOneTakesItsPlace()
        {
            File.WriteAllText($@"{m_Filename}.bad", @"The file from an earlier start.");
            File.WriteAllText(m_Filename, @"These are not settings.");

            var service = new CoreViewModelFixture.TestSettingService(m_Filename);

            service.UnreadableSettingsFilename.ShouldBe($@"{m_Filename}.bad");
            File.ReadAllText($@"{m_Filename}.bad").ShouldBe(@"These are not settings.");
        }

        [Fact]
        public void Constructor_Given_ASettingsFileThatItCanRead_Then_ItReportsNoFailure()
        {
            string content = $@"{{ ""Version"": ""{Versions.AppSettingsLatest}"", ""SelectedTheme"": ""Dark"" }}";
            File.WriteAllText(m_Filename, content);

            var service = new CoreViewModelFixture.TestSettingService(m_Filename);

            service.SettingsReadFailure.ShouldBeNull();
            service.UnreadableSettingsFilename.ShouldBeNull();
            File.ReadAllText(m_Filename).ShouldBe(content);
        }

        [Theory]
        [InlineData(@"")]
        [InlineData(@"   ")]
        public void Constructor_Given_AnEmptySettingsFile_Then_ItStartsWithTheDefaultsAndKeepsTheFile(string content)
        {
            // An empty file is not a failure: the service read it as no settings before, and it still does.
            File.WriteAllText(m_Filename, content);

            var service = new CoreViewModelFixture.TestSettingService(m_Filename);

            service.SettingsReadFailure.ShouldBeNull();
            File.Exists(m_Filename).ShouldBeTrue();
        }

        [Fact]
        public void Constructor_Given_NoSettingsFile_Then_ItReportsNoFailure()
        {
            var service = new CoreViewModelFixture.TestSettingService(m_Filename);

            service.SettingsReadFailure.ShouldBeNull();
            service.UnreadableSettingsFilename.ShouldBeNull();
        }
    }
}
