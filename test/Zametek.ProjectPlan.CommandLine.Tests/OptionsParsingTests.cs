using CommandLine;
using Shouldly;
using Xunit;

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
        private static ParserResult<Options> Parse(params string[] args)
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
    }
}
