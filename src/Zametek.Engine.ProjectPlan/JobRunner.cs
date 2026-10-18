using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Engine.ProjectPlan
{
    /// <summary>
    /// Runs plans through the view models, headless: open or import the plan, compile it, build its outputs, and hand
    /// each output to the job's sink. Every job gets a scope of its own - the project, the view models and everything
    /// that depends on them - which is disposed when the job ends, so one job leaves nothing behind for the next.
    /// </summary>
    public class JobRunner
    {
        #region Fields

        // The CSV files of a simulation, each with the output it is, in the order the job produces them.
        private static readonly (SimulationCsvFile File, JobOutput Output)[] s_SimulationCsvOutputs =
        [
            (SimulationCsvFile.Statistics, JobOutput.SimulationStatistics),
            (SimulationCsvFile.Histogram, JobOutput.SimulationHistogram),
            (SimulationCsvFile.Paths, JobOutput.SimulationPaths),
            (SimulationCsvFile.Activities, JobOutput.SimulationActivities),
            (SimulationCsvFile.Iterations, JobOutput.SimulationIterations),
        ];

        private readonly IServiceScopeFactory m_ScopeFactory;

        // The processors, shared by the simulations that the jobs of this runner run at one time.
        private readonly SimulationProcessorShare m_ProcessorShare;

        #endregion

        #region Ctors

        public JobRunner(IServiceScopeFactory scopeFactory)
            : this(scopeFactory, new SimulationProcessorShare())
        {
        }

        // A runner whose jobs share the processors as the given share says. A test uses it to share fewer, or more, than the
        // machine has.
        internal JobRunner(
            IServiceScopeFactory scopeFactory,
            SimulationProcessorShare processorShare)
        {
            ArgumentNullException.ThrowIfNull(scopeFactory);
            ArgumentNullException.ThrowIfNull(processorShare);
            m_ScopeFactory = scopeFactory;
            m_ProcessorShare = processorShare;
        }

        #endregion

        #region Public Members

        /// <summary>
        /// Lists the scenarios of a project file, without processing any of them. A file the engine cannot read is
        /// <see cref="ProjectNotReadableException"/>.
        /// </summary>
        public async Task<IReadOnlyList<ScenarioSummary>> ListScenariosAsync(
            Stream input,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(input);
            cancellationToken.ThrowIfCancellationRequested();

            await using AsyncServiceScope scope = m_ScopeFactory.CreateAsyncScope();
            IProjectFileOpen projectFileOpen = scope.ServiceProvider.GetRequiredService<IProjectFileOpen>();
            ProjectModel projectModel = await OpenProjectAsync(projectFileOpen, input);
            return ScenarioSelector.ListScenarios(projectModel);
        }

        /// <summary>
        /// Runs one job. A chart or graph that cannot be produced does not stop it: the error is reported through the
        /// sink, as the desktop reports it in a dialog, the remaining outputs are still produced, and the job completes
        /// with errors. Anything else the job cannot get past, it throws - an input it cannot read
        /// (<see cref="ProjectNotReadableException"/>), a scenario it cannot select
        /// (<see cref="ScenarioSelectionException"/>), a compilation that runs out of time
        /// (<see cref="GraphCompilationTimeoutException"/>), or a project or scenario export the sink cannot store -
        /// and whatever the sink stored before that stays where it is. Cancelling the token stops the job before the
        /// next step it would take, with an <see cref="OperationCanceledException"/>: a step already under way - reading
        /// the plan, compiling it, producing an output - runs to its end first, and a job that has produced its last
        /// output completes. A simulation is the exception: it stops between two chunks of its iterations, and the job stops
        /// with it. The settings of a simulation are the scenario's, and so a request that asks of it what the scenario
        /// cannot give (its outputs without a result, or a run with more iterations than it may have) is found out only
        /// after the plan is in, and it is a <see cref="SimulationException"/>.
        /// </summary>
        public async Task<JobResult> RunAsync(
            JobRequest request,
            IJobSink sink,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(sink);
            cancellationToken.ThrowIfCancellationRequested();

            await using AsyncServiceScope scope = m_ScopeFactory.CreateAsyncScope();
            IServiceProvider services = scope.ServiceProvider;

            // First, so that whatever the job reports reaches this job's sink, and whatever it stamps is stamped
            // with the time it runs at.
            JobDialogService dialogService = services.GetRequiredService<JobDialogService>();
            dialogService.Sink = sink;
            services.GetRequiredService<JobClock>().Now = request.Now;

            return await RunAsync(request, sink, dialogService, services, m_ProcessorShare, cancellationToken);
        }

        #endregion

        #region Private Members

        // The project in the stream or, when the engine cannot read it, ProjectNotReadableException, in the reader's own
        // words.
        private static async Task<ProjectModel> OpenProjectAsync(
            IProjectFileOpen projectFileOpen,
            Stream input)
        {
            try
            {
                return await projectFileOpen.OpenProjectFileAsync(input);
            }
            catch (Exception ex) when (IsUnreadable(ex))
            {
                throw new ProjectNotReadableException(ex.Message, ex);
            }
        }

        // The workbook's scenario or, when the engine cannot read it, ProjectNotReadableException.
        private static ProjectScenarioImportModel ImportProjectScenario(
            IProjectScenarioFileImport projectFileImport,
            Stream input,
            ProjectScenarioImportFormat importFormat)
        {
            try
            {
                return projectFileImport.ImportProjectScenarioFile(input, importFormat);
            }
            catch (Exception ex) when (IsUnreadable(ex))
            {
                throw new ProjectNotReadableException(ex.Message, ex);
            }
        }

        // Whether an exception from a reader says that the file cannot be read. A job that was cancelled, or that ran out
        // of memory, has not met a file it cannot read.
        private static bool IsUnreadable(Exception ex)
        {
            return ex is not (OperationCanceledException or OutOfMemoryException);
        }

        private static async Task<JobResult> RunAsync(
            JobRequest request,
            IJobSink sink,
            JobDialogService dialogService,
            IServiceProvider services,
            SimulationProcessorShare processorShare,
            CancellationToken cancellationToken)
        {
            // The view models are resolved and never started (see IStartSubscriptions): a job invokes every build
            // step itself, so no reactive pipeline of theirs exists to run alongside it.
            IProjectScenarioManagerViewModel project = services.GetRequiredService<IProjectScenarioManagerViewModel>();
            ICoreViewModel core = services.GetRequiredService<ICoreViewModel>();
            IMetricManagerViewModel metrics = services.GetRequiredService<IMetricManagerViewModel>();
            IOutputManagerViewModel outputs = services.GetRequiredService<IOutputManagerViewModel>();

            ISettingService settingService = services.GetRequiredService<ISettingService>();

            // Applied before anything is loaded, because opening a project compiles
            // the scenario it lands on.
            settingService.CompilationTimeoutMilliseconds = request.CompileTimeoutMilliseconds;

            core.AutoCompile = false;

            // Plan in.
            if (request.ImportFormat is ProjectScenarioImportFormat importFormat)
            {
                // An import lands in the current scenario of the open project.
                // The desktop always has one - every new project starts with a
                // Base scenario - so start the project the same way here.
                // Without it the import would reach the core but not the
                // project, and a saved project would hold no scenario at all.
                project.ResetProject();

                // Read after the reset, which gives the Base scenario a new id.
                IProjectScenarioFileImport projectFileImport = services.GetRequiredService<IProjectScenarioFileImport>();
                Guid projectScenarioId = settingService.ScenarioId;
                string projectScenarioTitle = settingService.ScenarioTitle;
                ProjectScenarioImportModel projectImport = ImportProjectScenario(projectFileImport, request.Input, importFormat);

                core.ProcessProjectScenarioImport(projectImport, projectScenarioId, projectScenarioTitle);
            }
            else
            {
                IProjectFileOpen projectFileOpen = services.GetRequiredService<IProjectFileOpen>();
                ProjectModel projectModel = await OpenProjectAsync(projectFileOpen, request.Input);

                if (request.Scenario is not null)
                {
                    // Re-point the project's current-scenario marker before any
                    // processing: ProcessProject loads whichever scenario Current
                    // names, so this is the entire mechanism of scenario selection.
                    projectModel = projectModel with { Current = ScenarioSelector.ResolveScenarioId(projectModel, request.Scenario) };
                }

                project.ProcessProject(projectModel);
            }

            // Base theme.
            {
                core.BaseTheme = request.BaseTheme;
            }

            // Simulation settings. They are the scenario's, and the request can give others in their place. What the request
            // asks of the simulation is checked now, before the plan compiles, so that a request that cannot be done fails
            // at once.
            {
                if (request.SimulationSettings is SimulationSettingsModel simulationSettings)
                {
                    core.SimulationSettings = simulationSettings;
                }

                ValidateSimulation(request, core);
            }

            // Compile.
            {
                cancellationToken.ThrowIfCancellationRequested();

                // We do not need to set IsReadyToReviseTrackers since this is a one step
                // process (i.e. we are not changing any tracker UI elements).

                core.RunCompile();
                outputs.BuildCompilationOutput();

                if (core.HasCompilationErrors)
                {
                    return new JobResult
                    {
                        Status = JobStatus.CompilationErrors,
                        CompilationOutput = outputs.CompilationOutput,
                        CompilationErrors = [.. core.GraphCompilation.CompilationErrors.Select(x => new JobCompilationError(x.ErrorCode.ToString(), x.ErrorMessage))],
                    };
                }

                // Mirrors the order of CoreViewModel.RunBuildCascade, which is what
                // builds these outputs in the desktop app after each compile.
                core.BuildArrowGraph();
                core.BuildVertexGraph();
                core.BuildResourceSeriesSet();
                core.BuildTrackingSeriesSet();
                core.BuildNetworkMetrics();
                core.BuildRiskMetrics();
                core.BuildFinancialMetrics();
            }

            // Simulation. A run goes before the project and the scenario are written, so that they hold its result, and before
            // the other outputs. The outputs of the simulation come last, and the statistics of a result that the scenario
            // held are there without a run.
            ISimulationChartManagerViewModel? simulation = null;
            bool simulationRan = false;
            bool hasSimulationResult = false;

            if (request.RunSimulation)
            {
                cancellationToken.ThrowIfCancellationRequested();

                simulation = services.GetRequiredService<ISimulationChartManagerViewModel>();
                SimulationSummaryModel? earlier = core.SimulationSummary;

                // The threads of the run: those the request limits it to, or else its share of the processors with the other
                // simulations that run now. The result is the same for any number of them.
                using SimulationProcessorShare.Lease share = processorShare.Begin();
                simulation.MaxDegreeOfParallelism = request.MaximumSimulationThreads ?? share.Threads;

                int iterations = core.SimulationSettings.Iterations;
                IProgress<int>? progress = request.Progress is IProgress<JobProgress> observer
                    ? new IterationProgress(done => observer.Report(new JobProgress(done, iterations)))
                    : null;

                await simulation.RunSimulationWithSavedSeedAsync(cancellationToken, progress);

                // A run that was cancelled has no result, and the job stops with it. A run that failed has told the dialog
                // service why, and the result that the scenario had before it is not the result of this job.
                cancellationToken.ThrowIfCancellationRequested();

                simulationRan = true;
                hasSimulationResult = core.SimulationSummary is not null
                    && !ReferenceEquals(core.SimulationSummary, earlier);
            }
            else if (core.SimulationSummary is not null)
            {
                simulation = services.GetRequiredService<ISimulationChartManagerViewModel>();
                simulation.BuildPresentation();
                hasSimulationResult = true;
            }

            // Project and scenario out. Either one failing stops the job.
            {
                if (request.SaveProject)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    IProjectFileSave projectFileSave = services.GetRequiredService<IProjectFileSave>();
                    ProjectModel projectModel = project.BuildProject();
                    await sink.WriteOutputAsync(JobOutput.Project, stream => projectFileSave.SaveProjectFileAsync(projectModel, stream));
                }
                if (request.ExportFormat is ProjectScenarioExportFormat exportFormat)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    IProjectScenarioFileExport projectFileExport = services.GetRequiredService<IProjectScenarioFileExport>();
                    ProjectScenarioModel projectScenarioModel = core.BuildProjectScenario();
                    await sink.WriteOutputAsync(
                        JobOutput.ScenarioExport,
                        stream =>
                        {
                            projectFileExport.ExportProjectScenarioFile(
                                projectScenarioModel,
                                core.ResourceSeriesSet,
                                core.TrackingSeriesSet,
                                core.DisplaySettingsViewModel.ShowDates,
                                stream,
                                exportFormat);
                            return Task.CompletedTask;
                        });
                }
            }

            // Chart and graph exports. Each manager view model is resolved only
            // when its export was requested - construction is not free, and a
            // typical run wants at most one or two of them.

            // Gantt chart export.
            if (request.GanttChart is ChartOutputRequest ganttChart)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IGanttChartManagerViewModel gantt = services.GetRequiredService<IGanttChartManagerViewModel>();

                await WriteChartAsync(
                    dialogService,
                    sink,
                    JobOutput.GanttChart,
                    ganttChart,
                    gantt.BuildGanttChartPlotModel,
                    gantt.WriteGanttChartImageAsync,
                    cancellationToken);
            }

            // Arrow graph export.
            if (request.ArrowGraph is GraphOutputRequest arrowGraph)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IArrowGraphManagerViewModel arrow = services.GetRequiredService<IArrowGraphManagerViewModel>();

                await WriteGraphAsync(
                    dialogService,
                    sink,
                    JobOutput.ArrowGraph,
                    arrowGraph,
                    arrow.WriteFixedLayoutArrowGraphImageAsync,
                    cancellationToken);
            }

            // Vertex graph export.
            if (request.VertexGraph is GraphOutputRequest vertexGraph)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IVertexGraphManagerViewModel vertex = services.GetRequiredService<IVertexGraphManagerViewModel>();

                await WriteGraphAsync(
                    dialogService,
                    sink,
                    JobOutput.VertexGraph,
                    vertexGraph,
                    vertex.WriteFixedLayoutVertexGraphImageAsync,
                    cancellationToken);
            }

            // Resource chart export.
            if (request.ResourceChart is ChartOutputRequest resourceChart)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IResourceChartManagerViewModel resources = services.GetRequiredService<IResourceChartManagerViewModel>();

                await WriteChartAsync(
                    dialogService,
                    sink,
                    JobOutput.ResourceChart,
                    resourceChart,
                    resources.BuildResourceChartPlotModel,
                    resources.WriteResourceChartImageAsync,
                    cancellationToken);
            }

            // EV chart export.
            if (request.EarnedValueChart is ChartOutputRequest earnedValueChart)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IEarnedValueChartManagerViewModel ev = services.GetRequiredService<IEarnedValueChartManagerViewModel>();

                await WriteChartAsync(
                    dialogService,
                    sink,
                    JobOutput.EarnedValueChart,
                    earnedValueChart,
                    ev.BuildEarnedValueChartPlotModel,
                    ev.WriteEarnedValueChartImageAsync,
                    cancellationToken);
            }

            // Scenario chart export.
            if (request.ScenarioChart is ChartOutputRequest scenarioChart)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IScenarioChartManagerViewModel scenarios = services.GetRequiredService<IScenarioChartManagerViewModel>();

                // The tracked-metrics set the chart plots is normally assembled by
                // a reactive pipeline that a job never starts, so build it
                // explicitly first.
                project.BuildTrackedMetrics();

                await WriteChartAsync(
                    dialogService,
                    sink,
                    JobOutput.ScenarioChart,
                    scenarioChart,
                    scenarios.BuildScenarioChartPlotModel,
                    scenarios.WriteScenarioChartImageAsync,
                    cancellationToken);
            }

            // The chart and the CSV files of the simulation, and its statistics. A result that a run of the job made is the one they
            // come from, or else the one that the scenario held. A project file does not hold the iterations of a run, and so a
            // job that did not run the simulation has no file of them.
            JobSimulationMetrics? simulationMetrics = null;

            if (simulation is not null
                && hasSimulationResult)
            {
                simulationMetrics = JobSimulationMetrics.From(simulation);

                if (request.SimulationChart is ChartOutputRequest simulationChart)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await WriteChartAsync(
                        dialogService,
                        sink,
                        JobOutput.SimulationChart,
                        simulationChart,
                        simulation.BuildSimulationChartPlotModel,
                        simulation.WriteSimulationChartImageAsync,
                        cancellationToken);
                }

                foreach ((SimulationCsvFile file, JobOutput output) in s_SimulationCsvOutputs)
                {
                    if (request.SimulationCsvFiles.HasFlag(file)
                        && (simulationRan || file != SimulationCsvFile.Iterations))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        await WriteReportedOutputAsync(
                            dialogService,
                            sink,
                            output,
                            stream => simulation.WriteSimulationCsvAsync(stream, file),
                            cancellationToken);
                    }
                }
            }

            // A chart or graph export that fails does not throw: WriteReportedOutputAsync
            // reports the failure through the dialog service, as the desktop does. The
            // remaining outputs have still been produced, but the job as a whole has failed.
            return new JobResult
            {
                Status = dialogService.HasShownErrors ? JobStatus.CompletedWithErrors : JobStatus.Succeeded,
                Metrics = JobMetrics.From(metrics),
                Simulation = simulationMetrics,
            };
        }

        // What the request asks of the simulation, checked against the scenario that the job has loaded. A run needs settings that
        // a run can use, and no more iterations than the request allows. The outputs of a simulation need a result: the run of
        // the job, or one that the scenario holds.
        private static void ValidateSimulation(
            JobRequest request,
            ICoreViewModel core)
        {
            if (request.RunSimulation)
            {
                SimulationSettingsModel settings = core.SimulationSettings;

                if (settings.Iterations < 1
                    || settings.Iterations > GraphLimits.MaximumSimulationIterations)
                {
                    throw new SimulationException(
                        string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_SimulationIterationsOutOfRange, GraphLimits.MaximumSimulationIterations),
                        SimulationFailure.InvalidSettings);
                }

                if (settings.Distribution == DurationDistribution.ModifiedBetaPert
                    && (double.IsNaN(settings.ModifiedBetaShape) || double.IsInfinity(settings.ModifiedBetaShape) || settings.ModifiedBetaShape <= 0.0))
                {
                    throw new SimulationException(Resource.ProjectPlan.Messages.Message_SimulationShapeOutOfRange, SimulationFailure.InvalidSettings);
                }

                if (request.MaximumSimulationThreads is int threads
                    && (threads < -1 || threads == 0))
                {
                    throw new SimulationException(Resource.ProjectPlan.Messages.Message_SimulationThreadsOutOfRange, SimulationFailure.InvalidSettings);
                }

                if (request.MaximumSimulationIterations is int maximum
                    && settings.Iterations > maximum)
                {
                    throw new SimulationException(
                        string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_SimulationIterationsAboveLimit, settings.Iterations, maximum),
                        SimulationFailure.IterationsAboveLimit);
                }

                return;
            }

            if ((request.SimulationChart is not null || request.SimulationCsvFiles != SimulationCsvFile.None)
                && core.SimulationSummary is null)
            {
                throw new SimulationException(Resource.ProjectPlan.Messages.Message_SimulationOutputsNeedAResult, SimulationFailure.NoResult);
            }
        }

        // The iterations that have run, as a callback: Progress<T> would post them to the context that made it.
        private sealed class IterationProgress
            : IProgress<int>
        {
            private readonly Action<int> m_Callback;

            public IterationProgress(Action<int> callback)
            {
                m_Callback = callback;
            }

            public void Report(int value) => m_Callback(value);
        }

        private static async Task WriteChartAsync(
            IDialogService dialogService,
            IJobSink sink,
            JobOutput output,
            ChartOutputRequest chart,
            Action buildPlotModel,
            Func<Stream, ChartImageFormat, int, int, Task> writePlotImageAsync,
            CancellationToken cancellationToken)
        {
            buildPlotModel();

            await WriteReportedOutputAsync(
                dialogService,
                sink,
                output,
                stream => writePlotImageAsync(stream, chart.Format, chart.Width, chart.Height),
                cancellationToken);
        }

        private static async Task WriteGraphAsync(
            IDialogService dialogService,
            IJobSink sink,
            JobOutput output,
            GraphOutputRequest graph,
            Func<Stream, GraphExportFormat, Task> writeGraphImageAsync,
            CancellationToken cancellationToken)
        {
            await WriteReportedOutputAsync(
                dialogService,
                sink,
                output,
                stream => writeGraphImageAsync(stream, graph.Format),
                cancellationToken);
        }

        // A chart or graph export that fails is reported rather than thrown, as the desktop reports it in a dialog and
        // carries on, so that the remaining exports still run and the metrics are still taken. The job then completes
        // with errors. A job being cancelled is not an export failing: if the sink gives up on an output because the job
        // was cancelled, the job stops.
        private static async Task WriteReportedOutputAsync(
            IDialogService dialogService,
            IJobSink sink,
            JobOutput output,
            Func<Stream, Task> write,
            CancellationToken cancellationToken)
        {
            try
            {
                await sink.WriteOutputAsync(output, write);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                await dialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        #endregion
    }
}
