using Shouldly;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.ProjectPlan.Engine;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Unit tests for the internal Program helpers: the wording of a scenario
    /// the engine could not select (the engine's own ScenarioSelectorTests cover
    /// the selecting), export file naming, the option-combination validation that
    /// backs the usage-error exit code, and the export format mappings.
    /// </summary>
    public class ProgramHelperTests
    {
        [Fact]
        public void BuildScenarioSelectionMessage_Given_NoMatch_Then_PointsAtListScenarios()
        {
            var ex = new ScenarioSelectionException(@"No scenario matches 'Gamma'", @"Gamma", ScenarioSelectionFailure.NoMatch, 0);

            Program.BuildScenarioSelectionMessage(ex).ShouldBe(
                string.Format(Resource.ProjectPlan.Messages.Message_NoScenarioMatches, @"Gamma", @"--list-scenarios"));
        }

        [Fact]
        public void BuildScenarioSelectionMessage_Given_SeveralMatches_Then_CountsThemAndPointsAtListScenarios()
        {
            var ex = new ScenarioSelectionException(@"'Beta' matches 2 scenarios", @"Beta", ScenarioSelectionFailure.SeveralMatches, 2);

            Program.BuildScenarioSelectionMessage(ex).ShouldBe(
                string.Format(Resource.ProjectPlan.Messages.Message_SeveralScenariosMatch, @"Beta", 2, @"--list-scenarios"));
        }

        [Fact]
        public void BuildScenarioSelectionMessage_Given_NoScenarioData_Then_NamesTheScenario()
        {
            var ex = new ScenarioSelectionException(@"Scenario 'Beta' has no scenario data in the project file", @"Beta", ScenarioSelectionFailure.NoScenarioData, 1);

            Program.BuildScenarioSelectionMessage(ex).ShouldBe(
                string.Format(Resource.ProjectPlan.Messages.Message_ScenarioHasNoScenarioData, @"Beta"));
        }

        [Fact]
        public void BuildExportFilePath_Given_Format_Then_LowercasesExtension()
        {
            string expected = Path.Combine(@"exports", @"Project-gantt.png");

            Program.BuildExportFilePath(@"exports", @"Project", @"-gantt", @"Png").ShouldBe(expected);
        }

        [Fact]
        public void OptionLongName_Given_OptionProperty_Then_ReturnsAttributeLongName()
        {
            Program.OptionLongName(nameof(Options.GanttDirectory)).ShouldBe(@"--gantt-directory");
            Program.OptionLongName(nameof(Options.ListScenarios)).ShouldBe(@"--list-scenarios");
            Program.OptionLongName(nameof(Options.InputFilename)).ShouldBe(@"--input");
        }

        [Fact]
        public void OptionLongName_Given_NonOptionProperty_Then_Throws()
        {
            Should.Throw<InvalidOperationException>(
                () => Program.OptionLongName(@"NotAProperty"));
        }

        [Fact]
        public void ValidateOptions_Given_InputAndImport_Then_UsageException()
        {
            Should.Throw<Program.UsageException>(
                () => Program.ValidateOptions(new Options
                {
                    InputFilename = @"a.zpp",
                    ImportFilename = @"b.xlsx",
                }));
        }

        [Fact]
        public void ValidateOptions_Given_ScenarioWithImport_Then_UsageException()
        {
            Should.Throw<Program.UsageException>(
                () => Program.ValidateOptions(new Options
                {
                    ImportFilename = @"b.xlsx",
                    Scenario = @"Beta",
                }));
        }

        [Fact]
        public void ValidateOptions_Given_ScenarioAndListScenarios_Then_UsageException()
        {
            Should.Throw<Program.UsageException>(
                () => Program.ValidateOptions(new Options
                {
                    InputFilename = @"a.zpp",
                    Scenario = @"Beta",
                    ListScenarios = true,
                }));
        }

        [Fact]
        public void ValidateOptions_Given_DirectoryWithoutSize_Then_UsageException()
        {
            Should.Throw<Program.UsageException>(
                () => Program.ValidateOptions(new Options
                {
                    InputFilename = @"a.zpp",
                    GanttDirectory = Path.GetTempPath(),
                }));
        }

        [Fact]
        public void ValidateOptions_Given_MissingExportDirectory_Then_Throws()
        {
            string missingDirectory = Path.Combine(Path.GetTempPath(), $@"zpp-missing-{Guid.NewGuid():N}");

            Should.Throw<InvalidOperationException>(
                () => Program.ValidateOptions(new Options
                {
                    InputFilename = @"a.zpp",
                    GanttDirectory = missingDirectory,
                    GanttSize = [800, 600],
                }));
        }

        [Fact]
        public void ValidateOptions_Given_ValidCombination_Then_DoesNotThrow()
        {
            Should.NotThrow(
                () => Program.ValidateOptions(new Options
                {
                    InputFilename = @"a.zpp",
                    GanttDirectory = Path.GetTempPath(),
                    GanttSize = [800, 600],
                }));
        }

        [Fact]
        public void ValidateOptions_Given_NegativeCompileTimeout_Then_UsageException()
        {
            Should.Throw<Program.UsageException>(
                () => Program.ValidateOptions(new Options
                {
                    InputFilename = @"a.zpp",
                    CompileTimeoutMilliseconds = -1,
                }));
        }

        [Fact]
        public void ValidateOptions_Given_ZeroCompileTimeout_Then_DoesNotThrow()
        {
            // Zero means no limit, which is a legitimate choice for a batch run.
            Should.NotThrow(
                () => Program.ValidateOptions(new Options
                {
                    InputFilename = @"a.zpp",
                    CompileTimeoutMilliseconds = 0,
                }));
        }

        [Fact]
        public void ToChartImageFormat_Given_EveryPlotExport_Then_MapsToTheFormatOfTheSameName()
        {
            foreach (PlotExport format in Enum.GetValues<PlotExport>())
            {
                Program.ToChartImageFormat(format).ToString().ShouldBe(format.ToString());
            }
        }

        [Theory]
        [InlineData(GraphExport.Jpeg, GraphExportFormat.Jpeg)]
        [InlineData(GraphExport.Png, GraphExportFormat.Png)]
        [InlineData(GraphExport.Pdf, GraphExportFormat.Pdf)]
        [InlineData(GraphExport.Svg, GraphExportFormat.Svg)]
        [InlineData(GraphExport.GraphML, GraphExportFormat.GraphML)]
        [InlineData(GraphExport.Dot, GraphExportFormat.GraphViz)]
        public void ToGraphExportFormat_Given_GraphExport_Then_MapsToItsFormat(GraphExport format, GraphExportFormat expected)
        {
            Program.ToGraphExportFormat(format).ShouldBe(expected);
        }

        [Fact]
        public void ToGraphExportFormat_Given_EveryGraphExport_Then_Maps()
        {
            foreach (GraphExport format in Enum.GetValues<GraphExport>())
            {
                Should.NotThrow(() => Program.ToGraphExportFormat(format));
            }
        }
    }
}
