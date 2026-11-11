using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Text;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.Engine.ProjectPlan.Tests
{
    /// <summary>
    /// The data of the scenario chart. The plan has two tracked scenarios: Alpha, a task of 5 days, and Beta, a task of 10
    /// days. Its chart plots the duration against itself on the first axis of Y, with a linear fit, and the duration in man
    /// months on the second, with a fit of the second order, which two points cannot give. A job that compiles the tracked
    /// scenarios gives the data in its result, and the chart writes it as two CSV files after the image.
    /// </summary>
    public class JobRunnerScenarioChartTests
    {
        private static readonly DateTimeOffset s_Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

        private static readonly Guid s_AlphaId = Guid.Parse(@"8f4d2f43-4c1b-4f16-9df8-40e1a2b3c4d5");
        private static readonly Guid s_BetaId = Guid.Parse(@"17c3e2d9-95a4-4b47-b7ff-51f0a1b2c3d4");

        public JobRunnerScenarioChartTests()
        {
            ProjectPlanEngine.Initialize();
        }

        private static byte[] Plan(string name = @"scenario-chart.zpp")
        {
            return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, @"Assets", name));
        }

        private static async Task<(JobResult Result, MemoryJobSink Sink)> RunAsync(
            byte[] plan,
            Func<Stream, JobRequest> buildRequest)
        {
            IServiceCollection services = new ServiceCollection()
                .AddProjectPlanEngine()
                .AddSingleton<TimeProvider>(new FixedTimeProvider(s_Now));
            await using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var sink = new MemoryJobSink();
            using var input = new MemoryStream(plan);
            JobResult result = await provider.GetRequiredService<JobRunner>().RunAsync(buildRequest(input), sink);
            return (result, sink);
        }

        private static string[] Lines(byte[] content)
        {
            return Encoding.UTF8.GetString(content.AsSpan(3)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }

        [Fact]
        public async Task RunAsync_Given_CompileTracked_Then_TheResultHasTheDataOfTheChart()
        {
            (JobResult result, MemoryJobSink sink) = await RunAsync(Plan(), input => new JobRequest { Input = input, CompileTracked = true });

            result.Status.ShouldBe(JobStatus.Succeeded);
            sink.Outputs.ShouldBeEmpty();

            ScenarioChartDataModel data = result.ScenarioChart.ShouldNotBeNull();
            data.XMetric.ShouldBe(TrackedMetrics.NetworkDuration);

            ScenarioChartSeriesModel y1 = data.Y1.ShouldNotBeNull();
            y1.Metric.ShouldBe(TrackedMetrics.NetworkDuration);
            y1.Points.Select(x => (x.Id, x.Path, x.X, x.Y)).ShouldBe([(s_AlphaId, @"Alpha", 5.0, 5.0), (s_BetaId, @"Beta", 10.0, 10.0)]);

            // The line y = x.
            CurveFitModel fit = y1.CurveFit.ShouldNotBeNull();
            fit.CurveFittingType.ShouldBe(CurveFittingType.Linear);
            fit.Coefficients[0].ShouldBe(0.0, 1e-9);
            fit.Coefficients[1].ShouldBe(1.0, 1e-9);
            fit.DerivativeCoefficients[0].ShouldBe(1.0, 1e-9);
            fit.RSquared.ShouldBe(1.0, 1e-9);

            // The values of the second axis are the metrics of the scenarios that the job compiled. Two points cannot give a fit
            // of the second order.
            ScenarioChartSeriesModel y2 = data.Y2.ShouldNotBeNull();
            y2.Metric.ShouldBe(TrackedMetrics.NetworkDurationManMonths);
            y2.CurveFittingType.ShouldBe(CurveFittingType.PolynomialOrder2);
            y2.Points.Select(x => x.Y).ShouldBe(result.TrackedScenarios.ShouldNotBeNull().Select(x => x.Metrics!.NetworkDurationManMonths!.Value));
            y2.CurveFit.ShouldBeNull();
        }

        [Fact]
        public async Task RunAsync_Given_TheScenarioChart_Then_ItsPointsAndItsFitsFollowTheImageAsCsv()
        {
            (JobResult result, MemoryJobSink sink) = await RunAsync(
                Plan(),
                input => new JobRequest { Input = input, ScenarioChart = new ChartOutputRequest(ChartImageFormat.Svg, 400, 300) });

            result.Status.ShouldBe(JobStatus.Succeeded);
            sink.Outputs.Select(x => x.Output).ShouldBe([JobOutput.ScenarioChart, JobOutput.ScenarioChartPoints, JobOutput.ScenarioChartFits]);

            // The chart compiles the tracked scenarios, and so the result has the data also without CompileTracked.
            result.ScenarioChart.ShouldNotBeNull().Y1.ShouldNotBeNull().Points.Count.ShouldBe(2);

            // Each is UTF-8 with a byte order mark, as the other CSV files are.
            byte[] points = sink[JobOutput.ScenarioChartPoints];
            byte[] fits = sink[JobOutput.ScenarioChartFits];
            points.Take(3).ShouldBe([(byte)0xEF, (byte)0xBB, (byte)0xBF]);
            fits.Take(3).ShouldBe([(byte)0xEF, (byte)0xBB, (byte)0xBF]);

            string[] pointLines = Lines(points);
            pointLines[0].ShouldBe(@"Axis,Id,Path,X,Y");
            pointLines[1].ShouldBe($@"Y1,{s_AlphaId},Alpha,5,5");
            pointLines[2].ShouldBe($@"Y1,{s_BetaId},Beta,10,10");
            pointLines.Skip(3).Select(x => x.Split(',')[0]).ShouldBe([@"Y2", @"Y2"]);

            string[] fitLines = Lines(fits);
            fitLines.Length.ShouldBe(3);
            fitLines[1].ShouldStartWith(@"Y1,NetworkDuration,NetworkDuration,Linear,false,");
            fitLines[2].ShouldBe(@"Y2,NetworkDuration,NetworkDurationManMonths,PolynomialOrder2,false,,,");
        }

        [Fact]
        public async Task RunAsync_Given_NeitherCompileTrackedNorTheChart_Then_NoDataOfTheChart()
        {
            (JobResult result, _) = await RunAsync(Plan(), input => new JobRequest { Input = input });

            result.Status.ShouldBe(JobStatus.Succeeded);
            result.ScenarioChart.ShouldBeNull();
        }

        [Fact]
        public async Task RunAsync_Given_AChartWithNoAxes_Then_NoDataAndFilesWithOnlyTheirHeaders()
        {
            (JobResult result, MemoryJobSink sink) = await RunAsync(
                Plan(@"stale-tracked.zpp"),
                input => new JobRequest { Input = input, CompileTracked = true, ScenarioChart = new ChartOutputRequest(ChartImageFormat.Svg, 400, 300) });

            result.Status.ShouldBe(JobStatus.Succeeded);
            result.ScenarioChart.ShouldBeNull();
            Lines(sink[JobOutput.ScenarioChartPoints]).ShouldBe([@"Axis,Id,Path,X,Y"]);
            Lines(sink[JobOutput.ScenarioChartFits]).ShouldBe([@"Axis,XMetric,YMetric,CurveFitting,Absolute,RSquared,Coefficients,DerivativeCoefficients"]);
        }

        private sealed class FixedTimeProvider(DateTimeOffset now)
            : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }
    }
}
