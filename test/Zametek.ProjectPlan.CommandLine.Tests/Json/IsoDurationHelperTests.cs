using Shouldly;
using System.Text.Json;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for durations as zpp serve's API writes them: ISO 8601, in days, hours, minutes and seconds, and nothing
    /// longer.
    /// </summary>
    public class IsoDurationHelperTests
    {
        [Theory]
        [InlineData(@"PT5S", 5_000)]
        [InlineData(@"PT0.5S", 500)]
        [InlineData(@"PT0.001S", 1)]
        [InlineData(@"PT0S", 0)]
        [InlineData(@"PT1M", 60_000)]
        [InlineData(@"PT1H30M", 5_400_000)]
        [InlineData(@"P1D", 86_400_000)]
        [InlineData(@"P1DT2H3M4.5S", 93_784_500)]
        [InlineData(@"PT90S", 90_000)]
        public void TryParse_Given_ADurationOfDaysHoursMinutesAndSeconds_Then_ItsLength(string text, double milliseconds)
        {
            IsoDurationHelper.TryParse(text, out TimeSpan duration).ShouldBeTrue();

            duration.TotalMilliseconds.ShouldBe(milliseconds);
        }

        [Theory]
        [InlineData(@"")]
        [InlineData(@" ")]
        [InlineData(@"P")]
        [InlineData(@"PT")]
        [InlineData(@"P1DT")]
        [InlineData(@"5S")]
        [InlineData(@"PT5")]
        [InlineData(@"PT-5S")]
        [InlineData(@"-PT5S")]
        [InlineData(@"P1Y")]
        [InlineData(@"P1M")]
        [InlineData(@"P1W")]
        [InlineData(@"PT5s")]
        [InlineData(@"pt5s")]
        [InlineData(@"PT5S ")]
        [InlineData(@" PT5S")]
        [InlineData(@"PT1.S")]
        [InlineData(@"PT.5S")]
        [InlineData(@"P1.5D")]
        [InlineData(@"PT5M5H")]
        [InlineData(@"5000")]
        [InlineData(@"00:00:05")]
        [InlineData(@"PT99999999999999999999999S")]
        [InlineData(@"P99999999999999D")]
        public void TryParse_Given_AnythingElse_Then_NotADuration(string text)
        {
            IsoDurationHelper.TryParse(text, out TimeSpan duration).ShouldBeFalse();

            duration.ShouldBe(default);
        }

        [Fact]
        public void TryParse_Given_Nothing_Then_NotADuration()
        {
            IsoDurationHelper.TryParse(null, out _).ShouldBeFalse();
        }

        [Theory]
        [InlineData(0, @"PT0S")]
        [InlineData(1, @"PT0.001S")]
        [InlineData(500, @"PT0.5S")]
        [InlineData(5_000, @"PT5S")]
        [InlineData(60_000, @"PT1M")]
        [InlineData(120_000, @"PT2M")]
        [InlineData(3_600_000, @"PT1H")]
        [InlineData(86_400_000, @"P1D")]
        public void ToString_Given_ADuration_Then_ISO8601(int milliseconds, string expected)
        {
            IsoDurationHelper.ToString(TimeSpan.FromMilliseconds(milliseconds)).ShouldBe(expected);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(999)]
        [InlineData(5_000)]
        [InlineData(61_001)]
        [InlineData(90_000_000)]
        public void ToString_Given_ADuration_Then_ParsesBackToIt(int milliseconds)
        {
            var duration = TimeSpan.FromMilliseconds(milliseconds);

            IsoDurationHelper.TryParse(IsoDurationHelper.ToString(duration), out TimeSpan parsed).ShouldBeTrue();

            parsed.ShouldBe(duration);
        }

        [Fact]
        public void Converter_Given_ADuration_Then_WritesAndReadsItAsAString()
        {
            string json = JsonSerializer.Serialize(TimeSpan.FromSeconds(120), JobJsonHelper.ServerOptions);

            json.ShouldBe(@"""PT2M""");
            JsonSerializer.Deserialize<TimeSpan>(json, JobJsonHelper.ServerOptions).ShouldBe(TimeSpan.FromSeconds(120));
            JsonSerializer.Deserialize<TimeSpan?>(@"null", JobJsonHelper.ServerOptions).ShouldBeNull();
        }

        [Theory]
        [InlineData(@"5000")]
        [InlineData(@"""00:00:05""")]
        [InlineData(@"""P1Y""")]
        [InlineData(@"true")]
        public void Converter_Given_SomethingThatIsNotADuration_Then_Refuses(string json)
        {
            Should.Throw<JsonException>(() => JsonSerializer.Deserialize<TimeSpan>(json, JobJsonHelper.ServerOptions));
        }
    }
}
