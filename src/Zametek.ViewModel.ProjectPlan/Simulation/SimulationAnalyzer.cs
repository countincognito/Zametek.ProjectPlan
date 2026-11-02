using System.Globalization;
using System.Text;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Makes the statistics of a simulation from its raw result: the mean and the percentiles of the finish
    /// time, a histogram split by critical path, the table of the critical paths, and how often each
    /// activity was critical. It is a pure calculation, with no state.
    /// </summary>
    /// <remarks>
    /// When an iteration has more than one critical path, its weight is split equally among those paths.
    /// The shares of the paths therefore add up to 100 %, and the criticality of an activity stays right.
    /// Whole days make ties between paths common, and a "multiple paths" group would be too large to be
    /// useful.
    /// </remarks>
    public static class SimulationAnalyzer
    {
        /// <summary>
        /// The most paths that the histogram shows by themselves. All the other paths share one group.
        /// </summary>
        public const int MaximumChartPaths = 8;

        /// <summary>
        /// The most paths that the table lists.
        /// </summary>
        public const int MaximumTablePaths = 25;

        /// <summary>
        /// The most bins that the histogram has. A plan with a wider range of finish times has bins of
        /// more than one day.
        /// </summary>
        public const int MaximumBins = 100;

        private const int c_ShortLabelEnds = 2;
        private const int c_ShortLabelMaximumSteps = 6;

        private static readonly int[] s_Percentiles = [5, 10, 25, 50, 75, 80, 90, 95, 99];

        /// <param name="result">The raw result of the run.</param>
        /// <param name="activityNames">The name of each activity, by its ID.</param>
        /// <param name="plannedFinishTime">The finish time of the plan as it stands.</param>
        /// <param name="targetFinishTime">The target finish time, or null.</param>
        /// <param name="settings">
        /// The settings that the run used. The summary keeps the seed, the distribution and the shape value, so that the
        /// result says how it was made also after the settings of the scenario change.
        /// </param>
        public static SimulationSummaryModel Analyze(
            SimulationResult<int> result,
            IReadOnlyDictionary<int, string> activityNames,
            int plannedFinishTime,
            int? targetFinishTime,
            SimulationSettings settings)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(activityNames);
            ArgumentNullException.ThrowIfNull(settings);

            int[] finishTimes = [.. result.FinishTimes.Where(x => x != SimulationResult<int>.FailedFinishTime)];
            int validCount = finishTimes.Length;

            var summary = new SimulationSummaryModel
            {
                IterationCount = result.IterationCount,
                Seed = settings.Seed,
                Distribution = settings.Distribution,
                ModifiedBetaShape = settings.Distribution == DurationDistribution.ModifiedBetaPert ? settings.ModifiedBetaShape : null,
                ValidIterationCount = validCount,
                FailedIterationCount = result.FailedIterationCount,
                PlannedFinishTime = plannedFinishTime,
                TargetFinishTime = targetFinishTime,
            };

            if (validCount == 0)
            {
                return summary;
            }

            Array.Sort(finishTimes);

            int minimum = finishTimes[0];
            int maximum = finishTimes[^1];
            double mean = finishTimes.Sum(x => (double)x) / validCount;
            double variance = finishTimes.Sum(x => (x - mean) * (x - mean)) / validCount;

            double? targetProbability = targetFinishTime is int target
                ? (double)finishTimes.Count(x => x <= target) / validCount
                : null;

            // The histogram: one bin for each day while there are few enough days, and wider bins after that.
            int range = maximum - minimum + 1;
            int binWidth = Math.Max(1, (int)Math.Ceiling(range / (double)MaximumBins));
            int binCount = (range + binWidth - 1) / binWidth;

            // The weight of each path, and the groups that the histogram shows.
            double[] pathWeights = [];
            int[] groupOfPath = [];
            int groupCount = 0;
            List<int> rankedPaths = [];
            if (result.HasCriticalPaths)
            {
                pathWeights = new double[result.CriticalPaths.Count];
                for (int i = 0; i < result.IterationCount; i++)
                {
                    if (result.FinishTimes[i] == SimulationResult<int>.FailedFinishTime)
                    {
                        continue;
                    }

                    ReadOnlySpan<int> paths = result.CriticalPathIndexes(i);
                    if (paths.Length == 0)
                    {
                        continue;
                    }

                    double share = 1.0 / paths.Length;
                    foreach (int path in paths)
                    {
                        pathWeights[path] += share;
                    }
                }

                // The heaviest first, and the path that appeared first when two weigh the same.
                rankedPaths = [.. Enumerable.Range(0, pathWeights.Length)
                    .OrderByDescending(x => pathWeights[x])
                    .ThenBy(x => x)];

                groupCount = Math.Min(MaximumChartPaths, rankedPaths.Count);
                groupOfPath = new int[pathWeights.Length];
                Array.Fill(groupOfPath, groupCount);
                for (int rank = 0; rank < groupCount; rank++)
                {
                    groupOfPath[rankedPaths[rank]] = rank;
                }
            }

            var binCounts = new int[binCount];
            var binGroupWeights = new double[binCount][];
            for (int b = 0; b < binCount; b++)
            {
                binGroupWeights[b] = new double[groupCount + 1];
            }

            for (int i = 0; i < result.IterationCount; i++)
            {
                int finishTime = result.FinishTimes[i];
                if (finishTime == SimulationResult<int>.FailedFinishTime)
                {
                    continue;
                }

                int bin = (finishTime - minimum) / binWidth;
                binCounts[bin]++;

                if (!result.HasCriticalPaths)
                {
                    binGroupWeights[bin][groupCount] += 1.0;
                    continue;
                }

                ReadOnlySpan<int> paths = result.CriticalPathIndexes(i);
                if (paths.Length == 0)
                {
                    binGroupWeights[bin][groupCount] += 1.0;
                    continue;
                }

                double share = 1.0 / paths.Length;
                foreach (int path in paths)
                {
                    binGroupWeights[bin][groupOfPath[path]] += share;
                }
            }

            var bins = new List<SimulationHistogramBinModel>(binCount);
            int cumulative = 0;
            for (int b = 0; b < binCount; b++)
            {
                cumulative += binCounts[b];
                bins.Add(new SimulationHistogramBinModel
                {
                    FirstFinishTime = minimum + (b * binWidth),
                    LastFinishTime = Math.Min(maximum, minimum + (((b + 1) * binWidth) - 1)),
                    Count = binCounts[b],
                    CumulativeShare = (double)cumulative / validCount,
                    PathGroupWeights = binGroupWeights[b],
                });
            }

            // The table of the paths.
            var pathRows = new List<SimulationPathModel>();
            double listedWeight = 0.0;
            for (int rank = 0; rank < Math.Min(MaximumTablePaths, rankedPaths.Count); rank++)
            {
                int index = rankedPaths[rank];
                double weight = pathWeights[index];
                listedWeight += weight;
                (string label, string shortLabel, string idLabel, bool hasResourceLinks) = DescribePath(result.CriticalPaths[index], activityNames);
                pathRows.Add(new SimulationPathModel
                {
                    Rank = rank + 1,
                    Label = label,
                    ShortLabel = shortLabel,
                    IdLabel = idLabel,
                    HasResourceLinks = hasResourceLinks,
                    Weight = weight,
                    Share = weight / validCount,
                });
            }

            // How often each activity was critical.
            var activities = new List<SimulationActivityCriticalityModel>();
            if (result.HasCriticalActivities)
            {
                for (int a = 0; a < result.ActivityIds.Count; a++)
                {
                    int count = 0;
                    for (int i = 0; i < result.IterationCount; i++)
                    {
                        if (result.FinishTimes[i] != SimulationResult<int>.FailedFinishTime
                            && result.IsCritical(i, a))
                        {
                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        int id = result.ActivityIds[a];
                        activities.Add(new SimulationActivityCriticalityModel
                        {
                            ActivityId = id,
                            Name = NameOf(activityNames, id),
                            CriticalIterationCount = count,
                            CriticalityIndex = (double)count / validCount,
                        });
                    }
                }

                activities = [.. activities
                    .OrderByDescending(x => x.CriticalIterationCount)
                    .ThenBy(x => x.ActivityId)];
            }

            return summary with
            {
                Mean = mean,
                StandardDeviation = Math.Sqrt(variance),
                Minimum = minimum,
                Maximum = maximum,
                TargetProbability = targetProbability,
                Percentiles = [.. s_Percentiles.Select(p => new SimulationPercentileModel
                {
                    Percentile = p,
                    FinishTime = Percentile(finishTimes, p),
                })],
                BinWidth = binWidth,
                Bins = bins,
                Paths = pathRows,
                ChartPathCount = groupCount,
                OtherPathsWeight = Math.Max(0.0, (validCount - listedWeight)),
                Activities = activities,
            };
        }

        /// <summary>
        /// The smallest value that at least the given share of the sorted values reach (the nearest rank).
        /// </summary>
        public static int Percentile(int[] sortedValues, int percentile)
        {
            ArgumentNullException.ThrowIfNull(sortedValues);
            if (sortedValues.Length == 0)
            {
                throw new ArgumentException(@"There are no values.", nameof(sortedValues));
            }

            // Whole numbers throughout: a product of floating point numbers can come out a hair above a whole
            // number, and the rank would then move up by one.
            long rank = (((long)percentile * sortedValues.Length) + 99) / 100;
            rank = Math.Clamp(rank, 1, sortedValues.Length);
            return sortedValues[(int)rank - 1];
        }

        public static string NameOf(IReadOnlyDictionary<int, string> activityNames, int activityId)
        {
            ArgumentNullException.ThrowIfNull(activityNames);
            return activityNames.TryGetValue(activityId, out string? name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : $@"#{activityId.ToString(CultureInfo.InvariantCulture)}";
        }

        /// <summary>
        /// The names of the activities on a path, in order, with > between two that depend on each other
        /// and ~> between two that share a resource.
        /// </summary>
        public static string FormatLabel(CriticalPath<int> path, IReadOnlyDictionary<int, string> activityNames)
        {
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(activityNames);
            return DescribePath(path, activityNames).Label;
        }

        private static (string Label, string ShortLabel, string IdLabel, bool HasResourceLinks) DescribePath(
            CriticalPath<int> path,
            IReadOnlyDictionary<int, string> activityNames)
        {
            IReadOnlyList<CriticalPathStep<int>> steps = path.Steps;
            bool hasResourceLinks = steps.Any(x => x.Link == CriticalPathLink.Resource);

            string Join(IEnumerable<int> indexes, Func<int, string> describe)
            {
                var builder = new StringBuilder();
                int? previous = null;
                foreach (int index in indexes)
                {
                    if (previous is int p)
                    {
                        // A gap is shown as an ellipsis, in the place of the steps that are left out.
                        builder.Append(index == p + 1
                            ? (steps[index].Link == CriticalPathLink.Resource ? @" ~> " : @" > ")
                            : @" > … > ");
                    }
                    builder.Append(describe(steps[index].ActivityId));
                    previous = index;
                }
                return builder.ToString();
            }

            string Name(int id) => NameOf(activityNames, id);

            string label = Join(Enumerable.Range(0, steps.Count), Name);

            string shortLabel = steps.Count <= c_ShortLabelMaximumSteps
                ? label
                : Join(
                    Enumerable.Range(0, c_ShortLabelEnds)
                        .Concat(Enumerable.Range(steps.Count - c_ShortLabelEnds, c_ShortLabelEnds)),
                    Name);

            // The ID of an activity takes the place of its name: it is much shorter.
            string idLabel = Join(Enumerable.Range(0, steps.Count), id => id.ToString(CultureInfo.InvariantCulture));

            return (label, shortLabel, idLabel, hasResourceLinks);
        }
    }
}
