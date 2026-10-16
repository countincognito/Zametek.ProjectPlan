using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Globalization;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The sheets of the schedule simulation in an Excel workbook: the settings of a run, and the result of the last one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A workbook has the sheets only for a scenario that has something to say. A scenario that has the default settings
    /// and no result gives the sheets that it always gave, and thus a workbook that was made before the simulation
    /// exists is the workbook that is made now. The settings are one row. The result is a row of numbers (the statistics),
    /// and a sheet for each list in it (the percentiles, the histogram, the critical paths and the criticalities).
    /// The iterations of a run are not in the workbook, as they are not in a project file.
    /// </para>
    /// <para>
    /// The values are written as what they are, and read as what they are: a number is a number cell, and so a reader
    /// does not depend on the culture of the person who made the workbook. The seed is the exception. It is 64 bits wide,
    /// a cell holds only 53 bits of a whole number, and thus the seed is text.
    /// </para>
    /// </remarks>
    internal static class XlsxSimulationSheets
    {
        // The histogram has one column for the weight of each group of paths: PathGroupWeight1, PathGroupWeight2, and so on.
        private const string c_PathGroupWeightColumnPrefix = @"PathGroupWeight";

        private static readonly IReadOnlyList<string> s_SettingsColumnTitles =
            [
                nameof(SimulationSettingsModel.Iterations),
                nameof(SimulationSettingsModel.Distribution),
                nameof(SimulationSettingsModel.ModifiedBetaShape),
                nameof(SimulationSettingsModel.Seed),
                nameof(SimulationSettingsModel.UseRandomSeed),
                nameof(SimulationSettingsModel.TargetFinishTime),
            ];

        private static readonly IReadOnlyList<string> s_StatisticsColumnTitles =
            [
                nameof(SimulationSummaryModel.IterationCount),
                nameof(SimulationSummaryModel.ValidIterationCount),
                nameof(SimulationSummaryModel.FailedIterationCount),
                nameof(SimulationSummaryModel.PlannedFinishTime),
                nameof(SimulationSummaryModel.TargetFinishTime),
                nameof(SimulationSummaryModel.TargetProbability),
                nameof(SimulationSummaryModel.Mean),
                nameof(SimulationSummaryModel.StandardDeviation),
                nameof(SimulationSummaryModel.Minimum),
                nameof(SimulationSummaryModel.Maximum),
                nameof(SimulationSummaryModel.BinWidth),
                nameof(SimulationSummaryModel.ChartPathCount),
                nameof(SimulationSummaryModel.OtherPathsWeight),
            ];

        private static readonly IReadOnlyList<string> s_PercentileColumnTitles =
            [
                nameof(SimulationPercentileModel.Percentile),
                nameof(SimulationPercentileModel.FinishTime),
            ];

        private static readonly IReadOnlyList<string> s_HistogramColumnTitles =
            [
                nameof(SimulationHistogramBinModel.FirstFinishTime),
                nameof(SimulationHistogramBinModel.LastFinishTime),
                nameof(SimulationHistogramBinModel.Count),
                nameof(SimulationHistogramBinModel.CumulativeShare),
            ];

        private static readonly IReadOnlyList<string> s_PathColumnTitles =
            [
                nameof(SimulationPathModel.Rank),
                nameof(SimulationPathModel.Label),
                nameof(SimulationPathModel.ShortLabel),
                nameof(SimulationPathModel.IdLabel),
                nameof(SimulationPathModel.HasResourceLinks),
                nameof(SimulationPathModel.Weight),
                nameof(SimulationPathModel.Share),
            ];

        private static readonly IReadOnlyList<string> s_CriticalityColumnTitles =
            [
                nameof(SimulationActivityCriticalityModel.ActivityId),
                nameof(SimulationActivityCriticalityModel.Name),
                nameof(SimulationActivityCriticalityModel.CriticalIterationCount),
                nameof(SimulationActivityCriticalityModel.CriticalityIndex),
            ];

        #region Writing

        /// <summary>
        /// Whether the scenario has something to write: settings that are not the defaults, or a result.
        /// </summary>
        public static bool HasSimulation(ProjectScenarioModel projectScenario)
        {
            ArgumentNullException.ThrowIfNull(projectScenario);
            return projectScenario.SimulationSummary is not null
                || projectScenario.SimulationSettings != new SimulationSettingsModel();
        }

        public static void WriteToWorkbook(
            ProjectScenarioModel projectScenario,
            XSSFWorkbook workbook,
            ICellStyle titleStyle)
        {
            ArgumentNullException.ThrowIfNull(projectScenario);
            ArgumentNullException.ThrowIfNull(workbook);
            ArgumentNullException.ThrowIfNull(titleStyle);

            SimulationSettingsModel settings = projectScenario.SimulationSettings;

            WriteSheet(
                workbook,
                titleStyle,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationSettings,
                s_SettingsColumnTitles,
                [[settings.Iterations, settings.Distribution, settings.ModifiedBetaShape, settings.Seed, settings.UseRandomSeed, settings.TargetFinishTime]]);

            SimulationSummaryModel? summary = projectScenario.SimulationSummary;

            if (summary is null)
            {
                return;
            }

            WriteSheet(
                workbook,
                titleStyle,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationStatistics,
                s_StatisticsColumnTitles,
                [
                    [
                        summary.IterationCount,
                        summary.ValidIterationCount,
                        summary.FailedIterationCount,
                        summary.PlannedFinishTime,
                        summary.TargetFinishTime,
                        summary.TargetProbability,
                        summary.Mean,
                        summary.StandardDeviation,
                        summary.Minimum,
                        summary.Maximum,
                        summary.BinWidth,
                        summary.ChartPathCount,
                        summary.OtherPathsWeight,
                    ],
                ]);

            WriteSheet(
                workbook,
                titleStyle,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationPercentiles,
                s_PercentileColumnTitles,
                [.. summary.Percentiles.Select(x => new object?[] { x.Percentile, x.FinishTime })]);

            int groupCount = summary.Bins.Select(x => x.PathGroupWeights.Count).DefaultIfEmpty().Max();

            WriteSheet(
                workbook,
                titleStyle,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationHistogram,
                [.. s_HistogramColumnTitles, .. Enumerable.Range(1, groupCount).Select(x => $@"{c_PathGroupWeightColumnPrefix}{x}")],
                [
                    .. summary.Bins.Select(x => (object?[])
                    [
                        x.FirstFinishTime,
                        x.LastFinishTime,
                        x.Count,
                        x.CumulativeShare,
                        .. x.PathGroupWeights.Select(y => (object?)y),
                    ]),
                ]);

            WriteSheet(
                workbook,
                titleStyle,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationPaths,
                s_PathColumnTitles,
                [.. summary.Paths.Select(x => new object?[] { x.Rank, x.Label, x.ShortLabel, x.IdLabel, x.HasResourceLinks, x.Weight, x.Share })]);

            WriteSheet(
                workbook,
                titleStyle,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationCriticalities,
                s_CriticalityColumnTitles,
                [.. summary.Activities.Select(x => new object?[] { x.ActivityId, x.Name, x.CriticalIterationCount, x.CriticalityIndex })]);
        }

        private static void WriteSheet(
            XSSFWorkbook workbook,
            ICellStyle titleStyle,
            string sheetTitle,
            IReadOnlyList<string> columnTitles,
            IReadOnlyList<object?[]> rows)
        {
            ISheet sheet = workbook.CreateSheet(sheetTitle);
            IRow titleRow = sheet.CreateRow(0);

            for (int columnIndex = 0; columnIndex < columnTitles.Count; columnIndex++)
            {
                ICell cell = titleRow.CreateCell(columnIndex);
                cell.CellStyle = titleStyle;
                cell.SetCellValue(columnTitles[columnIndex]);
            }

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                IRow row = sheet.CreateRow(rowIndex + 1);
                object?[] values = rows[rowIndex];

                for (int columnIndex = 0; columnIndex < values.Length; columnIndex++)
                {
                    SetCellValue(row.CreateCell(columnIndex), values[columnIndex]);
                }
            }
        }

        // A value that is not there is a blank cell. The seed is text, as the remarks of the class say.
        private static void SetCellValue(
            ICell cell,
            object? value)
        {
            switch (value)
            {
                case null:
                    break;
                case string text:
                    cell.SetCellValue(text);
                    break;
                case bool flag:
                    cell.SetCellValue(flag);
                    break;
                case int number:
                    cell.SetCellValue((double)number);
                    break;
                case double number:
                    cell.SetCellValue(number);
                    break;
                case ulong seed:
                    cell.SetCellValue(seed.ToString(CultureInfo.InvariantCulture));
                    break;
                case Enum choice:
                    cell.SetCellValue(choice.ToString());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        #endregion

        #region Reading

        /// <summary>
        /// Reads what the workbook holds of the simulation. A sheet that is not there, a column that is not there, and a
        /// cell that is not a value that it can read all give the default: the settings are the defaults and the result is
        /// none. A workbook that has the settings and not the statistics has no result, because a result is more than the
        /// lists.
        /// </summary>
        public static (SimulationSettingsModel Settings, SimulationSummaryModel? Summary) ReadFromWorkbook(XSSFWorkbook? workbook)
        {
            return workbook is null
                ? (new SimulationSettingsModel(), null)
                : (ReadSettings(workbook), ReadSummary(workbook));
        }

        private static SimulationSettingsModel ReadSettings(XSSFWorkbook workbook)
        {
            var settings = new SimulationSettingsModel();
            SheetTable? table = SheetTable.Open(workbook, Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationSettings);
            SheetRow? row = table?.Rows.FirstOrDefault();

            if (row is null)
            {
                return settings;
            }

            return settings with
            {
                Iterations = row.Int(nameof(SimulationSettingsModel.Iterations)) ?? settings.Iterations,
                Distribution = row.Enum<DurationDistribution>(nameof(SimulationSettingsModel.Distribution)) ?? settings.Distribution,
                ModifiedBetaShape = row.Double(nameof(SimulationSettingsModel.ModifiedBetaShape)) ?? settings.ModifiedBetaShape,
                Seed = row.ULong(nameof(SimulationSettingsModel.Seed)) ?? settings.Seed,
                UseRandomSeed = row.Bool(nameof(SimulationSettingsModel.UseRandomSeed)) ?? settings.UseRandomSeed,
                TargetFinishTime = row.Int(nameof(SimulationSettingsModel.TargetFinishTime)),
            };
        }

        private static SimulationSummaryModel? ReadSummary(XSSFWorkbook workbook)
        {
            SheetTable? statistics = SheetTable.Open(workbook, Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationStatistics);
            SheetRow? row = statistics?.Rows.FirstOrDefault();

            if (row is null)
            {
                return null;
            }

            var summary = new SimulationSummaryModel();

            summary = summary with
            {
                IterationCount = row.Int(nameof(SimulationSummaryModel.IterationCount)) ?? summary.IterationCount,
                ValidIterationCount = row.Int(nameof(SimulationSummaryModel.ValidIterationCount)) ?? summary.ValidIterationCount,
                FailedIterationCount = row.Int(nameof(SimulationSummaryModel.FailedIterationCount)) ?? summary.FailedIterationCount,
                PlannedFinishTime = row.Int(nameof(SimulationSummaryModel.PlannedFinishTime)) ?? summary.PlannedFinishTime,
                TargetFinishTime = row.Int(nameof(SimulationSummaryModel.TargetFinishTime)),
                TargetProbability = row.Double(nameof(SimulationSummaryModel.TargetProbability)),
                Mean = row.Double(nameof(SimulationSummaryModel.Mean)) ?? summary.Mean,
                StandardDeviation = row.Double(nameof(SimulationSummaryModel.StandardDeviation)) ?? summary.StandardDeviation,
                Minimum = row.Int(nameof(SimulationSummaryModel.Minimum)) ?? summary.Minimum,
                Maximum = row.Int(nameof(SimulationSummaryModel.Maximum)) ?? summary.Maximum,
                BinWidth = row.Int(nameof(SimulationSummaryModel.BinWidth)) ?? summary.BinWidth,
                ChartPathCount = row.Int(nameof(SimulationSummaryModel.ChartPathCount)) ?? summary.ChartPathCount,
                OtherPathsWeight = row.Double(nameof(SimulationSummaryModel.OtherPathsWeight)) ?? summary.OtherPathsWeight,
            };

            return summary with
            {
                Percentiles =
                [
                    .. ReadRows(workbook, Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationPercentiles).Select(x => new SimulationPercentileModel
                    {
                        Percentile = x.Int(nameof(SimulationPercentileModel.Percentile)) ?? 0,
                        FinishTime = x.Int(nameof(SimulationPercentileModel.FinishTime)) ?? 0,
                    }),
                ],
                Bins =
                [
                    .. ReadRows(workbook, Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationHistogram).Select(x => new SimulationHistogramBinModel
                    {
                        FirstFinishTime = x.Int(nameof(SimulationHistogramBinModel.FirstFinishTime)) ?? 0,
                        LastFinishTime = x.Int(nameof(SimulationHistogramBinModel.LastFinishTime)) ?? 0,
                        Count = x.Int(nameof(SimulationHistogramBinModel.Count)) ?? 0,
                        CumulativeShare = x.Double(nameof(SimulationHistogramBinModel.CumulativeShare)) ?? 0.0,
                        PathGroupWeights = ReadPathGroupWeights(x),
                    }),
                ],
                Paths =
                [
                    .. ReadRows(workbook, Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationPaths).Select(x => new SimulationPathModel
                    {
                        Rank = x.Int(nameof(SimulationPathModel.Rank)) ?? 0,
                        Label = x.Text(nameof(SimulationPathModel.Label)) ?? string.Empty,
                        ShortLabel = x.Text(nameof(SimulationPathModel.ShortLabel)) ?? string.Empty,
                        IdLabel = x.Text(nameof(SimulationPathModel.IdLabel)) ?? string.Empty,
                        HasResourceLinks = x.Bool(nameof(SimulationPathModel.HasResourceLinks)) ?? false,
                        Weight = x.Double(nameof(SimulationPathModel.Weight)) ?? 0.0,
                        Share = x.Double(nameof(SimulationPathModel.Share)) ?? 0.0,
                    }),
                ],
                Activities =
                [
                    .. ReadRows(workbook, Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationCriticalities).Select(x => new SimulationActivityCriticalityModel
                    {
                        ActivityId = x.Int(nameof(SimulationActivityCriticalityModel.ActivityId)) ?? 0,
                        Name = x.Text(nameof(SimulationActivityCriticalityModel.Name)) ?? string.Empty,
                        CriticalIterationCount = x.Int(nameof(SimulationActivityCriticalityModel.CriticalIterationCount)) ?? 0,
                        CriticalityIndex = x.Double(nameof(SimulationActivityCriticalityModel.CriticalityIndex)) ?? 0.0,
                    }),
                ],
            };
        }

        private static IEnumerable<SheetRow> ReadRows(
            XSSFWorkbook workbook,
            string sheetTitle) =>
            SheetTable.Open(workbook, sheetTitle)?.Rows ?? [];

        // The weights are in the columns PathGroupWeight1, PathGroupWeight2 and so on, in the order of the number. The list
        // ends at the first column that has no number.
        private static List<double> ReadPathGroupWeights(SheetRow row)
        {
            List<double> weights = [];

            foreach (string column in row.ColumnTitles
                .Where(x => x.StartsWith(c_PathGroupWeightColumnPrefix, StringComparison.Ordinal))
                .Select(x => (Title: x, Number: int.TryParse(x.AsSpan(c_PathGroupWeightColumnPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int number) ? number : -1))
                .Where(x => x.Number >= 0)
                .OrderBy(x => x.Number)
                .Select(x => x.Title))
            {
                double? weight = row.Double(column);

                if (weight is null)
                {
                    break;
                }

                weights.Add(weight.Value);
            }

            return weights;
        }

        // A sheet of the workbook: the titles of its columns, and the rows under them. A row that has no cell at all is not
        // a row.
        private sealed class SheetTable
        {
            private readonly Dictionary<string, int> m_ColumnIndexes;
            private readonly IReadOnlyList<string> m_ColumnTitles;
            private readonly ISheet m_Sheet;

            private SheetTable(
                ISheet sheet,
                IReadOnlyList<string> columnTitles)
            {
                m_Sheet = sheet;
                m_ColumnTitles = columnTitles;
                m_ColumnIndexes = [];

                for (int i = 0; i < columnTitles.Count; i++)
                {
                    m_ColumnIndexes.TryAdd(columnTitles[i], i);
                }
            }

            public static SheetTable? Open(
                XSSFWorkbook workbook,
                string sheetTitle)
            {
                ISheet? sheet = workbook.GetSheet(sheetTitle);
                IRow? titleRow = sheet?.GetRow(0);

                if (sheet is null
                    || titleRow is null)
                {
                    return null;
                }

                List<string> titles = [];

                for (int i = 0; i < titleRow.LastCellNum; i++)
                {
                    titles.Add(titleRow.GetCell(i)?.ToString()?.Trim() ?? string.Empty);
                }

                return new SheetTable(sheet, titles);
            }

            public IEnumerable<SheetRow> Rows
            {
                get
                {
                    for (int i = m_Sheet.FirstRowNum + 1; i <= m_Sheet.LastRowNum; i++)
                    {
                        IRow? row = m_Sheet.GetRow(i);

                        if (row is null
                            || row.Cells.Count == 0
                            || row.Cells.All(x => x.CellType == CellType.Blank))
                        {
                            continue;
                        }

                        yield return new SheetRow(row, m_ColumnIndexes, m_ColumnTitles);
                    }
                }
            }
        }

        // A row of a sheet, read by the title of the column. Each read gives null for a column that is not there and for a
        // cell that has no value of the kind that was asked for. A number is read from a number cell, or from text that
        // holds a number in the invariant culture.
        private sealed class SheetRow
        {
            private readonly IRow m_Row;
            private readonly IReadOnlyDictionary<string, int> m_ColumnIndexes;

            public SheetRow(
                IRow row,
                IReadOnlyDictionary<string, int> columnIndexes,
                IReadOnlyList<string> columnTitles)
            {
                m_Row = row;
                m_ColumnIndexes = columnIndexes;
                ColumnTitles = columnTitles;
            }

            public IReadOnlyList<string> ColumnTitles { get; }

            private ICell? CellOf(string column) =>
                m_ColumnIndexes.TryGetValue(column, out int index)
                    ? m_Row.GetCell(index)
                    : null;

            // A cell that holds the result of a formula is read as the result.
            private static CellType TypeOf(ICell cell) =>
                cell.CellType == CellType.Formula
                    ? cell.CachedFormulaResultType
                    : cell.CellType;

            public string? Text(string column)
            {
                ICell? cell = CellOf(column);

                if (cell is null)
                {
                    return null;
                }

                return TypeOf(cell) switch
                {
                    CellType.String => cell.StringCellValue,
                    CellType.Numeric => cell.NumericCellValue.ToString(@"R", CultureInfo.InvariantCulture),
                    CellType.Boolean => cell.BooleanCellValue.ToString(),
                    _ => null,
                };
            }

            public double? Double(string column)
            {
                ICell? cell = CellOf(column);

                if (cell is null)
                {
                    return null;
                }

                double? value = TypeOf(cell) switch
                {
                    CellType.Numeric => cell.NumericCellValue,
                    CellType.String when double.TryParse(cell.StringCellValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) => parsed,
                    _ => null,
                };

                return value is double number && double.IsFinite(number)
                    ? number
                    : null;
            }

            public int? Int(string column)
            {
                double? value = Double(column);

                return value is double number
                    && number == Math.Floor(number)
                    && number >= int.MinValue
                    && number <= int.MaxValue
                        ? (int)number
                        : null;
            }

            public ulong? ULong(string column)
            {
                ICell? cell = CellOf(column);

                if (cell is null)
                {
                    return null;
                }

                switch (TypeOf(cell))
                {
                    case CellType.String when ulong.TryParse(cell.StringCellValue, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsed):
                        return parsed;
                    case CellType.Numeric:
                        // A cell holds a whole number exactly only up to 2^53.
                        double number = cell.NumericCellValue;
                        return number >= 0.0 && number <= 9007199254740992.0 && number == Math.Floor(number)
                            ? (ulong)number
                            : null;
                    default:
                        return null;
                }
            }

            public bool? Bool(string column)
            {
                ICell? cell = CellOf(column);

                if (cell is null)
                {
                    return null;
                }

                return TypeOf(cell) switch
                {
                    CellType.Boolean => cell.BooleanCellValue,
                    CellType.String when bool.TryParse(cell.StringCellValue, out bool parsed) => parsed,
                    _ => null,
                };
            }

            public T? Enum<T>(string column)
                where T : struct, Enum
            {
                string? text = Text(column);

                return text is not null
                    && System.Enum.TryParse(text.Trim(), ignoreCase: true, out T parsed)
                    && System.Enum.IsDefined(parsed)
                        ? parsed
                        : null;
            }
        }

        #endregion
    }
}
