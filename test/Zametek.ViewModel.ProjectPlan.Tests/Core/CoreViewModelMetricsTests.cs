using Shouldly;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for the six metrics that the core holds (the risk, cost, billing, margin, effort and network metrics): each takes
    /// a value that differs from the one it holds, and raises its change, and the change of the record as a whole, only then.
    /// </summary>
    /// <remarks>
    /// The six are set from the file of a scenario, which sets them one by one after the compilation, and by the builds of
    /// the outputs. The tests go through the public members of the core.
    /// </remarks>
    public class CoreViewModelMetricsTests
    {
        private const double c_Distinct = 424242.0;

        public static TheoryData<string> MetricNames => new()
        {
            nameof(CoreViewModel.RiskMetrics),
            nameof(CoreViewModel.CostMetrics),
            nameof(CoreViewModel.BillingMetrics),
            nameof(CoreViewModel.MarginMetrics),
            nameof(CoreViewModel.EffortMetrics),
            nameof(CoreViewModel.NetworkMetrics),
        };

        private static readonly string[] s_Names =
        [
            nameof(CoreViewModel.RiskMetrics),
            nameof(CoreViewModel.CostMetrics),
            nameof(CoreViewModel.BillingMetrics),
            nameof(CoreViewModel.MarginMetrics),
            nameof(CoreViewModel.EffortMetrics),
            nameof(CoreViewModel.NetworkMetrics),
        ];

        #region The file of a scenario

        [Theory]
        [MemberData(nameof(MetricNames))]
        public void ProcessProjectScenario_Given_AFileMetricThatDiffers_Then_OnlyThatMetricIsRaisedOnceMoreAndHoldsTheFileValue(string property)
        {
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(3);
            MetricsModel computed = Computed(scenario);

            (Dictionary<string, int> equal, CoreViewModel equalCore) = Load(scenario with { Metrics = computed });
            using (equalCore)
            {
                MetricsModel changed = Changed(computed, property);
                (Dictionary<string, int> different, CoreViewModel differentCore) = Load(scenario with { Metrics = changed });
                using (differentCore)
                {
                    foreach (string name in s_Names)
                    {
                        different[name].ShouldBe(equal[name] + (name == property ? 1 : 0), name);
                    }

                    different[nameof(CoreViewModel.Metrics)].ShouldBe(equal[nameof(CoreViewModel.Metrics)] + 1);
                    differentCore.Metrics.ShouldBe(changed);
                    equalCore.Metrics.ShouldBe(computed);
                }
            }
        }

        [Fact]
        public void ProcessProjectScenario_Given_TheFileMetricsThatTheCompilationGives_Then_NoMetricIsRaisedBeyondTheCompilation()
        {
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(3);
            MetricsModel computed = Computed(scenario);

            (Dictionary<string, int> equal, CoreViewModel core) = Load(scenario with { Metrics = computed });
            using (core)
            {
                // The load resets the metrics to the empty ones that the core begins with, the compilation sets them, and
                // the file would set them again. Each is raised once at most (by the compilation), and so the file added
                // nothing.
                foreach (string name in s_Names)
                {
                    equal[name].ShouldBeLessThanOrEqualTo(1, name);
                }
            }
        }

        #endregion

        #region The builds of the outputs

        [Fact]
        public void BuildNetworkMetrics_Given_AChangedNetwork_Then_TheScenarioIsMarkedUpdatedAndTheOutputsAreNotStale()
        {
            using CoreViewModel core = Loaded();
            int? before = core.NetworkMetrics.Duration;
            core.SetActivityDuration(1, 30);
            core.RunCompile();
            core.IsProjectScenarioUpdated = false;
            core.HasStaleOutputs = false;

            core.BuildNetworkMetrics();

            core.NetworkMetrics.Duration.ShouldNotBe(before);
            core.IsProjectScenarioUpdated.ShouldBeTrue();
            core.HasStaleOutputs.ShouldBeFalse();
        }

        [Fact]
        public void BuildNetworkMetrics_Given_AnUnchangedNetwork_Then_TheScenarioIsNotMarkedUpdatedAndTheMetricIsNotRaised()
        {
            using CoreViewModel core = Loaded();
            core.BuildNetworkMetrics();
            core.IsProjectScenarioUpdated = false;
            core.HasStaleOutputs = false;
            Dictionary<string, int> raised = Watch(core);

            core.BuildNetworkMetrics();

            core.IsProjectScenarioUpdated.ShouldBeFalse();
            raised[nameof(CoreViewModel.NetworkMetrics)].ShouldBe(0);
            raised[nameof(CoreViewModel.Metrics)].ShouldBe(1);
        }

        [Fact]
        public void BuildRiskMetrics_Given_AChangedRisk_Then_TheScenarioIsMarkedUpdatedAndTheOutputsAreNotStale()
        {
            using CoreViewModel core = Loaded();
            RisksModel before = core.RiskMetrics;
            core.SetActivityDuration(1, 30);
            core.RunCompile();
            core.IsProjectScenarioUpdated = false;
            core.HasStaleOutputs = false;

            core.BuildRiskMetrics();

            core.RiskMetrics.ShouldNotBe(before);
            core.IsProjectScenarioUpdated.ShouldBeTrue();
            core.HasStaleOutputs.ShouldBeFalse();
        }

        [Fact]
        public void BuildRiskMetrics_Given_AnUnchangedRisk_Then_TheScenarioIsNotMarkedUpdatedAndTheMetricIsNotRaised()
        {
            using CoreViewModel core = Loaded();
            core.BuildRiskMetrics();
            core.IsProjectScenarioUpdated = false;
            core.HasStaleOutputs = false;
            Dictionary<string, int> raised = Watch(core);

            core.BuildRiskMetrics();

            core.IsProjectScenarioUpdated.ShouldBeFalse();
            raised[nameof(CoreViewModel.RiskMetrics)].ShouldBe(0);
        }

        [Fact]
        public void BuildFinancialMetrics_Given_ChangedCosts_Then_TheCostIsRaisedAndTheScenarioIsMarkedUpdatedAndTheOutputsAreNotStale()
        {
            using CoreViewModel core = Loaded();
            CostsModel before = core.CostMetrics;
            core.ResourceSettings = core.ResourceSettings with
            {
                Resources = [.. core.ResourceSettings.Resources.Select(x => x with { UnitCost = 50.0 })],
            };
            core.IsProjectScenarioUpdated = false;
            core.HasStaleOutputs = false;
            Dictionary<string, int> raised = Watch(core);

            core.BuildFinancialMetrics();

            core.CostMetrics.ShouldNotBe(before);
            raised[nameof(CoreViewModel.CostMetrics)].ShouldBe(1);
            core.IsProjectScenarioUpdated.ShouldBeTrue();
            core.HasStaleOutputs.ShouldBeFalse();
        }

        [Fact]
        public void BuildFinancialMetrics_Given_UnchangedFinances_Then_NothingIsRaisedAndTheScenarioIsNotMarkedUpdated()
        {
            using CoreViewModel core = Loaded();
            core.BuildFinancialMetrics();
            core.IsProjectScenarioUpdated = false;
            core.HasStaleOutputs = false;
            Dictionary<string, int> raised = Watch(core);

            core.BuildFinancialMetrics();

            core.IsProjectScenarioUpdated.ShouldBeFalse();
            raised[nameof(CoreViewModel.CostMetrics)].ShouldBe(0);
            raised[nameof(CoreViewModel.BillingMetrics)].ShouldBe(0);
            raised[nameof(CoreViewModel.MarginMetrics)].ShouldBe(0);
            raised[nameof(CoreViewModel.EffortMetrics)].ShouldBe(0);
        }

        #endregion

        // A core that holds the plan of three chained activities, compiled and with its outputs built.
        private static CoreViewModel Loaded()
        {
            CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(3), Guid.NewGuid(), @"Test");
            return core;
        }

        // The metrics that the compilation of the scenario gives. A load ends with the metrics of the file, which are empty
        // for this scenario, and so the builds of the outputs give the computed ones back.
        private static MetricsModel Computed(ProjectScenarioModel scenario)
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"Computed");
            core.BuildNetworkMetrics();
            core.BuildRiskMetrics();
            core.BuildFinancialMetrics();
            return core.BuildProjectScenario().Metrics;
        }

        // Loads the scenario in a new core, and counts how often each of the metrics and the record was raised on the way.
        private static (Dictionary<string, int> Raised, CoreViewModel Core) Load(ProjectScenarioModel scenario)
        {
            CoreViewModel core = CoreViewModelFixture.Create();
            Dictionary<string, int> raised = Watch(core);

            core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"Test");

            return (raised, core);
        }

        private static Dictionary<string, int> Watch(INotifyPropertyChanged source)
        {
            Dictionary<string, int> raised = s_Names.Append(nameof(CoreViewModel.Metrics)).ToDictionary(x => x, _ => 0);

            source.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is not null && raised.ContainsKey(e.PropertyName))
                {
                    raised[e.PropertyName]++;
                }
            };

            return raised;
        }

        // The metrics, with the record of the property changed to one that the compilation cannot give.
        private static MetricsModel Changed(MetricsModel metrics, string property) =>
            property switch
            {
                nameof(CoreViewModel.RiskMetrics) => metrics with { Risks = metrics.Risks with { Criticality = c_Distinct } },
                nameof(CoreViewModel.CostMetrics) => metrics with { Costs = metrics.Costs with { Total = c_Distinct } },
                nameof(CoreViewModel.BillingMetrics) => metrics with { Billings = metrics.Billings with { Total = c_Distinct } },
                nameof(CoreViewModel.MarginMetrics) => metrics with { Margins = metrics.Margins with { Total = c_Distinct } },
                nameof(CoreViewModel.EffortMetrics) => metrics with { Efforts = metrics.Efforts with { Total = c_Distinct } },
                nameof(CoreViewModel.NetworkMetrics) => metrics with { Network = metrics.Network with { Duration = (int)c_Distinct } },
                _ => throw new ArgumentOutOfRangeException(nameof(property), property, null),
            };
    }
}
