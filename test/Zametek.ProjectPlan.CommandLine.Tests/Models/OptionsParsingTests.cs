using CommandLine;
using Shouldly;
using System.Reflection;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for the Options class as seen through a parser configured the same
    /// way Program configures its own: case-insensitive enums, no auto version.
    /// These pin the option surface - names, size separators, enum values and
    /// the at-least-one-input group - that scripts depend on.
    /// </summary>
    public class OptionsParsingTests
    {
        // ArgumentsHelperTests parses with it too.
        internal static ParserResult<Options> Parse(params string[] args)
        {
            using var parser = new Parser(with =>
            {
                with.CaseInsensitiveEnumValues = true;
                with.HelpWriter = null;
                with.AutoVersion = false;
            });

            return parser.ParseArguments<Options>(args);
        }

        private static Options ParsedValue(params string[] args)
        {
            Parsed<Options> parsed = Parse(args).ShouldBeOfType<Parsed<Options>>();
            return parsed.Value;
        }

        [Fact]
        public void Parse_Given_NoSimulationOptions_Then_NoSimulationAndNothingForItToDefaultTo()
        {
            Options options = ParsedValue(@"-i", @"a.zpp");

            options.Simulate.ShouldBeFalse();
            options.SimulationIterations.ShouldBeNull();
            options.SimulationSeed.ShouldBeNull();
            options.SimulationDistribution.ShouldBeNull();
            options.SimulationShape.ShouldBeNull();
            options.SimulationTarget.ShouldBeNull();
            options.SimulationChartDirectory.ShouldBeNull();
            options.SimulationChartFormat.ShouldBe(PlotExport.Jpeg);
            options.SimulationChartSize.ShouldBeEmpty();
            options.SimulationCsvDirectory.ShouldBeNull();
        }

        [Fact]
        public void Parse_Given_EverySimulationOption_Then_Each()
        {
            Options options = ParsedValue(
                @"-i", @"a.zpp",
                @"--simulate",
                @"--simulation-iterations", @"20000",
                @"--simulation-seed", @"18446744073709551615",
                @"--simulation-distribution", @"modifiedbetapert",
                @"--simulation-shape", @"6.5",
                @"--simulation-target", @"120",
                @"--simulation-chart-directory", @"charts",
                @"--simulation-chart-format", @"png",
                @"--simulation-chart-size", @"800:600",
                @"--simulation-csv-directory", @"csv");

            options.Simulate.ShouldBeTrue();
            options.SimulationIterations.ShouldBe(20_000);
            options.SimulationSeed.ShouldBe(ulong.MaxValue);
            options.SimulationDistribution.ShouldBe(DurationDistribution.ModifiedBetaPert);
            options.SimulationShape.ShouldBe(6.5);
            options.SimulationTarget.ShouldBe(120);
            options.SimulationChartDirectory.ShouldBe(@"charts");
            options.SimulationChartFormat.ShouldBe(PlotExport.Png);
            options.SimulationChartSize.ShouldBe([800, 600]);
            options.SimulationCsvDirectory.ShouldBe(@"csv");
        }

        [Theory]
        [InlineData(@"BetaPert", DurationDistribution.BetaPert)]
        [InlineData(@"modifiedBetaPert", DurationDistribution.ModifiedBetaPert)]
        [InlineData(@"TRIANGULAR", DurationDistribution.Triangular)]
        [InlineData(@"normal", DurationDistribution.Normal)]
        [InlineData(@"Uniform", DurationDistribution.Uniform)]
        public void Parse_Given_ADistributionInAnyCase_Then_Parses(string value, DurationDistribution expected)
        {
            ParsedValue(@"-i", @"a.zpp", @"--simulate", @"--simulation-distribution", value).SimulationDistribution.ShouldBe(expected);
        }

        [Theory]
        [InlineData(@"--simulation-iterations", @"many")]
        [InlineData(@"--simulation-iterations", @"1.5")]
        [InlineData(@"--simulation-seed", @"-1")]
        [InlineData(@"--simulation-seed", @"18446744073709551616")]
        [InlineData(@"--simulation-seed", @"x")]
        [InlineData(@"--simulation-distribution", @"gamma")]
        [InlineData(@"--simulation-shape", @"wide")]
        [InlineData(@"--simulation-target", @"soon")]
        [InlineData(@"--simulation-chart-size", @"800")]
        public void Parse_Given_ASimulationOptionWithAValueItDoesNotTake_Then_Fails(string option, string value)
        {
            Parse(@"-i", @"a.zpp", @"--simulate", option, value).ShouldBeOfType<NotParsed<Options>>();
        }

        [Fact]
        public void Parse_Given_InputAndScenario_Then_Populated()
        {
            Options options = ParsedValue(@"-i", @"a.zpp", @"-s", @"Beta");

            options.InputFilename.ShouldBe(@"a.zpp");
            options.Scenario.ShouldBe(@"Beta");
        }

        [Fact]
        public void Parse_Given_SizeWithSeparator_Then_TwoValues()
        {
            Options options = ParsedValue(@"-i", @"a.zpp", @"--gantt-size", @"800:600");

            options.GanttSize.ShouldBe([800, 600]);
        }

        [Fact]
        public void Parse_Given_LowercaseEnumValue_Then_Parses()
        {
            Options options = ParsedValue(@"-i", @"a.zpp", @"--gantt-format", @"png");

            options.GanttFormat.ShouldBe(PlotExport.Png);
        }

        [Fact]
        public void Parse_Given_MetricsFormatJson_Then_Parses()
        {
            Options options = ParsedValue(@"-i", @"a.zpp", @"--metrics-format", @"json");

            options.MetricsFormat.ShouldBe(MetricsExport.Json);
        }

        [Fact]
        public void Parse_Given_NoFormatOptions_Then_DefaultsApply()
        {
            Options options = ParsedValue(@"-i", @"a.zpp");

            options.MetricsFormat.ShouldBe(MetricsExport.Markdown);
            options.GanttFormat.ShouldBe(PlotExport.Jpeg);
            options.Verbose.ShouldBeFalse();
            options.ListScenarios.ShouldBeFalse();
            options.CompileTimeoutMilliseconds.ShouldBe(AppSettingsModel.DefaultCompilationTimeoutMilliseconds);
        }

        [Fact]
        public void Parse_Given_CompileTimeout_Then_Parses()
        {
            Options options = ParsedValue(@"-i", @"a.zpp", @"--compile-timeout", @"30000");

            options.CompileTimeoutMilliseconds.ShouldBe(30_000);
        }

        [Fact]
        public void Parse_Given_CompileTimeoutZero_Then_Parses()
        {
            // Zero is the documented way to switch the limit off, so it must survive
            // parsing rather than fall back to the default.
            Options options = ParsedValue(@"-i", @"a.zpp", @"--compile-timeout", @"0");

            options.CompileTimeoutMilliseconds.ShouldBe(0);
        }

        [Fact]
        public void Parse_Given_VerboseShortName_Then_True()
        {
            Options options = ParsedValue(@"-i", @"a.zpp", @"-v");

            options.Verbose.ShouldBeTrue();
        }

        [Fact]
        public void Parse_Given_ListScenariosShortName_Then_True()
        {
            Options options = ParsedValue(@"-i", @"a.zpp", @"-l");

            options.ListScenarios.ShouldBeTrue();
        }

        [Fact]
        public void Parse_Given_NoInputOrImport_Then_Fails()
        {
            Parse(@"--gantt-size", @"800:600").ShouldBeOfType<NotParsed<Options>>();
        }

        [Fact]
        public void Parse_Given_SingleSizeValue_Then_Fails()
        {
            Parse(@"-i", @"a.zpp", @"--gantt-size", @"800").ShouldBeOfType<NotParsed<Options>>();
        }

        [Fact]
        public void Parse_Given_UnknownOption_Then_Fails()
        {
            Parse(@"-i", @"a.zpp", @"--nonsense").ShouldBeOfType<NotParsed<Options>>();
        }

        [Fact]
        public void Parse_Given_ServerLocalAndApiKeyFile_Then_Populated()
        {
            Options options = ParsedValue(@"-i", @"a.zpp", @"--server", @"http://localhost:9770", @"--local", @"--api-key-file", @"api-key");

            options.Server.ShouldBe(@"http://localhost:9770");
            options.Local.ShouldBeTrue();
            options.ApiKeyFile.ShouldBe(@"api-key");
        }

        [Fact]
        public void Parse_Given_NoServerOptions_Then_NoneSet()
        {
            Options options = ParsedValue(@"-i", @"a.zpp");

            options.Server.ShouldBeNull();
            options.Local.ShouldBeFalse();
            options.ApiKeyFile.ShouldBeNull();
        }

        [Fact]
        public void Options_Given_EveryOptionProperty_Then_HasLongName()
        {
            // Program.OptionLongName resolves message text from these attributes,
            // so every option must carry a long name. The options are the
            // instance properties, which the parser sets - Usage, which is
            // static, is the help's text.
            foreach (PropertyInfo property in typeof(Options).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                OptionAttribute? attribute = property.GetCustomAttribute<OptionAttribute>();

                attribute.ShouldNotBeNull();
                attribute.LongName.ShouldNotBeNullOrWhiteSpace($@"{property.Name} has no long option name");
            }
        }
    }
}
