using Shouldly;
using System;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    // The data of the scenario chart: the points that it plots for each Y axis, in the order of the chart, and the fit of each
    // axis. The settings of the chart are those that the plan keeps, and so the data and the chart always agree.
    public class ScenarioChartDataBuilderTests
    {
        private static readonly Guid s_AlphaId = Guid.Parse(@"8f4d2f43-4c1b-4f16-9df8-40e1a2b3c4d5");
        private static readonly Guid s_BetaId = Guid.Parse(@"17c3e2d9-95a4-4b47-b7ff-51f0a1b2c3d4");
        private static readonly Guid s_GammaId = Guid.Parse(@"0b9a8c7d-6e5f-4a3b-8c2d-1e0f9a8b7c6d");

        private static TrackedMetricsModel Tracked(Guid id, string path, int duration, double? directCost) => new()
        {
            NodeId = id,
            Name = path.Split('/').Last(),
            Path = path,
            Metrics = new MetricsModel
            {
                Network = new NetworkModel { Duration = duration },
                Costs = new CostsModel { Direct = directCost, Total = directCost * 2 },
            },
        };

        // Three tracked scenarios in the order of the project. Two have the same duration.
        private static readonly TrackedMetricsSetModel s_Set = new()
        {
            TrackedMetrics =
            [
                Tracked(s_AlphaId, @"Alpha", 10, 100.0),
                Tracked(s_BetaId, @"Plans/Beta", 5, 80.0),
                Tracked(s_GammaId, @"Gamma", 10, 90.0),
            ],
        };

        [Fact]
        public void Build_Given_AnAxisOfXAndOfY_Then_ThePointsInTheOrderOfTheChartAndTheFit()
        {
            ScenarioChartDataModel? data = ScenarioChartDataBuilder.Build(
                s_Set,
                TrackedMetrics.NetworkDuration,
                TrackedMetrics.CostsDirect,
                TrackedMetrics.None,
                CurveFittingType.Linear,
                CurveFittingType.None,
                absoluteCurveFittingY1: true,
                absoluteCurveFittingY2: false);

            data.ShouldNotBeNull();
            data.XMetric.ShouldBe(TrackedMetrics.NetworkDuration);
            data.Y2.ShouldBeNull();

            ScenarioChartSeriesModel y1 = data.Y1.ShouldNotBeNull();
            y1.Metric.ShouldBe(TrackedMetrics.CostsDirect);
            y1.CurveFittingType.ShouldBe(CurveFittingType.Linear);
            y1.AbsoluteCurveFitting.ShouldBeTrue();

            // The chart orders its points by X, and then by Y.
            y1.Points.ShouldBe(
            [
                new ScenarioChartPointModel { Id = s_BetaId, Path = @"Plans/Beta", X = 5.0, Y = 80.0 },
                new ScenarioChartPointModel { Id = s_GammaId, Path = @"Gamma", X = 10.0, Y = 90.0 },
                new ScenarioChartPointModel { Id = s_AlphaId, Path = @"Alpha", X = 10.0, Y = 100.0 },
            ]);

            // The fit is that of the points, as the calculator gives it.
            y1.CurveFit.ShouldBe(CurveFitCalculator.Fit([5.0, 10.0, 10.0], [80.0, 90.0, 100.0], CurveFittingType.Linear));
            y1.CurveFit.ShouldNotBeNull().Coefficients.Count.ShouldBe(2);
        }

        [Fact]
        public void Build_Given_TwoAxesOfY_Then_EachHasItsOwnPointsAndFit()
        {
            ScenarioChartDataModel data = ScenarioChartDataBuilder.Build(
                s_Set,
                TrackedMetrics.NetworkDuration,
                TrackedMetrics.CostsDirect,
                TrackedMetrics.CostsTotal,
                CurveFittingType.None,
                CurveFittingType.PolynomialOrder2,
                absoluteCurveFittingY1: false,
                absoluteCurveFittingY2: false).ShouldNotBeNull();

            ScenarioChartSeriesModel y1 = data.Y1.ShouldNotBeNull();
            y1.CurveFittingType.ShouldBe(CurveFittingType.None);
            y1.CurveFit.ShouldBeNull();

            // Two different durations are too few for a fit of three parameters: the axis has its points and no fit.
            ScenarioChartSeriesModel y2 = data.Y2.ShouldNotBeNull();
            y2.Metric.ShouldBe(TrackedMetrics.CostsTotal);
            y2.CurveFittingType.ShouldBe(CurveFittingType.PolynomialOrder2);
            y2.Points.Select(x => (x.Path, x.X, x.Y)).ShouldBe([(@"Plans/Beta", 5.0, 160.0), (@"Gamma", 10.0, 180.0), (@"Alpha", 10.0, 200.0)]);
            y2.CurveFit.ShouldBeNull();
        }

        [Fact]
        public void Build_Given_AMetricThatAScenarioDoesNotHave_Then_ItIs0AsOnTheChart()
        {
            var set = new TrackedMetricsSetModel { TrackedMetrics = [Tracked(s_AlphaId, @"Alpha", 10, null)] };

            ScenarioChartDataModel data = ScenarioChartDataBuilder.Build(
                set,
                TrackedMetrics.NetworkDuration,
                TrackedMetrics.CostsDirect,
                TrackedMetrics.None,
                CurveFittingType.None,
                CurveFittingType.None,
                absoluteCurveFittingY1: false,
                absoluteCurveFittingY2: false).ShouldNotBeNull();

            data.Y1.ShouldNotBeNull().Points.Single().Y.ShouldBe(0.0);
        }

        [Fact]
        public void Build_Given_NoTrackedScenario_Then_TheAxesWithNoPoints()
        {
            ScenarioChartDataModel data = ScenarioChartDataBuilder.Build(
                new TrackedMetricsSetModel(),
                TrackedMetrics.NetworkDuration,
                TrackedMetrics.CostsDirect,
                TrackedMetrics.None,
                CurveFittingType.Linear,
                CurveFittingType.None,
                absoluteCurveFittingY1: false,
                absoluteCurveFittingY2: false).ShouldNotBeNull();

            data.Y1.ShouldNotBeNull().Points.ShouldBeEmpty();
            data.Y1.CurveFit.ShouldBeNull();
        }

        [Theory]
        [InlineData(TrackedMetrics.None, TrackedMetrics.CostsDirect, TrackedMetrics.CostsTotal)]
        [InlineData(TrackedMetrics.NetworkDuration, TrackedMetrics.None, TrackedMetrics.None)]
        public void Build_Given_NoAxisOfXOrOfY_Then_NoData(TrackedMetrics x, TrackedMetrics y1, TrackedMetrics y2)
        {
            // The chart is then empty.
            ScenarioChartDataBuilder.Build(s_Set, x, y1, y2, CurveFittingType.Linear, CurveFittingType.Linear, false, false).ShouldBeNull();
        }

        [Fact]
        public void Equals_Given_TwoDataWithTheSamePointsAndFits_Then_TheyAreEqual()
        {
            // The records hold lists, and two of them are equal when their lists hold the same values.
            ScenarioChartDataModel? Build() => ScenarioChartDataBuilder.Build(
                s_Set,
                TrackedMetrics.NetworkDuration,
                TrackedMetrics.CostsDirect,
                TrackedMetrics.CostsTotal,
                CurveFittingType.Linear,
                CurveFittingType.Linear,
                absoluteCurveFittingY1: false,
                absoluteCurveFittingY2: false);

            ScenarioChartDataModel first = Build().ShouldNotBeNull();
            ScenarioChartDataModel second = Build().ShouldNotBeNull();

            second.ShouldBe(first);
            second.GetHashCode().ShouldBe(first.GetHashCode());
            (second with { Y2 = null }).ShouldNotBe(first);
        }
    }
}
