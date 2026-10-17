using System.Buffers;
using System.Globalization;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Writes what a simulation found as CSV: the statistics, the histogram, the critical paths, the
    /// criticality of each activity, and every iteration. The numbers use the invariant culture, a line
    /// ends with a line feed, and a field with a comma, a quote or a line end is quoted, as RFC 4180 says.
    /// </summary>
    public static class SimulationCsvWriter
    {
        private static readonly SearchValues<char> s_QuotedCharacters = SearchValues.Create(",\"\r\n");

        public static void WriteStatistics(TextWriter writer, SimulationSummaryModel summary)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(summary);

            WriteRow(writer, @"Statistic", @"Value");
            WriteRow(writer, @"Iterations", Number(summary.IterationCount));
            WriteRow(writer, @"ValidIterations", Number(summary.ValidIterationCount));
            WriteRow(writer, @"FailedIterations", Number(summary.FailedIterationCount));
            WriteRow(writer, @"PlannedFinish", Number(summary.PlannedFinishTime));

            if (!summary.HasResult)
            {
                return;
            }

            WriteRow(writer, @"Mean", Number(summary.Mean));
            WriteRow(writer, @"StandardDeviation", Number(summary.StandardDeviation));
            WriteRow(writer, @"Minimum", Number(summary.Minimum));
            WriteRow(writer, @"Maximum", Number(summary.Maximum));

            foreach (SimulationPercentileModel percentile in summary.Percentiles)
            {
                WriteRow(writer, $@"P{Number(percentile.Percentile)}", Number(percentile.FinishTime));
            }

            if (summary.TargetFinishTime is int target)
            {
                WriteRow(writer, @"TargetFinish", Number(target));
                WriteRow(writer, @"ProbabilityOfFinishingByTarget", Number(summary.TargetProbability ?? 0.0));
            }
        }

        public static void WriteHistogram(TextWriter writer, SimulationSummaryModel summary)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(summary);

            var header = new List<string> { @"FirstFinishTime", @"LastFinishTime", @"Count", @"Share", @"CumulativeShare" };
            for (int i = 0; i < summary.ChartPathCount; i++)
            {
                header.Add($@"Path{Number(i + 1)}");
            }
            header.Add(@"OtherPaths");
            WriteRow(writer, [.. header]);

            foreach (SimulationHistogramBinModel bin in summary.Bins)
            {
                var row = new List<string>
                {
                    Number(bin.FirstFinishTime),
                    Number(bin.LastFinishTime),
                    Number(bin.Count),
                    Number((double)bin.Count / summary.ValidIterationCount),
                    Number(bin.CumulativeShare),
                };
                row.AddRange(bin.PathGroupWeights.Select(x => Number(x)));
                WriteRow(writer, [.. row]);
            }
        }

        public static void WritePaths(TextWriter writer, SimulationSummaryModel summary)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(summary);

            WriteRow(writer, @"Rank", @"Path", @"HasResourceLinks", @"Iterations", @"Share");

            foreach (SimulationPathModel path in summary.Paths)
            {
                WriteRow(
                    writer,
                    Number(path.Rank),
                    path.Label,
                    path.HasResourceLinks ? @"true" : @"false",
                    Number(path.Weight),
                    Number(path.Share));
            }

            if (summary.OtherPathsWeight > 0.0 && summary.HasResult)
            {
                WriteRow(
                    writer,
                    string.Empty,
                    @"Other",
                    string.Empty,
                    Number(summary.OtherPathsWeight),
                    Number(summary.OtherPathsWeight / summary.ValidIterationCount));
            }
        }

        public static void WriteActivities(TextWriter writer, SimulationSummaryModel summary)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(summary);

            WriteRow(writer, @"ActivityId", @"Name", @"CriticalIterations", @"CriticalityIndex");

            foreach (SimulationActivityCriticalityModel activity in summary.Activities)
            {
                WriteRow(
                    writer,
                    Number(activity.ActivityId),
                    activity.Name,
                    Number(activity.CriticalIterationCount),
                    Number(activity.CriticalityIndex));
            }
        }

        /// <summary>
        /// Every iteration, with its finish time and its critical paths (a path is written as the
        /// names of its activities, and several paths are separated by a bar).
        /// </summary>
        public static void WriteIterations(
            TextWriter writer,
            SimulationResult<int> result,
            IReadOnlyDictionary<int, string> activityNames)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(activityNames);

            WriteRow(writer, @"Iteration", @"FinishTime", @"CriticalPaths");

            // The label of each path is made once, and not for each iteration that has it.
            string[] labels = result.HasCriticalPaths
                ? [.. result.CriticalPaths.Select(x => SimulationAnalyzer.FormatLabel(x, activityNames))]
                : [];

            for (int i = 0; i < result.IterationCount; i++)
            {
                int finishTime = result.FinishTimes[i];
                string paths = string.Empty;

                if (result.HasCriticalPaths && finishTime != SimulationResult<int>.FailedFinishTime)
                {
                    paths = string.Join(@" | ", result.CriticalPathIndexes(i).ToArray().Select(x => labels[x]));
                }

                WriteRow(
                    writer,
                    Number(i + 1),
                    finishTime == SimulationResult<int>.FailedFinishTime ? string.Empty : Number(finishTime),
                    paths);
            }
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Number(double value) => value.ToString(@"R", CultureInfo.InvariantCulture);

        private static void WriteRow(TextWriter writer, params string[] fields)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0)
                {
                    writer.Write(',');
                }
                writer.Write(Quote(fields[i]));
            }
            writer.Write(NewLineHelper.NewLine);
        }

        private static string Quote(string field)
        {
            if (field.AsSpan().IndexOfAny(s_QuotedCharacters) < 0)
            {
                return field;
            }
            return $@"""{field.Replace(@"""", @"""""", StringComparison.Ordinal)}""";
        }
    }
}
