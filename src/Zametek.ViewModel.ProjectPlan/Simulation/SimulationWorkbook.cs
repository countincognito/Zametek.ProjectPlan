using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The result of a simulation as one Excel workbook, with a sheet for each of the CSV files and one more. The sheets are the ones that the
    /// Excel export of a scenario writes for its simulation (<see cref="XlsxSimulationSheets"/>): the settings, the statistics, the
    /// percentiles, the histogram, the critical paths and the criticalities. A run in this session also has the sheet of its iterations,
    /// which a project does not hold.
    /// </summary>
    /// <remarks>
    /// A person who needs the files of a simulation gets one file in the place of five, and the numbers in it are numbers and not text.
    /// The workbook is made in memory, as every workbook of the application is. Excel holds at most 1,048,576 rows in a sheet, and a run
    /// has at most one million iterations, so that the sheet of the iterations always fits.
    /// </remarks>
    internal static class SimulationWorkbook
    {
        /// <param name="stream">The stream to write to. It is left open.</param>
        /// <param name="settings">The settings of the scenario.</param>
        /// <param name="summary">The result.</param>
        /// <param name="result">The iterations of the run in this session, or null for a result that a project held.</param>
        /// <param name="activityNames">The name of each activity, by its ID.</param>
        /// <param name="localNow">The time that the package of the workbook is stamped with.</param>
        public static void Write(
            Stream stream,
            SimulationSettingsModel settings,
            SimulationSummaryModel summary,
            SimulationResult<int>? result,
            IReadOnlyDictionary<int, string> activityNames,
            DateTimeOffset localNow)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(summary);
            ArgumentNullException.ThrowIfNull(activityNames);

            using var workbook = new XlsxWorkbookWriter();

            XlsxSimulationSheets.WriteToWorkbook(settings, summary, workbook);

            if (result is not null)
            {
                XlsxSimulationSheets.WriteIterations(workbook, result, activityNames);
            }

            workbook.Save(stream, localNow);
        }
    }
}
