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

        private static Options Simulating(Func<Options, Options>? change = null)
        {
            var options = new Options { InputFilename = @"a.zpp", Simulate = true };
            return change is null ? options : change(options);
        }

        [Fact]
        public void ValidateOptions_Given_ASimulationWithItsOptionsWithinTheirLimits_Then_Passes()
        {
            string directory = Path.GetTempPath();

            Should.NotThrow(() => Program.ValidateOptions(Simulating()));
            Should.NotThrow(() => Program.ValidateOptions(Simulating(x => { x.SimulationIterations = 1; x.SimulationSeed = 0; x.SimulationTarget = 0; return x; })));
            Should.NotThrow(() => Program.ValidateOptions(Simulating(x =>
            {
                x.SimulationIterations = GraphLimits.MaximumSimulationIterations;
                x.SimulationSeed = ulong.MaxValue;
                x.SimulationTarget = GraphLimits.MaximumTimeValue;
                x.SimulationDistribution = DurationDistribution.ModifiedBetaPert;
                x.SimulationShape = 0.5;
                x.SimulationChartDirectory = directory;
                x.SimulationChartSize = [800, 600];
                x.SimulationCsvDirectory = directory;
                return x;
            })));
        }

        [Theory]
        [InlineData(nameof(Options.SimulationIterations))]
        [InlineData(nameof(Options.SimulationSeed))]
        [InlineData(nameof(Options.SimulationDistribution))]
        [InlineData(nameof(Options.SimulationShape))]
        [InlineData(nameof(Options.SimulationTarget))]
        [InlineData(nameof(Options.SimulationChartDirectory))]
        [InlineData(nameof(Options.SimulationChartSize))]
        [InlineData(nameof(Options.SimulationCsvDirectory))]
        public void ValidateOptions_Given_ASimulationOptionWithoutSimulate_Then_UsageExceptionNamingBoth(string property)
        {
            var options = new Options { InputFilename = @"a.zpp" };

            switch (property)
            {
                case nameof(Options.SimulationIterations): options.SimulationIterations = 100; break;
                case nameof(Options.SimulationSeed): options.SimulationSeed = 5; break;
                case nameof(Options.SimulationDistribution): options.SimulationDistribution = DurationDistribution.Normal; break;
                case nameof(Options.SimulationShape): options.SimulationShape = 5.0; break;
                case nameof(Options.SimulationTarget): options.SimulationTarget = 50; break;
                case nameof(Options.SimulationChartDirectory): options.SimulationChartDirectory = Path.GetTempPath(); break;
                case nameof(Options.SimulationChartSize): options.SimulationChartSize = [10, 10]; break;
                default: options.SimulationCsvDirectory = Path.GetTempPath(); break;
            }

            Should.Throw<UsageException>(() => Program.ValidateOptions(options)).Message.ShouldBe(
                string.Format(Resource.ProjectPlan.Messages.Message_OptionOnlyValidWithOption, Program.OptionLongName(property), @"--simulate"));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(GraphLimits.MaximumSimulationIterations + 1)]
        public void ValidateOptions_Given_IterationsOutOfRange_Then_UsageException(int iterations)
        {
            Should.Throw<UsageException>(() => Program.ValidateOptions(Simulating(x => { x.SimulationIterations = iterations; return x; })))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_OptionMustBeBetween, @"--simulation-iterations", 1, GraphLimits.MaximumSimulationIterations));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(GraphLimits.MaximumTimeValue + 1)]
        public void ValidateOptions_Given_ATargetOutOfRange_Then_UsageException(int target)
        {
            Should.Throw<UsageException>(() => Program.ValidateOptions(Simulating(x => { x.SimulationTarget = target; return x; })))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_OptionMustBeBetween, @"--simulation-target", 0, GraphLimits.MaximumTimeValue));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-3.0)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void ValidateOptions_Given_AShapeThatIsNotAboveZero_Then_UsageException(double shape)
        {
            Should.Throw<UsageException>(() => Program.ValidateOptions(Simulating(x =>
            {
                x.SimulationDistribution = DurationDistribution.ModifiedBetaPert;
                x.SimulationShape = shape;
                return x;
            }))).Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_OptionMustBeAboveZero, @"--simulation-shape"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(DurationDistribution.BetaPert)]
        [InlineData(DurationDistribution.Triangular)]
        public void ValidateOptions_Given_AShapeForADistributionThatHasNone_Then_UsageException(DurationDistribution? distribution)
        {
            Should.Throw<UsageException>(() => Program.ValidateOptions(Simulating(x =>
            {
                x.SimulationDistribution = distribution;
                x.SimulationShape = 4.0;
                return x;
            }))).Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_OptionOnlyValidWithOptionValue, @"--simulation-shape", @"--simulation-distribution", @"ModifiedBetaPert"));
        }

        [Fact]
        public void ValidateOptions_Given_ASimulationChartWithoutASizeOrWithoutItsDirectory_Then_UsageExceptionOrNothing()
        {
            Should.Throw<UsageException>(() => Program.ValidateOptions(Simulating(x => { x.SimulationChartDirectory = Path.GetTempPath(); return x; })))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_OptionRequiredWithOption, @"--simulation-chart-size", @"--simulation-chart-directory"));

            // A directory that is not there fails the run before anything is written, as the other directories do.
            Should.Throw<InvalidOperationException>(() => Program.ValidateOptions(Simulating(x => { x.SimulationCsvDirectory = Path.Combine(Path.GetTempPath(), $@"zpp-{Guid.NewGuid():N}"); return x; })));
        }

        [Fact]
        public void BuildOutputFilenames_Given_TheOutputsOfASimulation_Then_EachInItsDirectoryNamedAsTheServerNamesIt()
        {
            var options = new Options
            {
                InputFilename = @"a.zpp",
                Simulate = true,
                SimulationChartDirectory = @"charts",
                SimulationChartFormat = PlotExport.Svg,
                SimulationCsvDirectory = @"csv",
            };

            Dictionary<JobOutput, string> filenames = Program.BuildOutputFilenames(options, @"plan");

            filenames.ShouldBe(new Dictionary<JobOutput, string>
            {
                [JobOutput.SimulationChart] = Path.Combine(@"charts", @"plan-simulation.svg"),
                [JobOutput.SimulationStatistics] = Path.Combine(@"csv", @"plan-simulation-statistics.csv"),
                [JobOutput.SimulationHistogram] = Path.Combine(@"csv", @"plan-simulation-histogram.csv"),
                [JobOutput.SimulationPaths] = Path.Combine(@"csv", @"plan-simulation-paths.csv"),
                [JobOutput.SimulationActivities] = Path.Combine(@"csv", @"plan-simulation-activities.csv"),
                [JobOutput.SimulationIterations] = Path.Combine(@"csv", @"plan-simulation-iterations.csv"),
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
