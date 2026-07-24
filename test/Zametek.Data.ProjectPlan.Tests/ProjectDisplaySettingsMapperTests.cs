using Shouldly;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for the version mapping of the project display settings around the
    /// v0.6.1 split of the scenario chart Y metric into Y1/Y2 (with per-metric
    /// curve fitting). v0.6.0 stays frozen with the old single-Y member names,
    /// so the mapper must bridge YAxis to Y1Axis (and CurveFittingType to
    /// CurveFittingTypeY1) in both directions, defaulting the Y2 members.
    /// </summary>
    public class ProjectDisplaySettingsMapperTests
    {
        [Fact]
        public void VersionMapper_Given_v0_6_0_DisplaySettings_When_UpgradedTo_v0_6_1_Then_YBridgesToY1AndY2Defaults()
        {
            var mapper = new VersionMapper();

            var settings_v0_6_0 = new v0_6_0.ProjectDisplaySettingsModel
            {
                ProjectScenarioSortMode = SortMode.Name,
                ProjectScenarioSortDirection = Zametek.Common.ProjectPlan.SortDirection.Descending,
                ScenarioChartShowNames = true,
                ScenarioChartTrackedMetricXAxis = TrackedMetrics.NetworkDuration,
                ScenarioChartTrackedMetricYAxis = TrackedMetrics.CostsTotal,
                ScenarioChartCurveFittingType = CurveFittingType.Linear,
            };

            v0_6_1.ProjectDisplaySettingsModel settings_v0_6_1 = mapper.FromV0_6_0ToV0_6_1(settings_v0_6_0);

            settings_v0_6_1.ProjectScenarioSortMode.ShouldBe(SortMode.Name);
            settings_v0_6_1.ProjectScenarioSortDirection.ShouldBe(Zametek.Common.ProjectPlan.SortDirection.Descending);
            settings_v0_6_1.ScenarioChartShowNames.ShouldBeTrue();
            settings_v0_6_1.ScenarioChartTrackedMetricXAxis.ShouldBe(TrackedMetrics.NetworkDuration);
            settings_v0_6_1.ScenarioChartTrackedMetricY1Axis.ShouldBe(TrackedMetrics.CostsTotal);
            settings_v0_6_1.ScenarioChartTrackedMetricY2Axis.ShouldBe(TrackedMetrics.None);
            settings_v0_6_1.ScenarioChartCurveFittingTypeY1.ShouldBe(CurveFittingType.Linear);
            settings_v0_6_1.ScenarioChartCurveFittingTypeY2.ShouldBe(CurveFittingType.None);
            settings_v0_6_1.ScenarioChartShowDerivativeY1.ShouldBeFalse();
            settings_v0_6_1.ScenarioChartShowDerivativeY2.ShouldBeFalse();
            settings_v0_6_1.ScenarioChartAbsoluteCurveFittingY1.ShouldBeFalse();
            settings_v0_6_1.ScenarioChartAbsoluteCurveFittingY2.ShouldBeFalse();
        }

        [Fact]
        public void VersionMapper_Given_v0_6_0_Project_When_UpgradedTo_v0_6_1_Then_DisplaySettingsBridge()
        {
            var mapper = new VersionMapper();

            var project_v0_6_0 = new v0_6_0.ProjectModel
            {
                DisplaySettings = new v0_6_0.ProjectDisplaySettingsModel
                {
                    ScenarioChartTrackedMetricYAxis = TrackedMetrics.EffortsTotal,
                    ScenarioChartCurveFittingType = CurveFittingType.Power,
                },
            };

            v0_6_1.ProjectModel project_v0_6_1 = v0_6_1.Converter.Upgrade(mapper, project_v0_6_0);

            project_v0_6_1.DisplaySettings.ScenarioChartTrackedMetricY1Axis.ShouldBe(TrackedMetrics.EffortsTotal);
            project_v0_6_1.DisplaySettings.ScenarioChartTrackedMetricY2Axis.ShouldBe(TrackedMetrics.None);
            project_v0_6_1.DisplaySettings.ScenarioChartCurveFittingTypeY1.ShouldBe(CurveFittingType.Power);
            project_v0_6_1.DisplaySettings.ScenarioChartCurveFittingTypeY2.ShouldBe(CurveFittingType.None);
        }

        [Fact]
        public void VersionMapper_Given_CurrentDisplaySettings_When_RoundTrippedThrough_v0_6_0_Then_Y1SurvivesAndY2Drops()
        {
            var mapper = new VersionMapper();

            var current = new ProjectDisplaySettingsModel
            {
                ScenarioChartShowNames = true,
                ScenarioChartTrackedMetricXAxis = TrackedMetrics.RisksCriticality,
                ScenarioChartTrackedMetricY1Axis = TrackedMetrics.CostsTotal,
                ScenarioChartTrackedMetricY2Axis = TrackedMetrics.NetworkDuration,
                ScenarioChartCurveFittingTypeY1 = CurveFittingType.Linear,
                ScenarioChartCurveFittingTypeY2 = CurveFittingType.PolynomialOrder2,
                ScenarioChartShowDerivativeY1 = true,
                ScenarioChartShowDerivativeY2 = true,
                ScenarioChartAbsoluteCurveFittingY1 = true,
                ScenarioChartAbsoluteCurveFittingY2 = true,
            };

            v0_6_0.ProjectDisplaySettingsModel downgraded = mapper.FromCurrentToV0_6_0(current);

            downgraded.ScenarioChartTrackedMetricYAxis.ShouldBe(TrackedMetrics.CostsTotal);
            downgraded.ScenarioChartCurveFittingType.ShouldBe(CurveFittingType.Linear);

            ProjectDisplaySettingsModel roundTripped = mapper.FromV0_6_0ToCurrent(downgraded);

            roundTripped.ScenarioChartShowNames.ShouldBeTrue();
            roundTripped.ScenarioChartTrackedMetricXAxis.ShouldBe(TrackedMetrics.RisksCriticality);
            roundTripped.ScenarioChartTrackedMetricY1Axis.ShouldBe(TrackedMetrics.CostsTotal);
            roundTripped.ScenarioChartCurveFittingTypeY1.ShouldBe(CurveFittingType.Linear);

            // The Y2 members and the derivative display flags have no v0.6.0
            // representation, so they reset.
            roundTripped.ScenarioChartTrackedMetricY2Axis.ShouldBe(TrackedMetrics.None);
            roundTripped.ScenarioChartCurveFittingTypeY2.ShouldBe(CurveFittingType.None);
            roundTripped.ScenarioChartShowDerivativeY1.ShouldBeFalse();
            roundTripped.ScenarioChartShowDerivativeY2.ShouldBeFalse();
            roundTripped.ScenarioChartAbsoluteCurveFittingY1.ShouldBeFalse();
            roundTripped.ScenarioChartAbsoluteCurveFittingY2.ShouldBeFalse();
        }

        [Fact]
        public void VersionMapper_Given_CurrentDisplaySettings_When_RoundTrippedThrough_v0_6_1_Then_AllMembersSurvive()
        {
            var mapper = new VersionMapper();

            var current = new ProjectDisplaySettingsModel
            {
                ProjectScenarioSortMode = SortMode.ModifiedOn,
                ProjectScenarioSortDirection = Zametek.Common.ProjectPlan.SortDirection.Descending,
                ScenarioChartShowNames = true,
                ScenarioChartTrackedMetricXAxis = TrackedMetrics.RisksCriticality,
                ScenarioChartTrackedMetricY1Axis = TrackedMetrics.CostsTotal,
                ScenarioChartTrackedMetricY2Axis = TrackedMetrics.NetworkDuration,
                ScenarioChartCurveFittingTypeY1 = CurveFittingType.Linear,
                ScenarioChartCurveFittingTypeY2 = CurveFittingType.PolynomialOrder2,
                ScenarioChartShowDerivativeY1 = true,
                ScenarioChartShowDerivativeY2 = true,
                ScenarioChartAbsoluteCurveFittingY1 = true,
                ScenarioChartAbsoluteCurveFittingY2 = true,
            };

            ProjectDisplaySettingsModel roundTripped = mapper.FromV0_6_1ToCurrent(mapper.FromCurrentToV0_6_1(current));

            roundTripped.ShouldBeEquivalentTo(current);
        }
    }
}
