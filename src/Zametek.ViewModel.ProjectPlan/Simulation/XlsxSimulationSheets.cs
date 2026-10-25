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

        // What Excel holds: the rows of a sheet (the titles among them) and the characters of a cell.
        internal const int MaximumRows = 1_048_576;
        internal const int MaximumCellLength = 32_767;

        // The last character of a cell that holds less than the text it was given.
        private const string c_Truncated = @"…";

        private static readonly IReadOnlyList<string> s_IterationColumnTitles =
            [
                @"Iteration",
                @"FinishTime",
                @"CriticalPaths",
            ];

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
                nameof(SimulationSummaryModel.Seed),
                nameof(SimulationSummaryModel.Distribution),
                nameof(SimulationSummaryModel.ModifiedBetaShape),
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
            XlsxWorkbookWriter workbook)
        {
            ArgumentNullException.ThrowIfNull(projectScenario);

            WriteToWorkbook(projectScenario.SimulationSettings, projectScenario.SimulationSummary, workbook);
        }

        /// <summary>
        /// Writes the sheet of the settings, and, if there is a result, the sheets of it. The workbook of a scenario and the workbook of a
        /// simulation (<see cref="SimulationWorkbook"/>) have the same sheets, and so the two cannot differ.
        /// </summary>
        public static void WriteToWorkbook(
            SimulationSettingsModel settings,
            SimulationSummaryModel? summary,
            XlsxWorkbookWriter workbook)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(workbook);

            WriteSheet(
                workbook,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationSettings,
                s_SettingsColumnTitles,
                [[settings.Iterations, settings.Distribution, settings.ModifiedBetaShape, settings.Seed, settings.UseRandomSeed, settings.TargetFinishTime]]);

            if (summary is null)
            {
                return;
            }

            WriteSheet(
                workbook,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationStatistics,
                s_StatisticsColumnTitles,
                [
                    [
                        summary.IterationCount,
                        summary.ValidIterationCount,
                        summary.FailedIterationCount,
                        summary.Seed,
                        summary.Distribution,
                        summary.ModifiedBetaShape,
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
                Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationPercentiles,
                s_PercentileColumnTitles,
                [.. summary.Percentiles.Select(x => new object?[] { x.Percentile, x.FinishTime })]);

            int groupCount = summary.Bins.Select(x => x.PathGroupWeights.Count).DefaultIfEmpty().Max();

            WriteSheet(
                workbook,
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
                Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationPaths,
                s_PathColumnTitles,
                [.. summary.Paths.Select(x => new object?[] { x.Rank, x.Label, x.ShortLabel, x.IdLabel, x.HasResourceLinks, x.Weight, x.Share })]);

            WriteSheet(
                workbook,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationCriticalities,
                s_CriticalityColumnTitles,
                [.. summary.Activities.Select(x => new object?[] { x.ActivityId, x.Name, x.CriticalIterationCount, x.CriticalityIndex })]);
        }

        /// <summary>
        /// Writes the sheet of the iterations of a run: for each iteration, its number, its finish time (a cell with no value when
        /// the iteration failed) and its critical paths, as the CSV file of the iterations has them. A project does not hold the
        /// iterations, and so a workbook that is made of a saved result has no such sheet.
        /// </summary>
        /// <remarks>
        /// A sheet of Excel has <see cref="MaximumRows"/> rows, and so a run of more iterations than that has no sheet (a run has up to
        /// one million). A cell holds <see cref="MaximumCellLength"/> characters. The critical paths of an iteration that has more than
        /// that are cut, and end with an ellipsis.
        /// </remarks>
        public static void WriteIterations(
            XlsxWorkbookWriter workbook,
            SimulationResult<int> result,
            IReadOnlyDictionary<int, string> activityNames)
        {
            ArgumentNullException.ThrowIfNull(workbook);
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(activityNames);

            if (result.IterationCount >= MaximumRows)
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.ProjectPlan.Messages.Message_SimulationIterationsFitNoSheet,
                    result.IterationCount,
                    MaximumRows - 1));
            }

            XlsxSheetWriter sheet = workbook.AddSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationIterations);

            for (int columnIndex = 0; columnIndex < s_IterationColumnTitles.Count; columnIndex++)
            {
                sheet.Text(0, columnIndex, s_IterationColumnTitles[columnIndex], XlsxCellStyle.Title);
            }

            // The label of each path is made once, and not for each iteration that has it.
            string[] labels = result.HasCriticalPaths
                ? [.. result.CriticalPaths.Select(x => SimulationAnalyzer.FormatLabel(x, activityNames))]
                : [];

            for (int i = 0; i < result.IterationCount; i++)
            {
                int row = i + 1;
                int finishTime = result.FinishTimes[i];
                sheet.Number(row, 0, i + 1);

                if (finishTime == SimulationResult<int>.FailedFinishTime)
                {
                    sheet.Blank(row, 1);
                    sheet.Blank(row, 2);
                    continue;
                }

                sheet.Number(row, 1, finishTime);

                if (result.HasCriticalPaths)
                {
                    sheet.Text(row, 2, Truncate(string.Join(@" | ", result.CriticalPathIndexes(i).ToArray().Select(x => labels[x]))));
                }
                else
                {
                    sheet.Blank(row, 2);
                }
            }
        }

        // The text, or its start and an ellipsis when a cell cannot hold it. A character of two UTF-16 units is not cut in the middle.
        internal static string Truncate(string text)
        {
            if (text.Length <= MaximumCellLength)
            {
                return text;
            }

            int length = MaximumCellLength - c_Truncated.Length;

            if (char.IsHighSurrogate(text[length - 1]))
            {
                length--;
            }

            return string.Concat(text.AsSpan(0, length), c_Truncated);
        }

        private static void WriteSheet(
            XlsxWorkbookWriter workbook,
            string sheetTitle,
            IReadOnlyList<string> columnTitles,
            IReadOnlyList<object?[]> rows)
        {
            XlsxSheetWriter sheet = workbook.AddSheet(sheetTitle);

            for (int columnIndex = 0; columnIndex < columnTitles.Count; columnIndex++)
            {
                sheet.Text(0, columnIndex, columnTitles[columnIndex], XlsxCellStyle.Title);
            }

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                object?[] values = rows[rowIndex];

                for (int columnIndex = 0; columnIndex < values.Length; columnIndex++)
                {
                    SetCellValue(sheet, rowIndex + 1, columnIndex, values[columnIndex]);
                }
            }
        }

        // A value that is not there is a blank cell. The seed is text, as the remarks of the class say. A text that is longer than a
        // cell holds is cut (see Truncate).
        private static void SetCellValue(
            XlsxSheetWriter sheet,
            int row,
            int column,
            object? value)
        {
            switch (value)
            {
                case null:
                    sheet.Blank(row, column);
                    break;
                case string text:
                    // A critical path through a very large plan can have a label that a cell cannot hold.
                    sheet.Text(row, column, Truncate(text));
                    break;
                case bool flag:
                    sheet.Boolean(row, column, flag);
                    break;
                case int number:
                    sheet.Number(row, column, number);
                    break;
                case double number:
                    sheet.Number(row, column, number);
                    break;
                case ulong seed:
                    sheet.Text(row, column, seed.ToString(CultureInfo.InvariantCulture));
                    break;
                case Enum choice:
                    sheet.Text(row, column, choice.ToString());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        #endregion

        #region Reading

        /// <summary>
        /// Reads what the workbook holds of the simulation. A sheet that is not there, a column that is not there, and a
        /// cell that is blank all give the default: the settings are the defaults and the result is none. A workbook that
        /// has the settings and not the statistics has no result, because a result is more than the lists. A cell that
        /// holds something that is not a value of its kind is a problem, and is reported (see <see cref="ImportRow"/>).
        /// </summary>
        public static (SimulationSettingsModel Settings, SimulationSummaryModel? Summary) ReadFromWorkbook(XlsxWorkbookReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);

            SimulationSettingsModel settings = ReadSettings(reader);

            return (settings, ReadSummary(reader, settings));
        }

        private static SimulationSettingsModel ReadSettings(XlsxWorkbookReader reader)
        {
            var settings = new SimulationSettingsModel();
            ImportTable? table = reader.Table(Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationSettings, s_SettingsColumnTitles);
            ImportRow? row = table?.Rows.FirstOrDefault();

            if (row is null)
            {
                return settings;
            }

            return settings with
            {
                Iterations = row.Int(nameof(SimulationSettingsModel.Iterations)) ?? settings.Iterations,
                Distribution = row.Option<DurationDistribution>(nameof(SimulationSettingsModel.Distribution)) ?? settings.Distribution,
                ModifiedBetaShape = row.Double(nameof(SimulationSettingsModel.ModifiedBetaShape)) ?? settings.ModifiedBetaShape,
                Seed = row.ULong(nameof(SimulationSettingsModel.Seed)) ?? settings.Seed,
                UseRandomSeed = row.Bool(nameof(SimulationSettingsModel.UseRandomSeed)) ?? settings.UseRandomSeed,
                TargetFinishTime = row.Int(nameof(SimulationSettingsModel.TargetFinishTime)),
            };
        }

        private static SimulationSummaryModel? ReadSummary(
            XlsxWorkbookReader reader,
            SimulationSettingsModel settings)
        {
            ImportTable? statistics = reader.Table(Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationStatistics, s_StatisticsColumnTitles);
            ImportRow? row = statistics?.Rows.FirstOrDefault();

            if (row is null)
            {
                return null;
            }

            var summary = new SimulationSummaryModel();

            // The seed, the distribution and the shape value say how the run was set up. A workbook that was made before the
            // statistics had them has the settings of the scenario in their place, as the file did before it kept them with the
            // result. A blank cell of the shape is not that: the distribution of the run had no shape value.
            bool hasProvenance = row.Table.HasColumn(nameof(SimulationSummaryModel.Seed));
            DurationDistribution distribution = hasProvenance
                ? row.Option<DurationDistribution>(nameof(SimulationSummaryModel.Distribution)) ?? summary.Distribution
                : settings.Distribution;

            summary = summary with
            {
                Seed = hasProvenance
                    ? row.ULong(nameof(SimulationSummaryModel.Seed)) ?? summary.Seed
                    : settings.Seed,
                Distribution = distribution,
                ModifiedBetaShape = hasProvenance
                    ? row.Double(nameof(SimulationSummaryModel.ModifiedBetaShape))
                    : distribution == DurationDistribution.ModifiedBetaPert ? settings.ModifiedBetaShape : null,
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
                    .. ReadRows(reader, Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationPercentiles, s_PercentileColumnTitles).Select(x => new SimulationPercentileModel
                    {
                        Percentile = x.Int(nameof(SimulationPercentileModel.Percentile)) ?? 0,
                        FinishTime = x.Int(nameof(SimulationPercentileModel.FinishTime)) ?? 0,
                    }),
                ],
                Bins =
                [
                    .. ReadRows(reader, Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationHistogram, s_HistogramColumnTitles).Select(x => new SimulationHistogramBinModel
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
                    .. ReadRows(reader, Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationPaths, s_PathColumnTitles).Select(x => new SimulationPathModel
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
                    .. ReadRows(reader, Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationCriticalities, s_CriticalityColumnTitles).Select(x => new SimulationActivityCriticalityModel
                    {
                        ActivityId = x.Int(nameof(SimulationActivityCriticalityModel.ActivityId)) ?? 0,
                        Name = x.Text(nameof(SimulationActivityCriticalityModel.Name)) ?? string.Empty,
                        CriticalIterationCount = x.Int(nameof(SimulationActivityCriticalityModel.CriticalIterationCount)) ?? 0,
                        CriticalityIndex = x.Double(nameof(SimulationActivityCriticalityModel.CriticalityIndex)) ?? 0.0,
                    }),
                ],
            };
        }

        private static IEnumerable<ImportRow> ReadRows(
            XlsxWorkbookReader reader,
            string sheetTitle,
            IReadOnlyList<string> knownTitles) =>
            reader.Table(sheetTitle, knownTitles)?.Rows ?? [];

        // The weights are in the columns PathGroupWeight1, PathGroupWeight2 and so on, in the order of the number. The list
        // ends at the first column that has no number.
        private static List<double> ReadPathGroupWeights(ImportRow row)
        {
            List<double> weights = [];

            foreach (string column in row.Table.Titles
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

        #endregion
    }
}
