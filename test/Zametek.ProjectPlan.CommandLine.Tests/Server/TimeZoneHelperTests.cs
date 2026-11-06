using Shouldly;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for the name of a time zone as /v1/info says it: the IANA database's, whatever the platform calls it.
    /// </summary>
    public class TimeZoneHelperTests
    {
        [Theory]
        [InlineData(@"Europe/London", @"Europe/London")]
        [InlineData(@"America/Los_Angeles", @"America/Los_Angeles")]
        [InlineData(@"Asia/Tokyo", @"Asia/Tokyo")]
        [InlineData(@"GMT Standard Time", @"Europe/London")]
        [InlineData(@"Pacific Standard Time", @"America/Los_Angeles")]
        [InlineData(@"Tokyo Standard Time", @"Asia/Tokyo")]
        public void GetIanaId_Given_AZoneByAnyOfItsNames_Then_TheIanaName(string id, string expected)
        {
            TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(id);

            TimeZoneHelper.GetIanaId(zone).ShouldBe(expected);
        }

        [Fact]
        public void GetIanaId_Given_Utc_Then_UtcAsTheDatabaseSaysIt()
        {
            TimeZoneHelper.GetIanaId(TimeZoneInfo.Utc).ShouldBeOneOf(@"UTC", @"Etc/UTC");
        }

        [Fact]
        public void GetIanaId_Given_AZoneWithNoIanaName_Then_ThePlatformsOwn()
        {
            var zone = TimeZoneInfo.CreateCustomTimeZone(@"Contoso Standard Time", TimeSpan.FromHours(1), @"Contoso", @"Contoso");

            TimeZoneHelper.GetIanaId(zone).ShouldBe(@"Contoso Standard Time");
        }

        [Fact]
        public void GetIanaId_Given_TheLocalZone_Then_AZoneThatAProgramCanLookUp()
        {
            string id = TimeZoneHelper.GetIanaId(TimeZoneInfo.Local);

            id.ShouldNotBeNullOrWhiteSpace();
            TimeZoneInfo.FindSystemTimeZoneById(id).BaseUtcOffset.ShouldBe(TimeZoneInfo.Local.BaseUtcOffset);
        }

        [Fact]
        public void GetIanaId_Given_NoZone_Then_Throws()
        {
            Should.Throw<ArgumentNullException>(() => TimeZoneHelper.GetIanaId(null!));
        }
    }
}
