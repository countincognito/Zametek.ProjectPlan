using Shouldly;
using System;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The metrics that the axes of the scenario chart can show: each has a label of its own, and the chart reads the value of
    /// each from the metrics of a scenario.
    /// </summary>
    public class ScenarioChartTrackedMetricsTests
    {
        public static TheoryData<TrackedMetrics> EveryMetric =>
            [.. Enum.GetValues<TrackedMetrics>().Where(x => x != TrackedMetrics.None)];

        [Theory]
        [MemberData(nameof(EveryMetric))]
        public void TrackedMetricsValue_Given_AMetric_Then_HasALabel(TrackedMetrics metric)
        {
            StringConverters.TrackedMetricsValue(metric).ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public void TrackedMetricsValue_Given_EveryMetric_Then_NoTwoHaveTheSameLabel()
        {
            string[] labels = [.. Enum.GetValues<TrackedMetrics>().Select(x => StringConverters.TrackedMetricsValue(x))];

            labels.Distinct().Count().ShouldBe(labels.Length);
        }

        [Fact]
        public void GetMetricFunction_Given_TheHeadcount_Then_ReadsEachOfItsValues()
        {
            var metrics = new MetricsModel
            {
                Headcount = new HeadcountModel { Peak = 4, Average = 2.5, PeakToAverageRatio = 1.6 },
            };

            ScenarioChartManagerViewModel.GetMetricFunction(TrackedMetrics.HeadcountPeak)(metrics).ShouldBe(4.0);
            ScenarioChartManagerViewModel.GetMetricFunction(TrackedMetrics.HeadcountAverage)(metrics).ShouldBe(2.5);
            ScenarioChartManagerViewModel.GetMetricFunction(TrackedMetrics.HeadcountPeakToAverageRatio)(metrics).ShouldBe(1.6);
        }

        [Fact]
        public void GetMetricFunction_Given_NoHeadcount_Then_Zero()
        {
            // As for every other metric, a value that the metrics do not have is 0.
            ScenarioChartManagerViewModel.GetMetricFunction(TrackedMetrics.HeadcountPeakToAverageRatio)(new MetricsModel()).ShouldBe(0.0);
        }
    }
}
