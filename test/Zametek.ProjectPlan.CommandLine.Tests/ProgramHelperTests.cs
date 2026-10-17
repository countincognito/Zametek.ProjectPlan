using Shouldly;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Unit tests for the internal Program helpers: export file naming, the
    /// option-combination validation that backs the usage-error exit code, and
    /// the export format mappings.
    /// </summary>
    public class ProgramHelperTests
    {
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

        // The options of a run that has a simulation to do: the settings are the project's, and so the options hold only
        // whether to run it, and where the chart and the CSV files go.
        private static Options Simulating(Func<Options, Options>? change = null)
        {
            var options = new Options { InputFilename = @"a.zpp", RunSimulation = true };
            return change is null ? options : change(options);
        }

        [Fact]
        public void ValidateOptions_Given_ARunOfTheSimulationWithItsOutputs_Then_Passes()
        {
            string directory = Path.GetTempPath();

            Should.NotThrow(() => Program.ValidateOptions(Simulating()));
            Should.NotThrow(() => Program.ValidateOptions(Simulating(x =>
            {
                x.SimulationDirectory = directory;
                x.SimulationFormat = PlotExport.Png;
                x.SimulationSize = [800, 600];
                return x;
            })));
        }

        [Fact]
        public void ValidateOptions_Given_TheOutputsOfASimulationAndNoRun_Then_Passes()
        {
            // A project that holds a result gives its statistics and its outputs without a run, and whether it holds one is
            // known only when the project is read. The job says so then (see SimulationFailure.NoResult).
            Should.NotThrow(() => Program.ValidateOptions(new Options
            {
                InputFilename = @"a.zpp",
                SimulationDirectory = Path.GetTempPath(),
                SimulationSize = [800, 600],
            }));
        }

        [Fact]
        public void ValidateOptions_Given_ASimulationDirectoryWithoutASize_Then_UsageException()
        {
            Should.Throw<UsageException>(() => Program.ValidateOptions(Simulating(x => { x.SimulationDirectory = Path.GetTempPath(); return x; })))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_OptionRequiredWithOption, @"--simulation-size", @"--simulation-directory"));
        }

        [Fact]
        public void ValidateOptions_Given_ASimulationDirectoryThatIsNotThere_Then_TheRunFailsBeforeAnythingIsWritten()
        {
            // As the other directories do.
            Should.Throw<InvalidOperationException>(() => Program.ValidateOptions(Simulating(x =>
            {
                x.SimulationDirectory = Path.Combine(Path.GetTempPath(), $@"zpp-{Guid.NewGuid():N}");
                x.SimulationSize = [800, 600];
                return x;
            })));
        }

        [Fact]
        public void BuildOutputFilenames_Given_TheOutputsOfASimulation_Then_EachInItsDirectoryNamedAsTheServerNamesIt()
        {
            var options = new Options
            {
                InputFilename = @"a.zpp",
                RunSimulation = true,
                SimulationDirectory = @"simulation",
                SimulationFormat = PlotExport.Svg,
            };

            Dictionary<JobOutput, string> filenames = Program.BuildOutputFilenames(options, @"plan");

            // The chart and the CSV files side by side.
            filenames.ShouldBe(new Dictionary<JobOutput, string>
            {
                [JobOutput.SimulationChart] = Path.Combine(@"simulation", @"plan-simulation.svg"),
                [JobOutput.SimulationStatistics] = Path.Combine(@"simulation", @"plan-simulation-statistics.csv"),
                [JobOutput.SimulationHistogram] = Path.Combine(@"simulation", @"plan-simulation-histogram.csv"),
                [JobOutput.SimulationPaths] = Path.Combine(@"simulation", @"plan-simulation-paths.csv"),
                [JobOutput.SimulationActivities] = Path.Combine(@"simulation", @"plan-simulation-activities.csv"),
                [JobOutput.SimulationIterations] = Path.Combine(@"simulation", @"plan-simulation-iterations.csv"),
            });
        }

        [Fact]
        public void ValidateOptions_Given_InputAndImport_Then_UsageException()
        {
            Should.Throw<UsageException>(
                () => Program.ValidateOptions(new Options
                {
                    InputFilename = @"a.zpp",
                    ImportFilename = @"b.xlsx",
                }));
        }

        [Fact]
        public void ValidateOptions_Given_ScenarioWithImport_Then_UsageException()
        {
            Should.Throw<UsageException>(
                () => Program.ValidateOptions(new Options
                {
                    ImportFilename = @"b.xlsx",
                    Scenario = @"Beta",
                }));
        }

        [Fact]
        public void ValidateOptions_Given_ScenarioAndListScenarios_Then_UsageException()
        {
            Should.Throw<UsageException>(
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
            Should.Throw<UsageException>(
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
            Should.Throw<UsageException>(
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

        [Theory]
        [InlineData(@"2026-09-27T12:00:00+01:00", 2026, 9, 27, 12, 0, 0, 0, 60)]
        [InlineData(@"2026-09-27T11:00:00Z", 2026, 9, 27, 11, 0, 0, 0, 0)]
        [InlineData(@"2026-09-27T06:30:00-04:30", 2026, 9, 27, 6, 30, 0, 0, -270)]
        [InlineData(@"2026-09-27T12:00:00.25+01:00", 2026, 9, 27, 12, 0, 0, 250, 60)]
        [InlineData(@"2026-09-27T11:00:00.5Z", 2026, 9, 27, 11, 0, 0, 500, 0)]
        public void TryParseNow_Given_ATimeWithItsOffset_Then_ParsesIt(
            string now,
            int year, int month, int day, int hour, int minute, int second, int millisecond, int offsetMinutes)
        {
            Program.TryParseNow(now, out DateTimeOffset result).ShouldBeTrue();

            result.ShouldBe(new DateTimeOffset(year, month, day, hour, minute, second, millisecond, TimeSpan.FromMinutes(offsetMinutes)));
            result.Offset.ShouldBe(TimeSpan.FromMinutes(offsetMinutes));
        }

        [Theory]
        [InlineData(@"2026-09-27T12:00:00")]
        [InlineData(@"2026-09-27")]
        [InlineData(@"2026-09-27 12:00:00+01:00")]
        [InlineData(@"27/09/2026 12:00:00 +01:00")]
        [InlineData(@"2026-09-27T12:00+01:00")]
        [InlineData(@"now")]
        [InlineData(@"")]
        public void TryParseNow_Given_AnythingElse_Then_Refuses(string now)
        {
            // Without an offset in particular: the same time would be a different instant on each machine.
            Program.TryParseNow(now, out _).ShouldBeFalse();
        }

        [Fact]
        public void ValidateOptions_Given_NowWithoutAnOffset_Then_UsageException()
        {
            Should.Throw<UsageException>(
                () => Program.ValidateOptions(new Options
                {
                    InputFilename = @"a.zpp",
                    Now = @"2026-09-27T12:00:00",
                }))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_OptionMustBeADateTimeWithOffset, @"--now"));
        }

        [Fact]
        public void ValidateOptions_Given_NowWithAnOffset_Then_DoesNotThrow()
        {
            Should.NotThrow(
                () => Program.ValidateOptions(new Options
                {
                    InputFilename = @"a.zpp",
                    Now = @"2026-09-27T12:00:00+01:00",
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
