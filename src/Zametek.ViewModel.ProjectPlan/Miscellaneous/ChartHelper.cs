using CsvHelper;
using OxyPlot.Axes;
using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class ChartHelper
    {
        public static string FormatScheduleOutput(
            int days,
            bool showDates,
            DateTime projectStart,
            IDateTimeCalculator dateTimeCalculator)
        {
            if (dateTimeCalculator == null)
            {
                throw new ArgumentNullException(nameof(dateTimeCalculator));
            }
            if (showDates)
            {
                return dateTimeCalculator.AddDays(projectStart, days).ToString("d", CultureInfo.InvariantCulture);
            }
            return days.ToString(CultureInfo.InvariantCulture);
        }

        public static double CalculateChartTimeXValue(
            int input,
            bool showDates,
            DateTime projectStart,
            IDateTimeCalculator dateTimeCalculator)
        {
            if (dateTimeCalculator == null)
            {
                throw new ArgumentNullException(nameof(dateTimeCalculator));
            }
            double output = input;
            if (showDates)
            {
                output = DateTimeAxis.ToDouble(dateTimeCalculator.AddDays(projectStart, input));
            }
            return output;
        }

        public static async Task ExportDataTableToCsvAsync(
            DataTable dataTable,
            string filename)
        {
            await Task.Run(() => ExportDataTableToCsv(dataTable, filename)).ConfigureAwait(true);
        }

        public static void ExportDataTableToCsv(
            DataTable dataTable,
            string filename)
        {
            if (dataTable == null)
            {
                throw new ArgumentNullException(nameof(dataTable));
            }
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentNullException(nameof(filename));
            }
            TextWriter writer = File.CreateText(filename); // This gets disposed by the CsvWriter.
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                foreach (DataColumn column in dataTable.Columns)
                {
                    csv.WriteField(column.ColumnName);
                }
                csv.NextRecord();
                foreach (DataRow row in dataTable.Rows)
                {
                    for (var i = 0; i < dataTable.Columns.Count; i++)
                    {
                        csv.WriteField(row[i]);
                    }
                    csv.NextRecord();
                }
            }
        }
    }
}
