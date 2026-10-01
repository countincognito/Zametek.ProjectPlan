using ConsoleTables;
using Newtonsoft.Json;
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
        // and otherwise its metrics, in the format asked for - and returns the exit code the run ends with.
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
                        await console.WriteLineAsync(BuildMetricsJson(metrics));
                        break;
                    case MetricsExport.Table:
                        await console.DisplayAsync(BuildMetricsTable(metrics).ToString(), hasErrors: false);
                        break;
                    case MetricsExport.Markdown:
                    default:
                        await console.DisplayAsync(BuildMetricsTable(metrics).ToMarkDownString(), hasErrors: false);
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
        // select is described in zpp's words rather than the engine's.
        public static async Task<ExitCode> WriteFailureAsync(
            IJobConsole console,
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(console);
            ArgumentNullException.ThrowIfNull(exception);

            switch (exception)
            {
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

        // The metrics as a table, which prints as a table or as Markdown. This and the builders below are internal
        // rather than private so the test assembly can build the text it expects a run to print.
        internal static ConsoleTable BuildMetricsTable(JobMetrics metrics)
        {
            var table = new ConsoleTable(Resource.ProjectPlan.Titles.Title_Metrics, Resource.ProjectPlan.Titles.Title_Values);

            table.AddRow(Resource.ProjectPlan.Labels.Label_ActivityRisk, $@"{metrics.ActivityRisk:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_ActivityRiskWithStdDevCorrection, $@"{metrics.ActivityRiskWithStdDevCorrection:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_CriticalityRisk, $@"{metrics.CriticalityRisk:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_FibonacciRisk, $@"{metrics.FibonacciRisk:F2}");

            table.AddRow(Resource.ProjectPlan.Labels.Label_GeometricActivityRisk, $@"{metrics.GeometricActivityRisk:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_GeometricCriticalityRisk, $@"{metrics.GeometricCriticalityRisk:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_GeometricFibonacciRisk, $@"{metrics.GeometricFibonacciRisk:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_CyclomaticComplexity, $@"{metrics.NetworkCyclomaticComplexity}");

            table.AddRow(Resource.ProjectPlan.Labels.Label_ActivityEffort, $@"{metrics.ActivityEffort:F0}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_DurationManMonths, $@"{metrics.NetworkDurationManMonths:F1}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_ProjectFinish, $@"{metrics.ProjectFinish}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_EffortEfficiency, $@"{metrics.EffortEfficiency:F3}");

            table.AddRow(Resource.ProjectPlan.Labels.Label_DirectEffort, $@"{metrics.DirectEffort:F0}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_IndirectEffort, $@"{metrics.IndirectEffort:F0}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_OtherEffort, $@"{metrics.OtherEffort:F0}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_TotalEffort, $@"{metrics.TotalEffort:F0}");

            table.AddRow(Resource.ProjectPlan.Labels.Label_DirectCost, $@"{metrics.DirectCost:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_IndirectCost, $@"{metrics.IndirectCost:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_OtherCost, $@"{metrics.OtherCost:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_TotalCost, $@"{metrics.TotalCost:F2}");

            table.AddRow(Resource.ProjectPlan.Labels.Label_DirectBilling, $@"{metrics.DirectBilling:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_IndirectBilling, $@"{metrics.IndirectBilling:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_OtherBilling, $@"{metrics.OtherBilling:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_TotalBilling, $@"{metrics.TotalBilling:F2}");

            table.AddRow(Resource.ProjectPlan.Labels.Label_DirectMargin, $@"{metrics.DirectMarginAbsolute:F2}{metrics.DisplayDirectMargin}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_IndirectMargin, $@"{metrics.IndirectMarginAbsolute:F2}{metrics.DisplayIndirectMargin}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_OtherMargin, $@"{metrics.OtherMarginAbsolute:F2}{metrics.DisplayOtherMargin}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_TotalMargin, $@"{metrics.TotalMarginAbsolute:F2}{metrics.DisplayTotalMargin}");

            table.Configure(x =>
            {
                x.NumberAlignment = Alignment.Left;
                x.EnableCount = false;
            });

            return table;
        }

        internal static string BuildMetricsJson(JobMetrics metrics)
        {
            // Raw values rather than display strings wherever the contract offers
            // them: JSON numbers are culture-invariant by construction, and the
            // *Margin/*MarginAbsolute pairs carry the ratio and the currency value
            // that the table's display strings combine. ProjectFinish is the one
            // exception - the contract only exposes it as a display string.
            var output = new
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
                metrics.ProjectFinish,
                metrics.EffortEfficiency,
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

            // Json.NET indents with the platform's line end. JSON escapes the line breaks in its strings, so every one
            // left in the text is indentation.
            return NewLineHelper.NormalizeNewLines(JsonConvert.SerializeObject(output, Formatting.Indented));
        }

        internal static ConsoleTable BuildScenarioTable(IEnumerable<ScenarioSummary> scenarios)
        {
            var table = new ConsoleTable(
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

            table.Configure(x =>
            {
                x.NumberAlignment = Alignment.Left;
                x.EnableCount = false;
            });

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
