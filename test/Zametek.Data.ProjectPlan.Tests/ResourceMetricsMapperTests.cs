using Shouldly;
using System.Collections.Generic;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for the version mapping of the per-resource metrics that are new
    /// in v0.6.1. They persist alongside the project metrics in each scenario,
    /// so they must survive a Current/v0.6.1 round trip intact, while a v0.6.0
    /// scenario (which has no representation for them) must upgrade to an
    /// empty list that the first compile then populates.
    /// </summary>
    public class ResourceMetricsMapperTests
    {
        private static List<ResourceMetricsModel> CreateResourceMetrics()
        {
            return
            [
                new ResourceMetricsModel
                {
                    ResourceId = 3,
                    ResourceName = @"Alpha",
                    Costs = new ResourceCostsModel
                    {
                        Direct = 100.0,
                        Indirect = 20.0,
                        Other = 3.0,
                        Total = 123.0,
                    },
                    Billings = new ResourceBillingsModel
                    {
                        Direct = 200.0,
                        Indirect = 40.0,
                        Other = 6.0,
                        Total = 246.0,
                    },
                    Margins = new ResourceMarginsModel
                    {
                        Direct = 0.5,
                        Indirect = 0.5,
                        Other = 0.5,
                        Total = 0.5,
                        DirectAbsolute = 100.0,
                        IndirectAbsolute = 20.0,
                        OtherAbsolute = 3.0,
                        TotalAbsolute = 123.0,
                    },
                    Efforts = new ResourceEffortsModel
                    {
                        Direct = 10.0,
                        Indirect = 2.0,
                        Other = 1.0,
                        Total = 13.0,
                        Activity = 9.0,
                        Efficiency = 9.0 / 13.0,
                    },
                },
                new ResourceMetricsModel
                {
                    // An implicit/spare resource: no settings resource behind it.
                    ResourceId = null,
                    ResourceName = @"Resource 1",
                    Costs = new ResourceCostsModel
                    {
                        Direct = 7.0,
                        Indirect = 0.0,
                        Other = 0.0,
                        Total = 7.0,
                    },
                    Billings = new ResourceBillingsModel
                    {
                        Direct = 0.0,
                        Indirect = 0.0,
                        Other = 0.0,
                        Total = 0.0,
                    },
                    Margins = new ResourceMarginsModel
                    {
                        Direct = 0.0,
                        Indirect = 0.0,
                        Other = 0.0,
                        Total = 0.0,
                        DirectAbsolute = -7.0,
                        IndirectAbsolute = 0.0,
                        OtherAbsolute = 0.0,
                        TotalAbsolute = -7.0,
                    },
                    Efforts = new ResourceEffortsModel
                    {
                        Direct = 5.0,
                        Indirect = 0.0,
                        Other = 0.0,
                        Total = 5.0,
                        Activity = 5.0,
                        Efficiency = 1.0,
                    },
                },
            ];
        }

        [Fact]
        public void VersionMapper_Given_CurrentScenario_When_RoundTrippedThrough_v0_6_1_Then_ResourceMetricsSurvive()
        {
            var mapper = new VersionMapper();

            var current = new ProjectScenarioModel
            {
                ResourceMetrics = CreateResourceMetrics(),
            };

            v0_6_1.ProjectScenarioModel downgraded = mapper.FromCurrentToV0_6_1(current);

            downgraded.ResourceMetrics.Count.ShouldBe(2);

            ProjectScenarioModel roundTripped = mapper.FromV0_6_1ToCurrent(downgraded);

            roundTripped.ResourceMetrics.ShouldBeEquivalentTo(current.ResourceMetrics);
        }

        [Fact]
        public void VersionMapper_Given_CurrentScenario_When_RoundTrippedThrough_v0_6_0_Then_ResourceMetricsDrop()
        {
            var mapper = new VersionMapper();

            var current = new ProjectScenarioModel
            {
                ResourceMetrics = CreateResourceMetrics(),
            };

            v0_6_0.ProjectScenarioModel downgraded = mapper.FromCurrentToV0_6_0(current);

            ProjectScenarioModel roundTripped = mapper.FromV0_6_0ToCurrent(downgraded);

            // v0.6.0 has no representation for the per-resource metrics, so
            // they reset to an empty list that the first compile repopulates.
            roundTripped.ResourceMetrics.ShouldBeEmpty();
        }

        [Fact]
        public void VersionMapper_Given_v0_6_0_Scenario_When_UpgradedTo_v0_6_1_Then_ResourceMetricsDefaultEmpty()
        {
            var mapper = new VersionMapper();

            var scenario_v0_6_0 = new v0_6_0.ProjectScenarioModel();

            v0_6_1.ProjectScenarioModel scenario_v0_6_1 = mapper.FromV0_6_0ToV0_6_1(scenario_v0_6_0);

            scenario_v0_6_1.ResourceMetrics.ShouldBeEmpty();
        }
    }
}
