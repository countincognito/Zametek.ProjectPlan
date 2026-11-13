using ConsoleTables;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // The text zpp prints for a run and the exit code the run ends with, for whichever console the run prints to: zpp's
    // own, or one that keeps a job's text for a service to return, which then says exactly what zpp would have said.
    internal static class JobConsoleHelper
    {
        // Prints what a job that ran to its end leaves on stdout - the compiler's output when the plan did not compile,
        // and otherwise its metrics, in the format asked for, with the statistics of its simulation after them if it ran
        // one, the metrics of each tracked scenario if it compiled them, and the axes and the fits of the scenario chart if it
        // has them (each in a table of its own, or in JSON as one more member) - and returns the exit code the run ends with.
        public static async Task<ExitCode> WriteResultAsync(
            IJobConsole console,
            JobResult result,
            MetricsExport metricsFormat)
        {
            ArgumentNullException.ThrowIfNull(console);
            ArgumentNullException.ThrowIfNull(result);

            if (result.Status == JobStatus.CompilationErrors)
            {
                await console.DisplayAsync(result.CompilationOutput, hasErrors: true);
                return ExitCode.CompilationErrors;
            }

            // Metrics. A plan that compiled always has them.
            {
                JobMetrics metrics = result.Metrics ?? throw new InvalidOperationException();

                switch (metricsFormat)
                {
                    case MetricsExport.Json:
                        // Machine output: undecorated, no colours, no leading
                        // blank line, so it can be piped straight into a parser.
                        await console.WriteLineAsync(BuildMetricsJson(metrics, result.Simulation, result.TrackedScenarios, result.ScenarioChart));
                        break;
                    case MetricsExport.Table:
                        await console.DisplayAsync(BuildMetricsText(metrics, result.Simulation, result.TrackedScenarios, result.ScenarioChart, x => x.ToString()), hasErrors: false);
                        break;
                    case MetricsExport.Markdown:
                    default:
                        await console.DisplayAsync(BuildMetricsText(metrics, result.Simulation, result.TrackedScenarios, result.ScenarioChart, x => x.ToMarkDownString()), hasErrors: false);
                        break;
                }
            }

            // A chart or graph export that fails does not throw: the job reports
            // the failure through its sink, as the desktop reports it in a dialog.
            // The remaining outputs have still been produced and the metrics
            // printed, but the run as a whole has failed.
            return result.Status == JobStatus.CompletedWithErrors ? ExitCode.Failure : ExitCode.Success;
        }

        // Prints, on stderr, why a run stopped on an exception, and returns the exit code the run ends with: a
        // compilation that ran out of time has its own, and anything else is a failure. A scenario the job could not
        // select is described in zpp's words rather than the engine's. A workbook that has problems in its cells is said one
        // problem to a line, with the place of each, up to maximumImportProblems lines (the service that returns the console
        // of a job keeps it short).
        public static async Task<ExitCode> WriteFailureAsync(
            IJobConsole console,
            Exception exception,
            int maximumImportProblems = int.MaxValue)
        {
            ArgumentNullException.ThrowIfNull(console);
            ArgumentNullException.ThrowIfNull(exception);

            switch (exception)
            {
                case ProjectNotReadableException { ImportProblems: ScenarioImportException import }:
                    // A line at a time, as every other message of zpp is written, so that the line ends are the platform's.
                    await console.WriteErrorLineAsync(ImportIssueText.BuildHeader(import.TotalCount));

                    foreach (string line in ImportIssueText.ListLines(import.Issues, import.TotalCount, maximumImportProblems))
                    {
                        await console.WriteErrorLineAsync(line);
                    }

                    return ExitCode.Failure;
                case ScenarioSelectionException ex:
                    await console.WriteErrorLineAsync(BuildScenarioSelectionMessage(ex));
                    return ExitCode.Failure;
                case GraphCompilationTimeoutException ex:
                    await console.WriteErrorLineAsync(ex.Message);
                    return ExitCode.CompilationTimeout;
                default:
                    await console.WriteErrorLineAsync(exception.Message);
                    return ExitCode.Failure;
            }
        }

        // Prints a project's scenarios on stdout.
        public static async Task WriteScenariosAsync(
            IJobConsole console,
            IEnumerable<ScenarioSummary> scenarios)
        {
            ArgumentNullException.ThrowIfNull(console);
            ArgumentNullException.ThrowIfNull(scenarios);

            await console.DisplayAsync(BuildScenarioTable(scenarios).ToMarkDownString(), hasErrors: false);
        }

        // Prints a message a job raised as the desktop would show it in a dialog: errors and warnings on stderr,
        // anything else on stdout.
        public static async Task WriteMessageAsync(
            IJobConsole console,
            JobMessage message)
        {
            ArgumentNullException.ThrowIfNull(console);
            ArgumentNullException.ThrowIfNull(message);

            string text = $@"{message.Title}: {message.Message}";

            if (message.Kind is JobMessageKind.Error or JobMessageKind.Warning)
            {
                await console.WriteErrorLineAsync(text);
            }
            else
            {
                await console.WriteLineAsync(text);
            }
        }

        // The tables of a run as one block of text, written as a table or as Markdown: the metrics, then, if the job ran a
        // simulation, its statistics, if it compiled the tracked scenarios, the metrics of each, and, if the scenario chart has
        // data, its axes and its fits, each in a table of its own. A table ends its last line, and so one line end more is the
        // blank line between two tables.
        private static string BuildMetricsText(
            JobMetrics metrics,
            JobSimulationMetrics? simulation,
            IReadOnlyList<JobTrackedScenario>? trackedScenarios,
            ScenarioChartDataModel? scenarioChart,
            Func<ConsoleTable, string> write)
        {
            string text = write(BuildMetricsTable(metrics));

            if (simulation is not null)
            {
                text += NewLineHelper.NewLine + write(BuildSimulationTable(simulation));
            }

            if (trackedScenarios is { Count: > 0 })
            {
                text += NewLineHelper.NewLine + write(BuildTrackedScenariosTable(trackedScenarios));
            }

            if (scenarioChart is not null)
            {
                text += NewLineHelper.NewLine + write(BuildScenarioChartTable(scenarioChart));
            }

            return text;
        }

        // The axes and the fits of the scenario chart as a table of their own, as the panel shows them: the metric of each
        // axis, and for an axis of Y that has a fit, the formula of the curve and of its derivative. The points are in the
        // table of the tracked scenarios.
        internal static ConsoleTable BuildScenarioChartTable(ScenarioChartDataModel scenarioChart)
        {
            ArgumentNullException.ThrowIfNull(scenarioChart);
            ConsoleTable table = CreateTable(Resource.ProjectPlan.Titles.Title_ScenarioChartView, Resource.ProjectPlan.Titles.Title_Values);
            table.AddRow(Resource.ProjectPlan.Menus.Menu_TrackedMetricXAxis, StringConverters.TrackedMetricsValue(scenarioChart.XMetric));
            AddScenarioChartRows(table, scenarioChart.Y1, Resource.ProjectPlan.Menus.Menu_TrackedMetricY1Axis, Resource.ProjectPlan.Labels.Label_CurveFittingY1, Resource.ProjectPlan.Labels.Label_CurveFittingDerivativeY1);
            AddScenarioChartRows(table, scenarioChart.Y2, Resource.ProjectPlan.Menus.Menu_TrackedMetricY2Axis, Resource.ProjectPlan.Labels.Label_CurveFittingY2, Resource.ProjectPlan.Labels.Label_CurveFittingDerivativeY2);
            return table;
        }

        private static void AddScenarioChartRows(
            ConsoleTable table,
            ScenarioChartSeriesModel? series,
            string metricLabel,
            string fitLabel,
            string derivativeLabel)
        {
            if (series is null)
            {
                return;
            }

            table.AddRow(metricLabel, StringConverters.TrackedMetricsValue(series.Metric));

            if (series.CurveFit is CurveFitModel fit)
            {
                table.AddRow(fitLabel, CurveFitCalculator.BuildFormula(fit, series.AbsoluteCurveFitting));
                table.AddRow(derivativeLabel, CurveFitCalculator.BuildDerivativeFormula(fit, series.AbsoluteCurveFitting));
            }
        }

        // The metrics as a table, which prints as a table or as Markdown. This and the builders below are internal
        // rather than private so the test assembly can build the text it expects a run to print.
        internal static ConsoleTable BuildMetricsTable(JobMetrics metrics)
        {
            ConsoleTable table = CreateTable(Resource.ProjectPlan.Titles.Title_Metrics, Resource.ProjectPlan.Titles.Title_Values);

            foreach ((string label, string value) in MetricRows(metrics))
            {
                table.AddRow(label, value);
            }

            return table;
        }

        // The metrics of each tracked scenario as a table: a column for each scenario, under its path, and a row for each
        // metric, as the table of the metrics has them. A scenario that did not compile has no values.
        internal static ConsoleTable BuildTrackedScenariosTable(IReadOnlyList<JobTrackedScenario> trackedScenarios)
        {
            ConsoleTable table = CreateTable([Resource.ProjectPlan.Titles.Title_TrackedScenarios, .. trackedScenarios.Select(x => x.Path)]);
            List<(string Label, string Value)>[] columns = [.. trackedScenarios.Select(x => MetricRows(x.Metrics))];
            List<(string Label, string Value)> labels = MetricRows(null);

            for (int row = 0; row < labels.Count; row++)
            {
                table.AddRow([labels[row].Label, .. columns.Select(x => x[row].Value)]);
            }

            return table;
        }

        // The rows of a table of metrics: the label of each metric and its value as zpp prints it, or no value when there are
        // no metrics.
        private static List<(string Label, string Value)> MetricRows(JobMetrics? metrics)
        {
            string Value(Func<JobMetrics, string> format) => metrics is null ? string.Empty : format(metrics);

            return
            [
                (Resource.ProjectPlan.Labels.Label_ActivityRisk, Value(x => $@"{x.ActivityRisk:F2}")),
                (Resource.ProjectPlan.Labels.Label_ActivityRiskWithStdDevCorrection, Value(x => $@"{x.ActivityRiskWithStdDevCorrection:F2}")),
                (Resource.ProjectPlan.Labels.Label_CriticalityRisk, Value(x => $@"{x.CriticalityRisk:F2}")),
                (Resource.ProjectPlan.Labels.Label_FibonacciRisk, Value(x => $@"{x.FibonacciRisk:F2}")),

                (Resource.ProjectPlan.Labels.Label_GeometricActivityRisk, Value(x => $@"{x.GeometricActivityRisk:F2}")),
                (Resource.ProjectPlan.Labels.Label_GeometricCriticalityRisk, Value(x => $@"{x.GeometricCriticalityRisk:F2}")),
                (Resource.ProjectPlan.Labels.Label_GeometricFibonacciRisk, Value(x => $@"{x.GeometricFibonacciRisk:F2}")),
                (Resource.ProjectPlan.Labels.Label_CyclomaticComplexity, Value(x => $@"{x.NetworkCyclomaticComplexity}")),

                (Resource.ProjectPlan.Labels.Label_ActivityEffort, Value(x => $@"{x.ActivityEffort:F0}")),
                (Resource.ProjectPlan.Labels.Label_DurationManMonths, Value(x => $@"{x.NetworkDurationManMonths:F1}")),
                (Resource.ProjectPlan.Labels.Label_ProjectStartDate, Value(x => x.ProjectStartDate?.ToString(DateTimeCalculator.DateFormat, CultureInfo.CurrentCulture) ?? string.Empty)),
                (Resource.ProjectPlan.Labels.Label_ProjectFinish, Value(x => $@"{x.ProjectFinish}")),
                (Resource.ProjectPlan.Labels.Label_EffortEfficiency, Value(x => $@"{x.EffortEfficiency:F3}")),

                (Resource.ProjectPlan.Labels.Label_PeakHeadcount, Value(x => $@"{x.PeakHeadcount}")),
                (Resource.ProjectPlan.Labels.Label_AverageHeadcount, Value(x => $@"{x.AverageHeadcount:F1}")),
                (Resource.ProjectPlan.Labels.Label_PeakToAverageHeadcount, Value(x => $@"{x.PeakToAverageHeadcount:F2}")),

                (Resource.ProjectPlan.Labels.Label_DirectEffort, Value(x => $@"{x.DirectEffort:F0}")),
                (Resource.ProjectPlan.Labels.Label_IndirectEffort, Value(x => $@"{x.IndirectEffort:F0}")),
                (Resource.ProjectPlan.Labels.Label_OtherEffort, Value(x => $@"{x.OtherEffort:F0}")),
                (Resource.ProjectPlan.Labels.Label_TotalEffort, Value(x => $@"{x.TotalEffort:F0}")),

                (Resource.ProjectPlan.Labels.Label_DirectCost, Value(x => $@"{x.DirectCost:F2}")),
                (Resource.ProjectPlan.Labels.Label_IndirectCost, Value(x => $@"{x.IndirectCost:F2}")),
                (Resource.ProjectPlan.Labels.Label_OtherCost, Value(x => $@"{x.OtherCost:F2}")),
                (Resource.ProjectPlan.Labels.Label_TotalCost, Value(x => $@"{x.TotalCost:F2}")),

                (Resource.ProjectPlan.Labels.Label_DirectBilling, Value(x => $@"{x.DirectBilling:F2}")),
                (Resource.ProjectPlan.Labels.Label_IndirectBilling, Value(x => $@"{x.IndirectBilling:F2}")),
                (Resource.ProjectPlan.Labels.Label_OtherBilling, Value(x => $@"{x.OtherBilling:F2}")),
                (Resource.ProjectPlan.Labels.Label_TotalBilling, Value(x => $@"{x.TotalBilling:F2}")),

                (Resource.ProjectPlan.Labels.Label_DirectMargin, Value(x => $@"{x.DirectMarginAbsolute:F2}{x.DisplayDirectMargin}")),
                (Resource.ProjectPlan.Labels.Label_IndirectMargin, Value(x => $@"{x.IndirectMarginAbsolute:F2}{x.DisplayIndirectMargin}")),
                (Resource.ProjectPlan.Labels.Label_OtherMargin, Value(x => $@"{x.OtherMarginAbsolute:F2}{x.DisplayOtherMargin}")),
                (Resource.ProjectPlan.Labels.Label_TotalMargin, Value(x => $@"{x.TotalMarginAbsolute:F2}{x.DisplayTotalMargin}")),
            ];
        }

        // The statistics of a simulation as a table of their own, as the panel shows them: the seed and the distribution
        // first, then each of its other lines.
        internal static ConsoleTable BuildSimulationTable(JobSimulationMetrics simulation)
        {
            ConsoleTable table = CreateTable(Resource.ProjectPlan.Titles.Title_SimulationMetrics, Resource.ProjectPlan.Titles.Title_Values);

            foreach (SimulationStatisticModel statistic in simulation.Statistics)
            {
                table.AddRow(statistic.Name, statistic.Value);
            }

            return table;
        }

        // A table with a column for each title. The table library breaks a cell of more than 40 characters, which is its
        // default, onto more lines, and Markdown reads a line that continues a cell as a row of its own. A statistic can be
        // longer (the chance of the target, with the target and its date, has 50 characters), and so can the path of a
        // scenario, and so the width has no limit.
        private static ConsoleTable CreateTable(params string[] titles)
        {
            var table = new ConsoleTable(titles) { MaxWidth = int.MaxValue };

            table.Configure(x =>
            {
                x.NumberAlignment = Alignment.Left;
                x.EnableCount = false;
            });

            return table;
        }

        // The metrics as JSON. When the job ran a simulation, its statistics follow as one more object, Simulation: raw values,
        // as the metrics are, with the seed written as text - as the API writes it - because a seed can be larger than
        // every reader of JSON reads without loss, and the dates as yyyy-MM-dd. When the job compiled the tracked scenarios,
        // TrackedScenarios follows: for each, its path, its id, whether the job loaded it, and its metrics as the metrics of
        // the job are written (null for a scenario that did not compile). When the scenario chart has data, ScenarioChart
        // follows: the metrics of its axes, and for each axis of Y its points and its fit, with the coefficients as numbers
        // (null for an axis with no fit). Without any of them, the text is what it was before there was one.
        internal static string BuildMetricsJson(
            JobMetrics metrics,
            JobSimulationMetrics? simulation = null,
            IReadOnlyList<JobTrackedScenario>? trackedScenarios = null,
            ScenarioChartDataModel? scenarioChart = null)
        {
            object output = BuildMetricsObject(metrics);

            // Json.NET indents with the platform's line end. JSON escapes the line breaks in its strings, so every one
            // left in the text is indentation.
            if (simulation is null
                && trackedScenarios is null
                && scenarioChart is null)
            {
                return NewLineHelper.NormalizeNewLines(JsonConvert.SerializeObject(output, Formatting.Indented));
            }

            JObject json = JObject.FromObject(output);

            if (simulation is not null)
            {
                json[@"Simulation"] = JObject.FromObject(BuildSimulationJson(simulation));
            }

            if (trackedScenarios is not null)
            {
                json[@"TrackedScenarios"] = new JArray(trackedScenarios.Select(x => new JObject
                {
                    [@"Path"] = x.Path,
                    [@"Id"] = x.Id.ToString(),
                    [@"IsCurrent"] = x.IsCurrent,
                    [@"Metrics"] = x.Metrics is null ? JValue.CreateNull() : JObject.FromObject(BuildMetricsObject(x.Metrics)),
                }));
            }

            if (scenarioChart is not null)
            {
                json[@"ScenarioChart"] = new JObject
                {
                    [@"XMetric"] = TrackedMetricsNames.Get(scenarioChart.XMetric),
                    [@"Y1"] = BuildScenarioChartSeriesJson(scenarioChart.Y1),
                    [@"Y2"] = BuildScenarioChartSeriesJson(scenarioChart.Y2),
                };
            }

            return NewLineHelper.NormalizeNewLines(json.ToString(Formatting.Indented));
        }

        private static object BuildMetricsObject(JobMetrics metrics)
        {
            // Raw values rather than display strings wherever the contract offers
            // them: JSON numbers are culture-invariant by construction, and the
            // *Margin/*MarginAbsolute pairs carry the ratio and the currency value
            // that the table's display strings combine. ProjectFinish is the one
            // exception - the contract only exposes it as a display string.
            return new
            {
                metrics.ActivityRisk,
                metrics.ActivityRiskWithStdDevCorrection,
                metrics.CriticalityRisk,
                metrics.FibonacciRisk,
                metrics.GeometricActivityRisk,
                metrics.GeometricCriticalityRisk,
                metrics.GeometricFibonacciRisk,
                metrics.NetworkCyclomaticComplexity,
                metrics.NetworkDuration,
                metrics.NetworkDurationManMonths,
                ProjectStartDate = Date(metrics.ProjectStartDate),
                metrics.ProjectFinish,
                metrics.EffortEfficiency,
                metrics.PeakHeadcount,
                metrics.AverageHeadcount,
                metrics.PeakToAverageHeadcount,
                metrics.ActivityEffort,
                metrics.DirectEffort,
                metrics.IndirectEffort,
                metrics.OtherEffort,
                metrics.TotalEffort,
                metrics.DirectCost,
                metrics.IndirectCost,
                metrics.OtherCost,
                metrics.TotalCost,
                metrics.DirectBilling,
                metrics.IndirectBilling,
                metrics.OtherBilling,
                metrics.TotalBilling,
                metrics.DirectMargin,
                metrics.IndirectMargin,
                metrics.OtherMargin,
                metrics.TotalMargin,
                metrics.DirectMarginAbsolute,
                metrics.IndirectMarginAbsolute,
                metrics.OtherMarginAbsolute,
                metrics.TotalMarginAbsolute,
            };
        }

        private static JToken BuildScenarioChartSeriesJson(ScenarioChartSeriesModel? series)
        {
            if (series is null)
            {
                return JValue.CreateNull();
            }

            return new JObject
            {
                [@"Metric"] = TrackedMetricsNames.Get(series.Metric),
                [@"CurveFitting"] = series.CurveFittingType.ToString(),
                [@"Absolute"] = series.AbsoluteCurveFitting,
                [@"Points"] = new JArray(series.Points.Select(x => new JObject
                {
                    [@"Id"] = x.Id.ToString(),
                    [@"Path"] = x.Path,
                    [@"X"] = x.X,
                    [@"Y"] = x.Y,
                })),
                [@"Fit"] = series.CurveFit is CurveFitModel fit
                    ? new JObject
                    {
                        [@"RSquared"] = fit.RSquared,
                        [@"Coefficients"] = new JArray(fit.Coefficients),
                        [@"DerivativeCoefficients"] = new JArray(fit.DerivativeCoefficients),
                    }
                    : JValue.CreateNull(),
            };
        }

        private static object BuildSimulationJson(JobSimulationMetrics simulation)
        {
            return new
            {
                simulation.Iterations,
                simulation.ValidIterations,
                simulation.FailedIterations,
                simulation.ResourceSchedulingStallIterations,
                simulation.MaximumTimeValueExceededIterations,
                simulation.InvalidConstraintsIterations,
                Seed = simulation.Seed.ToString(CultureInfo.InvariantCulture),
                Distribution = simulation.Distribution.ToString(),
                simulation.ModifiedBetaShape,
                simulation.PlannedFinishDays,
                PlannedFinishDate = Date(simulation.PlannedFinishDate),
                simulation.MeanDays,
                simulation.StandardDeviationDays,
                simulation.EarliestFinishDays,
                EarliestFinishDate = Date(simulation.EarliestFinishDate),
                simulation.LatestFinishDays,
                LatestFinishDate = Date(simulation.LatestFinishDate),
                Percentiles = simulation.Percentiles.Select(x => new { x.Percentile, x.FinishDays, FinishDate = Date(x.FinishDate) }).ToArray(),
                simulation.TargetFinishDays,
                TargetFinishDate = Date(simulation.TargetFinishDate),
                simulation.TargetProbability,
                MissedMaximumLatestFinishTimes = simulation.MissedMaximumLatestFinishTimes.Select(x => new { x.ActivityId, x.Name, x.Iterations }).ToArray(),
            };
        }

        private static string? Date(DateOnly? date)
        {
            return date?.ToString(@"yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        internal static ConsoleTable BuildScenarioTable(IEnumerable<ScenarioSummary> scenarios)
        {
            ConsoleTable table = CreateTable(
                Resource.ProjectPlan.Titles.Title_Scenario,
                Resource.ProjectPlan.Titles.Title_Id,
                Resource.ProjectPlan.Titles.Title_Tracked,
                Resource.ProjectPlan.Titles.Title_Current);

            foreach (ScenarioSummary scenario in scenarios)
            {
                table.AddRow(
                    scenario.Path,
                    scenario.Id,
                    scenario.IsTracked ? Resource.ProjectPlan.Labels.Label_Yes : string.Empty,
                    scenario.IsCurrent ? Resource.ProjectPlan.Symbols.Symbol_Current : string.Empty);
            }

            return table;
        }

        // The engine says why it could not select the scenario; zpp says it in
        // its own words, which point at the option that lists the scenarios.
        internal static string BuildScenarioSelectionMessage(ScenarioSelectionException ex)
        {
            string listScenarios = Program.OptionLongName(nameof(Options.ListScenarios));

            return ex.Failure switch
            {
                ScenarioSelectionFailure.NoMatch => string.Format(Resource.ProjectPlan.Messages.Message_NoScenarioMatches, ex.Selector, listScenarios),
                ScenarioSelectionFailure.SeveralMatches => string.Format(Resource.ProjectPlan.Messages.Message_SeveralScenariosMatch, ex.Selector, ex.MatchCount, listScenarios),
                ScenarioSelectionFailure.NoScenarioData => string.Format(Resource.ProjectPlan.Messages.Message_ScenarioHasNoScenarioData, ex.Selector),
                _ => ex.Message,
            };
        }
    }
}
