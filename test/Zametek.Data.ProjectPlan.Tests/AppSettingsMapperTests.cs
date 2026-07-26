using Shouldly;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for the version mapping of the app settings around the v0.6.1
    /// introduction of the recently opened project file list. v0.6.0 stays
    /// frozen without the recents members, so upgrading defaults them (the
    /// default cap and an empty list) and downgrading drops them.
    /// </summary>
    public class AppSettingsMapperTests
    {
        [Fact]
        public void VersionMapper_Given_v0_6_0_AppSettings_When_UpgradedTo_v0_6_1_Then_RecentsDefault()
        {
            var mapper = new VersionMapper();

            var settings_v0_6_0 = new v0_6_0.AppSettingsModel
            {
                ProjectDirectory = @"C:\projects",
                DefaultShowDates = true,
                DefaultUseClassicDates = true,
                DefaultNonWorkingDayMode = NonWorkingDayMode.Weekends,
                DefaultHideCost = true,
                DefaultHideBilling = true,
                SelectedTheme = @"Dark",
            };

            v0_6_1.AppSettingsModel settings_v0_6_1 = v0_6_1.Converter.Upgrade(mapper, settings_v0_6_0);

            settings_v0_6_1.Version.ShouldBe(Versions.v0_6_1);
            settings_v0_6_1.ProjectDirectory.ShouldBe(@"C:\projects");
            settings_v0_6_1.DefaultShowDates.ShouldBeTrue();
            settings_v0_6_1.DefaultUseClassicDates.ShouldBeTrue();
            settings_v0_6_1.DefaultNonWorkingDayMode.ShouldBe(NonWorkingDayMode.Weekends);
            settings_v0_6_1.DefaultHideCost.ShouldBeTrue();
            settings_v0_6_1.DefaultHideBilling.ShouldBeTrue();
            settings_v0_6_1.SelectedTheme.ShouldBe(@"Dark");
            settings_v0_6_1.MaxRecentProjectFilePaths.ShouldBe(10);
            settings_v0_6_1.RecentProjectFilePaths.ShouldBeEmpty();
        }

        [Fact]
        public void VersionMapper_Given_CurrentAppSettings_When_RoundTrippedThrough_v0_6_0_Then_RecentsReset()
        {
            var mapper = new VersionMapper();

            var current = new AppSettingsModel
            {
                ProjectDirectory = @"C:\projects",
                SelectedTheme = @"Dark",
                MaxRecentProjectFilePaths = 5,
                RecentProjectFilePaths = [@"C:\projects\alpha.zpp", @"C:\projects\beta.zpp"],
            };

            v0_6_0.AppSettingsModel downgraded = mapper.FromCurrentToV0_6_0(current);
            AppSettingsModel roundTripped = Converter.Upgrade(downgraded);

            roundTripped.ProjectDirectory.ShouldBe(@"C:\projects");
            roundTripped.SelectedTheme.ShouldBe(@"Dark");

            // The recents members have no v0.6.0 representation, so they reset.
            roundTripped.MaxRecentProjectFilePaths.ShouldBe(10);
            roundTripped.RecentProjectFilePaths.ShouldBeEmpty();
        }

        [Fact]
        public void VersionMapper_Given_CurrentAppSettings_When_RoundTrippedThrough_v0_6_1_Then_AllMembersSurvive()
        {
            var mapper = new VersionMapper();

            var current = new AppSettingsModel
            {
                Version = Versions.v0_6_1,
                ProjectDirectory = @"C:\projects",
                DefaultShowDates = true,
                DefaultUseClassicDates = true,
                DefaultNonWorkingDayMode = NonWorkingDayMode.CustomCalendar,
                DefaultHideCost = true,
                DefaultHideBilling = true,
                SelectedTheme = @"Dark",
                MaxRecentProjectFilePaths = 5,
                RecentProjectFilePaths = [@"C:\projects\alpha.zpp", @"C:\projects\beta.zpp"],
            };

            AppSettingsModel roundTripped = mapper.FromV0_6_1ToCurrent(mapper.FromCurrentToV0_6_1(current));

            roundTripped.ShouldBeEquivalentTo(current);
        }
    }
}
