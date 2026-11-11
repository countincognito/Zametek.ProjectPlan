using Shouldly;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan.Tests
{
    /// <summary>
    /// The headcount of the metrics (the peak, the average and their ratio) is new in v0.6.2. It survives a Current/v0.6.2
    /// round trip. A scenario from v0.6.1 upgrades to an empty headcount, which the first compile fills, and keeps its other
    /// metrics.
    /// </summary>
    public class HeadcountMapperTests
    {
        private static MetricsModel Metrics() => new()
        {
            Network = new NetworkModel { Duration = 10 },
            Headcount = new HeadcountModel { Peak = 4, Average = 2.5, PeakToAverageRatio = 1.6 },
        };

        [Fact]
        public void VersionMapper_Given_CurrentScenario_When_RoundTrippedThrough_v0_6_2_Then_TheHeadcountSurvives()
        {
            var mapper = new VersionMapper();
            var current = new ProjectScenarioModel { Metrics = Metrics() };

            v0_6_2.ProjectScenarioModel stored = mapper.FromCurrentToV0_6_2(current);

            stored.Metrics.Headcount.Peak.ShouldBe(4);
            stored.Metrics.Headcount.Average.ShouldBe(2.5);
            stored.Metrics.Headcount.PeakToAverageRatio.ShouldBe(1.6);
            mapper.FromV0_6_2ToCurrent(stored).Metrics.ShouldBe(Metrics());
        }

        [Fact]
        public void VersionMapper_Given_v0_6_1_Scenario_When_UpgradedTo_v0_6_2_Then_TheHeadcountIsEmptyAndTheOtherMetricsStay()
        {
            var mapper = new VersionMapper();
            v0_6_1.ProjectScenarioModel older = mapper.FromCurrentToV0_6_1(new ProjectScenarioModel { Metrics = Metrics() });

            v0_6_2.ProjectScenarioModel upgraded = mapper.FromV0_6_1ToV0_6_2(older);

            upgraded.Metrics.Headcount.ShouldBe(new v0_6_2.HeadcountModel());
            upgraded.Metrics.Network.Duration.ShouldBe(10);
        }

        [Fact]
        public void VersionMapper_Given_CurrentScenario_When_RoundTrippedThrough_v0_6_1_Then_TheHeadcountDrops()
        {
            var mapper = new VersionMapper();

            ProjectScenarioModel roundTripped = mapper.FromV0_6_1ToCurrent(mapper.FromCurrentToV0_6_1(new ProjectScenarioModel { Metrics = Metrics() }));

            roundTripped.Metrics.Headcount.ShouldBe(new HeadcountModel());
            roundTripped.Metrics.Network.Duration.ShouldBe(10);
        }
    }
}
