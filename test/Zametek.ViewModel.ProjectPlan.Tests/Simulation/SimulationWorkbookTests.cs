using Shouldly;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The result of a simulation as one Excel workbook: the sheets of the Excel export of a scenario, and, for a run, the sheet of its
    /// iterations. Each sheet says what the CSV file of the same data says.
    /// </summary>
    public class SimulationWorkbookTests
    {
        private static readonly DateTimeOffset s_Now = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);

        private static readonly IReadOnlyDictionary<int, string> s_Names = new Dictionary<int, string>
        {
            [10] = "A",
            [20] = "B",
            [30] = "C",
        };

        private static readonly SimulationSettings s_RunSettings = new() { Seed = 7, Distribution = DurationDistribution.Triangular };

        private static readonly SimulationSettingsModel s_Settings = new()
        {
            Iterations = 6,
            Seed = 7,
            Distribution = DurationDistribution.Triangular,
            UseRandomSeed = false,
            TargetFinishTime = 11,
        };

        // Six iterations of a plan of three activities with two paths: the third failed, and the fifth has two paths.
        private static SimulationResult<int> Result() =>
            new(
                [10, 20, 30],
                [10, 11, SimulationResult<int>.FailedFinishTime, 12, 12, 11],
                null,
                [
                    new CriticalPath<int>([new CriticalPathStep<int>(10, CriticalPathLink.None), new CriticalPathStep<int>(20, CriticalPathLink.Dependency)]),
                    new CriticalPath<int>([new CriticalPathStep<int>(10, CriticalPathLink.None), new CriticalPathStep<int>(30, CriticalPathLink.Resource)]),
                ],
                [0, 1, 2, 2, 3, 5, 6],
                [0, 1, 0, 0, 1, 1],
                [true, true, true, true, true, true]);

        private static (byte[] Workbook, SimulationSummaryModel Summary, SimulationResult<int> Result) Make(bool withIterations = true, DateTimeOffset? now = null)
        {
            SimulationResult<int> result = Result();
            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(result, s_Names, 11, 11, s_RunSettings);
            using var stream = new MemoryStream();
            SimulationWorkbook.Write(stream, s_Settings, summary, withIterations ? result : null, s_Names, now ?? s_Now);
            return (stream.ToArray(), summary, result);
        }

        // A cell that holds nothing: the package may not have it, or may have it with no value.
        private static bool IsBlank(XlsxCell? cell) => cell is null || cell.Kind == XlsxCellKind.Blank;

        private static readonly string[] s_SimulationSheets =
        [
            "Simulation Settings",
            "Simulation Statistics",
            "Simulation Percentiles",
            "Simulation Histogram",
            "Simulation Paths",
            "Simulation Criticalities",
        ];

        [Fact]
        public void Write_Given_AResultAndItsIterations_Then_HasTheSheetsOfTheCsvFilesAndOneMore()
        {
            XlsxBook book = XlsxPackageReader.Read(Make().Workbook);

            book.SheetNames.ShouldBe([.. s_SimulationSheets, "Simulation Iterations"]);
        }

        [Fact]
        public void Write_Given_AResultThatAProjectHeld_Then_HasNoSheetOfTheIterations()
        {
            XlsxBook book = XlsxPackageReader.Read(Make(withIterations: false).Workbook);

            book.SheetNames.ShouldBe(s_SimulationSheets);
        }

        [Fact]
        public void Write_Given_TheIterations_Then_TheSheetHasARowForEachAndNumbersAsNumbers()
        {
            XlsxSheet sheet = XlsxPackageReader.Read(Make().Workbook).Sheet("Simulation Iterations")!;

            sheet.Header().ShouldBe(["Iteration", "FinishTime", "CriticalPaths"]);
            sheet.LastRow.ShouldBe(7);

            sheet.Cell("A2")!.Kind.ShouldBe(XlsxCellKind.Number);
            sheet.Cell("A2")!.Number.ShouldBe(1.0);
            sheet.Cell("B2")!.Kind.ShouldBe(XlsxCellKind.Number);
            sheet.Cell("B2")!.Number.ShouldBe(10.0);
            sheet.Cell("C2")!.Text.ShouldBe("A > B");
            sheet.Cell("C3")!.Text.ShouldBe("A ~> C");

            // Two paths in one iteration are separated by a bar, as in the CSV file.
            sheet.Cell("C6")!.Text.ShouldBe("A > B | A ~> C");
        }

        [Fact]
        public void Write_Given_AnIterationThatFailed_Then_ItsRowHasNoFinishTimeAndNoPaths()
        {
            XlsxSheet sheet = XlsxPackageReader.Read(Make().Workbook).Sheet("Simulation Iterations")!;

            sheet.Cell("A4")!.Number.ShouldBe(3.0);
            IsBlank(sheet.Cell("B4")).ShouldBeTrue();
            IsBlank(sheet.Cell("C4")).ShouldBeTrue();
        }

        [Fact]
        public void Write_Given_TheIterations_Then_EachRowSaysWhatTheCsvFileSays()
        {
            (byte[] workbook, _, SimulationResult<int> result) = Make();
            using var writer = new StringWriter();
            SimulationCsvWriter.WriteIterations(writer, result, s_Names);
            string[] lines = writer.ToString().TrimEnd('\n').Split('\n');

            XlsxSheet sheet = XlsxPackageReader.Read(workbook).Sheet("Simulation Iterations")!;

            lines.Length.ShouldBe(sheet.LastRow);

            for (int row = 2; row <= sheet.LastRow; row++)
            {
                string[] fields = lines[row - 1].Split(',');
                sheet.Cell(row, 1)!.Number.ShouldBe(double.Parse(fields[0], CultureInfo.InvariantCulture));

                if (fields[1].Length == 0)
                {
                    IsBlank(sheet.Cell(row, 2)).ShouldBeTrue();
                }
                else
                {
                    sheet.Cell(row, 2)!.Number.ShouldBe(double.Parse(fields[1], CultureInfo.InvariantCulture));
                }

                (sheet.Cell(row, 3)?.Text ?? string.Empty).ShouldBe(fields[2]);
            }
        }

        [Fact]
        public void Write_Given_TheSameInputsAtTheSameTime_Then_TheWorkbooksAreTheSame()
        {
            XlsxBook first = XlsxPackageReader.Read(Make().Workbook);
            XlsxBook second = XlsxPackageReader.Read(Make().Workbook);

            XlsxBook.Difference(first.Describe(), second.Describe()).ShouldBeEmpty();
            first.Created.ShouldBe(s_Now.UtcDateTime);
        }

        [Fact]
        public void Write_Given_ASummary_Then_TheSheetsOfTheSimulationAreThoseOfTheExportOfTheScenario()
        {
            (byte[] workbook, SimulationSummaryModel summary, _) = Make(withIterations: false);
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(3) with { SimulationSettings = s_Settings, SimulationSummary = summary };
            using var stream = new MemoryStream();
            new XlsxScenarioFileExporter(new DateTimeCalculator(new FixedTimeProvider(s_Now, TimeZoneInfo.Utc)))
                .ExportProjectScenarioXlsxFile(scenario, new ResourceSeriesSetModel(), new TrackingSeriesSetModel(), showDates: false, stream);

            XlsxBook simulation = XlsxPackageReader.Read(workbook);
            XlsxBook exported = XlsxPackageReader.Read(stream.ToArray());

            foreach (string name in s_SimulationSheets)
            {
                simulation.Sheet(name)!.Describe(dateFormats: false).ShouldBe(exported.Sheet(name)!.Describe(dateFormats: false), name);
            }
        }

        [Fact]
        public void Write_Given_ASummary_Then_TheStatisticsSheetHasTheSettingsOfTheRun()
        {
            // The settings of the scenario were changed after the run, and the result says which values made it.
            XlsxSheet sheet = XlsxPackageReader.Read(Make().Workbook).Sheet("Simulation Statistics")!;

            sheet.Header().Skip(3).Take(3).ShouldBe(["Seed", "Distribution", "ModifiedBetaShape"]);
            sheet.Cell("D2")!.Text.ShouldBe("7");
            sheet.Cell("E2")!.Text.ShouldBe(nameof(DurationDistribution.Triangular));
            IsBlank(sheet.Cell("F2")).ShouldBeTrue();
        }

        [Fact]
        public void WriteIterations_Given_MoreIterationsThanARowOfExcelHas_Then_Refuses()
        {
            var result = new SimulationResult<int>([10], new int[XlsxSimulationSheets.MaximumRows], null, [], null, null, null);
            using var workbook = new XlsxWorkbookWriter();

            InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => XlsxSimulationSheets.WriteIterations(workbook, result, s_Names));

            exception.Message.ShouldBe(string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_SimulationIterationsFitNoSheet, XlsxSimulationSheets.MaximumRows, XlsxSimulationSheets.MaximumRows - 1));
        }

        [Fact]
        public void WriteIterations_Given_TheMostIterationsASheetHolds_Then_Writes()
        {
            // A million iterations is the most a run has, and a sheet holds the iterations and the row of titles with room to spare.
            XlsxSimulationSheets.MaximumRows.ShouldBeGreaterThan(GraphLimits.MaximumSimulationIterations + 1);
        }

        [Fact]
        public void Truncate_Given_TextThatACellHolds_Then_IsTheSameText()
        {
            string text = new('a', XlsxSimulationSheets.MaximumCellLength);

            XlsxSimulationSheets.Truncate(text).ShouldBe(text);
        }

        [Fact]
        public void Truncate_Given_TextThatACellDoesNotHold_Then_EndsInAnEllipsisAndFits()
        {
            string truncated = XlsxSimulationSheets.Truncate(new string('a', XlsxSimulationSheets.MaximumCellLength + 1));

            truncated.Length.ShouldBe(XlsxSimulationSheets.MaximumCellLength);
            truncated.ShouldEndWith("…");
            truncated.TrimEnd('…').ShouldBe(new string('a', XlsxSimulationSheets.MaximumCellLength - 1));
        }

        [Fact]
        public void Truncate_Given_ACharacterOfTwoUnitsAtTheCut_Then_DoesNotCutItInTheMiddle()
        {
            // The pair of units starts at the last place before the ellipsis: a cut there would leave half of it.
            string text = new string('a', XlsxSimulationSheets.MaximumCellLength - 2) + "\U0001F600" + "bbb";

            string truncated = XlsxSimulationSheets.Truncate(text);

            truncated.ShouldEndWith("…");
            char.IsHighSurrogate(truncated[^2]).ShouldBeFalse();
            truncated.Length.ShouldBeLessThanOrEqualTo(XlsxSimulationSheets.MaximumCellLength);
        }

        [Fact]
        public void Write_Given_AnIterationWithPathsThatDoNotFitACell_Then_TheCellIsCutAndTheWorkbookIsWritten()
        {
            // One activity with a name of 40,000 characters: the label of the path is longer than a cell.
            var names = new Dictionary<int, string> { [10] = new string('n', 40_000) };
            var result = new SimulationResult<int>(
                [10],
                [5],
                null,
                [new CriticalPath<int>([new CriticalPathStep<int>(10, CriticalPathLink.None)])],
                [0, 1],
                [0],
                [true]);
            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(result, names, 5, null, s_RunSettings);
            using var stream = new MemoryStream();

            SimulationWorkbook.Write(stream, s_Settings, summary, result, names, s_Now);

            XlsxSheet sheet = XlsxPackageReader.Read(stream.ToArray()).Sheet("Simulation Iterations")!;
            string cell = sheet.Cell("C2")!.Text;
            cell.Length.ShouldBe(XlsxSimulationSheets.MaximumCellLength);
            cell.ShouldEndWith("…");
        }
    }
}
