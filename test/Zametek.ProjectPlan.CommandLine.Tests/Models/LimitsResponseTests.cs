using Shouldly;
using System.Text.Json;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for the limits as /v1/info gives them: the server's, with its durations as ISO 8601.
    /// </summary>
    public class LimitsResponseTests
    {
        [Fact]
        public void From_Given_TheLimits_Then_EachInItsMemberAndTheDurationsAsDurations()
        {
            LimitsResponse response = LimitsResponse.From(new ServeLimits
            {
                MaxJobs = 3,
                MaxQueue = 7,
                MaxUploadMegabytes = 11,
                MaxChartWidth = 1500,
                MaxChartHeight = 900,
                JobTimeoutSeconds = 90,
                MaxCompileTimeoutMilliseconds = 2500,
            });

            response.ShouldBe(new LimitsResponse(3, 7, 11, 1500, 900, TimeSpan.FromSeconds(90), TimeSpan.FromMilliseconds(2500)));
        }

        [Fact]
        public void Serialize_Given_TheDefaultLimits_Then_TheNamesAndDurationsTheContractHas()
        {
            string json = JsonSerializer.Serialize(
                LimitsResponse.From(new ServeLimits { MaxJobs = 4, MaxQueue = 8 }),
                JobJsonHelper.ServerOptions);

            json.ShouldBe(
                @"{""maxJobs"":4,""maxQueue"":8,""maxUploadMegabytes"":50,""maxChartWidth"":5000,""maxChartHeight"":5000,""jobTimeout"":""PT2M"",""maxCompileTimeout"":""PT1M""}");
        }

        [Fact]
        public void From_Given_NoLimits_Then_Throws()
        {
            Should.Throw<ArgumentNullException>(() => LimitsResponse.From(null!));
        }
    }
}
