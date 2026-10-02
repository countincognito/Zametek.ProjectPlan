using CommandLine;
using Shouldly;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for a job's options as zpp serve reads them: each named after the
    /// option of zpp's it stands for, with zpp's defaults.
    /// </summary>
    public class JobOptionsTests
    {
        private static JobOptions Deserialize(string json)
        {
            return JsonSerializer.Deserialize<JobOptions>(json, JobEndpoints.JsonOptions).ShouldNotBeNull();
        }

        [Fact]
        public void Deserialize_Given_EveryOption_Then_ReadsEach()
        {
            JobOptions options = Deserialize(
                """
                {
                    "scenario": "Beta",
                    "output": true,
                    "export": true,
                    "baseTheme": "dark",
                    "metricsFormat": "json",
                    "compileTimeout": 3000,
                    "now": "2026-10-02T09:00:00+01:00",
                    "gantt": { "format": "png", "width": 800, "height": 600 },
                    "arrow": { "format": "graphml" },
                    "vertex": { "format": "dot" },
                    "resource": { "format": "svg", "width": 640, "height": 480 },
                    "ev": { "format": "webp", "width": 320, "height": 240 },
                    "scenarioChart": { "format": "bmp", "width": 100, "height": 50 }
                }
                """);

            options.ShouldBe(new JobOptions
            {
                Scenario = @"Beta",
                Output = true,
                Export = true,
                BaseTheme = BaseTheme.Dark,
                MetricsFormat = MetricsExport.Json,
                CompileTimeout = 3000,
                Now = @"2026-10-02T09:00:00+01:00",
                Gantt = new ChartOptions { Format = PlotExport.Png, Width = 800, Height = 600 },
                Arrow = new GraphOptions { Format = GraphExport.GraphML },
                Vertex = new GraphOptions { Format = GraphExport.Dot },
                Resource = new ChartOptions { Format = PlotExport.Svg, Width = 640, Height = 480 },
                EV = new ChartOptions { Format = PlotExport.Webp, Width = 320, Height = 240 },
                ScenarioChart = new ChartOptions { Format = PlotExport.Bmp, Width = 100, Height = 50 },
            });
        }

        [Fact]
        public void Deserialize_Given_NoOptions_Then_ZppsDefaults()
        {
            JobOptions options = Deserialize(@"{}");

            options.ShouldBe(new JobOptions());
            options.BaseTheme.ShouldBe(BaseTheme.Light);
            options.MetricsFormat.ShouldBe(MetricsExport.Markdown);
            options.CompileTimeout.ShouldBeNull();
        }

        [Fact]
        public void Deserialize_Given_AChartOrGraphWithoutAFormat_Then_ZppsDefaultFormat()
        {
            JobOptions options = Deserialize(@"{""gantt"":{""width"":800,""height"":600},""arrow"":{}}");

            options.Gantt.ShouldNotBeNull().Format.ShouldBe(PlotExport.Jpeg);
            options.Arrow.ShouldNotBeNull().Format.ShouldBe(GraphExport.Jpeg);
        }

        [Fact]
        public void JobOptions_Given_EveryProperty_Then_NamedAfterTheOptionOfZppsItStandsFor()
        {
            // Its JSON name, in zpp's kebab case, is one of zpp's long names - or, for a chart or a graph, the start of
            // the long names of its options.
            HashSet<string> longNames = [.. typeof(Options).GetProperties()
                .Select(x => x.GetCustomAttribute<OptionAttribute>()?.LongName)
                .OfType<string>()];

            foreach (PropertyInfo property in typeof(JobOptions).GetProperties())
            {
                string kebab = ToKebabCase(JobEndpoints.JsonOptions.PropertyNamingPolicy!.ConvertName(property.Name));

                (longNames.Contains(kebab) || longNames.Contains($@"{kebab}-directory")).ShouldBeTrue(property.Name);
            }
        }

        private static string ToKebabCase(string camelCase)
        {
            var kebab = new StringBuilder();

            foreach (char c in camelCase)
            {
                if (char.IsUpper(c))
                {
                    kebab.Append('-').Append(char.ToLowerInvariant(c));
                }
                else
                {
                    kebab.Append(c);
                }
            }

            return kebab.ToString();
        }
    }
}
