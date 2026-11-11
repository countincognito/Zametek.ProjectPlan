using Shouldly;
using System;
using System.IO;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    // The data of the scenario chart as two CSV files, as zpp and the panel write them: the points of each Y axis, and the fit
    // of each Y axis. They are written as the other CSV files are: RFC 4180, the invariant culture, and a line feed at the end
    // of each line. A list of coefficients has a space between two of them, from the constant up.
    public class ScenarioChartCsvWriterTests
    {
        private static readonly Guid s_AlphaId = Guid.Parse(@"8f4d2f43-4c1b-4f16-9df8-40e1a2b3c4d5");
        private static readonly Guid s_BetaId = Guid.Parse(@"17c3e2d9-95a4-4b47-b7ff-51f0a1b2c3d4");

        private static readonly ScenarioChartDataModel s_Data = new()
        {
            XMetric = TrackedMetrics.NetworkDuration,
            Y1 = new ScenarioChartSeriesModel
            {
                Metric = TrackedMetrics.CostsDirect,
                CurveFittingType = CurveFittingType.PolynomialOrder2,
                AbsoluteCurveFitting = true,
                Points =
                [
                    new ScenarioChartPointModel { Id = s_BetaId, Path = @"Plans, old/Beta", X = 5.0, Y = 80.5 },
                    new ScenarioChartPointModel { Id = s_AlphaId, Path = @"Alpha", X = 10.0, Y = 1e-7 },
                ],
                CurveFit = new CurveFitModel
                {
                    CurveFittingType = CurveFittingType.PolynomialOrder2,
                    Coefficients = [1.5, -2.0, 0.125],
                    DerivativeCoefficients = [-2.0, 0.25],
                    RSquared = 0.9876543210123,
                },
            },
            Y2 = new ScenarioChartSeriesModel
            {
                Metric = TrackedMetrics.RisksActivityStdDevCorrection,
                CurveFittingType = CurveFittingType.Exponential,
                Points = [new ScenarioChartPointModel { Id = s_AlphaId, Path = @"Alpha", X = 10.0, Y = 0.5 }],
            },
        };

        private static string Write(Action<TextWriter> write)
        {
            using var writer = new StringWriter();
            write(writer);
            return writer.ToString();
        }

        [Fact]
        public void WritePoints_Given_TheData_Then_ARowForEachPointOfEachAxis()
        {
            Write(writer => ScenarioChartCsvWriter.WritePoints(writer, s_Data)).ShouldBe(
                "Axis,Id,Path,X,Y\n"
                + "Y1,17c3e2d9-95a4-4b47-b7ff-51f0a1b2c3d4,\"Plans, old/Beta\",5,80.5\n"
                + "Y1,8f4d2f43-4c1b-4f16-9df8-40e1a2b3c4d5,Alpha,10,1E-07\n"
                + "Y2,8f4d2f43-4c1b-4f16-9df8-40e1a2b3c4d5,Alpha,10,0.5\n");
        }

        [Fact]
        public void WriteFits_Given_TheData_Then_ARowForEachAxisWithItsFitOrNone()
        {
            // The second axis asks for a fit that the points cannot give, and so its row has no values.
            Write(writer => ScenarioChartCsvWriter.WriteFits(writer, s_Data)).ShouldBe(
                "Axis,XMetric,YMetric,CurveFitting,Absolute,RSquared,Coefficients,DerivativeCoefficients\n"
                + "Y1,NetworkDuration,DirectCost,PolynomialOrder2,true,0.9876543210123,1.5 -2 0.125,-2 0.25\n"
                + "Y2,NetworkDuration,ActivityRiskWithStandardDeviationCorrection,Exponential,false,,,\n");
        }

        [Fact]
        public void WriteFits_Given_AFitWithNoRSquared_Then_TheRSquaredIsEmpty()
        {
            ScenarioChartDataModel data = s_Data with
            {
                Y1 = s_Data.Y1! with { CurveFit = s_Data.Y1.CurveFit! with { RSquared = double.NaN } },
                Y2 = null,
            };

            Write(writer => ScenarioChartCsvWriter.WriteFits(writer, data)).Split('\n')[1]
                .ShouldBe("Y1,NetworkDuration,DirectCost,PolynomialOrder2,true,,1.5 -2 0.125,-2 0.25");
        }

        [Fact]
        public void Write_Given_NoData_Then_OnlyTheHeaders()
        {
            // A chart with no axes has no data, and the files are still there for a program that reads them.
            Write(writer => ScenarioChartCsvWriter.WritePoints(writer, null)).ShouldBe("Axis,Id,Path,X,Y\n");
            Write(writer => ScenarioChartCsvWriter.WriteFits(writer, null))
                .ShouldBe("Axis,XMetric,YMetric,CurveFitting,Absolute,RSquared,Coefficients,DerivativeCoefficients\n");
        }

        [Fact]
        public void TrackedMetricsNames_Given_EachMetric_Then_ADifferentNameInTheWordsOfTheMetricsOfTheApi()
        {
            string[] names = [.. Enum.GetValues<TrackedMetrics>().Select(TrackedMetricsNames.Get)];

            names.Distinct().Count().ShouldBe(names.Length);
            TrackedMetricsNames.Get(TrackedMetrics.None).ShouldBe(@"None");
            TrackedMetricsNames.Get(TrackedMetrics.RisksCriticality).ShouldBe(@"CriticalityRisk");
            TrackedMetricsNames.Get(TrackedMetrics.MarginsTotalAbsolute).ShouldBe(@"TotalMarginAbsolute");
            TrackedMetricsNames.Get(TrackedMetrics.EffortsEfficiency).ShouldBe(@"EffortEfficiency");
            TrackedMetricsNames.Get(TrackedMetrics.HeadcountPeakToAverageRatio).ShouldBe(@"PeakToAverageHeadcount");
        }
    }
}
