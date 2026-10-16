using Avalonia;
using ReactiveUI;
using ScottPlot;
using System.Diagnostics;
using System.Globalization;
using System.Reactive;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The panel that runs a Monte Carlo simulation of the schedule and shows what it found.
    /// </summary>
    /// <remarks>
    /// The panel is not part of the build cascade. A simulation costs about as much as a compile for each
    /// iteration, and it takes seconds to minutes, so it runs only when the user asks. It takes a copy of the
    /// plan under the locks of the core (the same rules as the snapshot of a compilation: refer to
    /// ARCHITECTURE section 7), runs on the task pool, and never touches the live activities again. The result
    /// is marked as stale when the plan compiles again, and the user runs it again if they want it.
    /// </remarks>
    public class ScheduleSimulationManagerViewModel
        : ToolViewModelBase, IScheduleSimulationManagerViewModel, IScottPlotViewModel
    {
        #region Fields

        private readonly Lock m_Lock;

        private static readonly IList<IFileFilter> s_ImageExportFileFilters =
            [
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ImageJpegFileType,
                    Patterns = [Resource.ProjectPlan.Filters.Filter_ImageJpegFilePattern]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ImagePngFileType,
                    Patterns = [Resource.ProjectPlan.Filters.Filter_ImagePngFilePattern]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ImageBmpFileType,
                    Patterns = [Resource.ProjectPlan.Filters.Filter_ImageBmpFilePattern]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ImageWebpFileType,
                    Patterns = [Resource.ProjectPlan.Filters.Filter_ImageWebpFilePattern]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ImageSvgFileType,
                    Patterns = [Resource.ProjectPlan.Filters.Filter_ImageSvgFilePattern]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_PdfFileType,
                    Patterns = [Resource.ProjectPlan.Filters.Filter_PdfFilePattern]
                }
            ];

        private static readonly IList<IFileFilter> s_CsvExportFileFilters =
            [
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_CsvFileType,
                    Patterns = [Resource.ProjectPlan.Filters.Filter_CsvFilePattern]
                }
            ];

        // The shape value that the modified beta-PERT distribution has when the user does not change it: the usual 4.
        private const double c_DefaultModifiedBetaShape = 4.0;

        // The progress is reported at most this often, in ticks of the stopwatch.
        private static readonly long s_ProgressInterval = Stopwatch.Frequency / 10;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly IScottPlotImageExporter m_ScottPlotImageExporter;

        // Reclaims the unmanaged Skia memory of each plot this view model replaces.
        private readonly PlotRetirer m_PlotRetirer;

        // The reactive pipelines, made by StartSubscriptions and not by the constructor: see IStartSubscriptions.
        private readonly SubscriptionLifetime m_SubscriptionLifetime = new();
        private IDisposable? m_BuildPresentationSub;

        // The run that is in progress, if there is one.
        private int m_RunGate;
        private CancellationTokenSource? m_RunCancellation;
        private long m_LastProgressTicks;

        // What the last run found, and what the CSV files need that the summary does not hold.
        private SimulationResult<int>? m_Result;
        private IReadOnlyDictionary<int, string> m_ActivityNames = new Dictionary<int, string>();

        #endregion

        #region Ctors

        public ScheduleSimulationManagerViewModel(
            ICoreViewModel coreViewModel,
            ISettingService settingService,
            IDialogService dialogService,
            IDateTimeCalculator dateTimeCalculator,
            IScottPlotImageExporter scottPlotImageExporter,
            IUIDispatcher uiDispatcher)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(scottPlotImageExporter);
            ArgumentNullException.ThrowIfNull(uiDispatcher);
            m_Lock = new();
            m_CoreViewModel = coreViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            m_DateTimeCalculator = dateTimeCalculator;
            m_ScottPlotImageExporter = scottPlotImageExporter;
            m_SimulationChartPlotModel = new Plot();
            m_PlotRetirer = new PlotRetirer(uiDispatcher);

            m_Iterations = SimulationSettings.DefaultIterations;
            m_UseRandomSeed = true;
            m_Seed = 1;
            m_Distribution = DurationDistribution.BetaPert;
            m_ModifiedBetaShape = c_DefaultModifiedBetaShape;
            m_StatusText = Resource.ProjectPlan.Labels.Label_SimulationNoResult;

            m_IsBusy = this
                .WhenAnyValue(ssm => ssm.m_CoreViewModel.IsBusy)
                .ToProperty(this, ssm => ssm.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(ssm => ssm.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, ssm => ssm.HasStaleOutputs);

            m_HasCompilationErrors = this
                .WhenAnyValue(ssm => ssm.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, ssm => ssm.HasCompilationErrors);

            m_IsModifiedBetaDistribution = this
                .WhenAnyValue(ssm => ssm.Distribution, (DurationDistribution distribution) => distribution == DurationDistribution.ModifiedBetaPert)
                .ToProperty(this, ssm => ssm.IsModifiedBetaDistribution);

            m_HasResult = this
                .WhenAnyValue(ssm => ssm.Summary, (SimulationSummaryModel? summary) => summary is not null && summary.HasResult)
                .ToProperty(this, ssm => ssm.HasResult);

            // The result belongs to the plan only while the plan has not compiled again.
            m_IsResultStale = this
                .WhenAnyValue(
                    ssm => ssm.HasResult,
                    ssm => ssm.ResultRevision,
                    ssm => ssm.m_CoreViewModel.CompilationOutputRevision,
                    ssm => ssm.m_CoreViewModel.HasStaleOutputs,
                    (hasResult, resultRevision, revision, hasStaleOutputs) => hasResult && (hasStaleOutputs || resultRevision != revision))
                .ToProperty(this, ssm => ssm.IsResultStale);

            // The commands come after the properties that they observe: a command reads them as soon as it is made.
            // A plan with no activities is told to the user when they ask, and not by a button that does nothing.
            IObservable<bool> canRun = this
                .WhenAnyValue(
                    ssm => ssm.IsRunning,
                    ssm => ssm.m_CoreViewModel.HasCompilationErrors,
                    (isRunning, hasErrors) => !isRunning && !hasErrors);

            RunSimulationCommand = ReactiveCommand.CreateFromTask(RunSimulationAsync, canRun);

            CancelSimulationCommand = ReactiveCommand.Create(
                CancelSimulation,
                this.WhenAnyValue(ssm => ssm.IsRunning));

            ResetSimulationChartCommand = ReactiveCommand.Create(ResetSimulationChart);

            {
                ReactiveCommand<RxVoid, RxVoid> saveSimulationChartImageFileCommand = ReactiveCommand.CreateFromTask(SaveSimulationChartImageFileAsync);
                SaveSimulationChartImageFileCommand = saveSimulationChartImageFileCommand;
            }

            {
                ReactiveCommand<RxVoid, RxVoid> exportCsvFilesCommand = ReactiveCommand.CreateFromTask(ExportCsvFilesAsync, this.WhenAnyValue(ssm => ssm.HasResult));
                ExportCsvFilesCommand = exportCsvFilesCommand;
            }

            Id = Resource.ProjectPlan.Titles.Title_ScheduleSimulation;
            Title = Resource.ProjectPlan.Titles.Title_ScheduleSimulation;
        }

        #endregion

        #region Properties

        private Plot m_SimulationChartPlotModel;
        public Plot SimulationChartPlotModel
        {
            get
            {
                return m_SimulationChartPlotModel;
            }
            private set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_SimulationChartPlotModel, value);
                }
            }
        }

        public object? ImageBounds { get; set; }

        // The revision of the compilation outputs that the plan was at when the result was made.
        private int m_ResultRevision;
        private int ResultRevision
        {
            get => m_ResultRevision;
            set => this.RaiseAndSetIfChanged(ref m_ResultRevision, value);
        }

        #endregion

        #region Private Methods

        private static ulong NewSeed()
        {
            Span<byte> bytes = stackalloc byte[sizeof(ulong)];
            RandomNumberGenerator.Fill(bytes);
            return BitConverter.ToUInt64(bytes);
        }

        // The first problem with the settings, or null when there is none.
        private string? FindInvalidSetting()
        {
            if (Iterations < 1 || Iterations > GraphLimits.MaximumSimulationIterations)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.ProjectPlan.Messages.Message_SimulationIterationsOutOfRange,
                    GraphLimits.MaximumSimulationIterations);
            }

            if (Distribution == DurationDistribution.ModifiedBetaPert
                && (double.IsNaN(ModifiedBetaShape) || double.IsInfinity(ModifiedBetaShape) || ModifiedBetaShape <= 0.0))
            {
                return Resource.ProjectPlan.Messages.Message_SimulationShapeOutOfRange;
            }

            return null;
        }

        private void ReportProgress(int done, int total, Stopwatch stopwatch)
        {
            long now = Stopwatch.GetTimestamp();
            long last = Interlocked.Read(ref m_LastProgressTicks);

            // Many threads report at the same time, and a report that came less than a tenth of a second after
            // the last one is dropped, unless it is the last of the run.
            if (done < total
                && (now - last < s_ProgressInterval || Interlocked.CompareExchange(ref m_LastProgressTicks, now, last) != last))
            {
                return;
            }

            double fraction = Math.Clamp(done / (double)total, 0.0, 1.0);
            TimeSpan elapsed = stopwatch.Elapsed;
            TimeSpan remaining = fraction > 0.0
                ? TimeSpan.FromTicks((long)(elapsed.Ticks * ((1.0 / fraction) - 1.0)))
                : TimeSpan.Zero;

            Progress = fraction;
            StatusText = string.Format(
                CultureInfo.CurrentCulture,
                Resource.ProjectPlan.Labels.Label_SimulationProgress,
                done,
                total,
                FormatDuration(remaining));
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalSeconds < 1.0)
            {
                return string.Format(CultureInfo.CurrentCulture, @"{0:0.0} s", duration.TotalSeconds);
            }
            if (duration.TotalMinutes < 1.0)
            {
                return string.Format(CultureInfo.CurrentCulture, @"{0:0} s", duration.TotalSeconds);
            }
            if (duration.TotalHours < 1.0)
            {
                return string.Format(CultureInfo.CurrentCulture, @"{0}:{1:00}", (int)duration.TotalMinutes, duration.Seconds);
            }
            return string.Format(CultureInfo.CurrentCulture, @"{0}:{1:00}:{2:00}", (int)duration.TotalHours, duration.Minutes, duration.Seconds);
        }

        // The simulation itself. It runs on the task pool: a copy of the plan, the simulator, and the statistics.
        private SimulationOutcome Simulate(
            SimulationSettings settings,
            int? targetFinishTime,
            IProgress<int> progress,
            CancellationToken cancellationToken)
        {
            SimulationInputModel input = m_CoreViewModel.CaptureSimulationInput();

            var simulator = new ScheduleSimulator<int, int, int>(
                input.Activities,
                input.Resources,
                input.WorkStreams,
                input.Estimates);

            if (simulator.CompilationErrors.Count > 0)
            {
                throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_SimulationCompilationErrors);
            }

            SimulationResult<int> result = simulator.Run(settings, progress, cancellationToken);

            SimulationSummaryModel summary = SimulationAnalyzer.Analyze(
                result,
                input.ActivityNames,
                simulator.PlannedFinishTime,
                targetFinishTime);

            return new SimulationOutcome(result, summary, input.ActivityNames, input.CompilationOutputRevision);
        }

        private sealed record SimulationOutcome(
            SimulationResult<int> Result,
            SimulationSummaryModel Summary,
            IReadOnlyDictionary<int, string> ActivityNames,
            int CompilationOutputRevision);

        // The finish time as the user reads it: the day number, and the date as well when the dates are on display.
        private string FormatFinish(int days, bool showDates, DateTimeOffset projectStart)
        {
            string number = days.ToString(CultureInfo.CurrentCulture);

            if (!showDates)
            {
                return number;
            }

            DateTimeOffset finish = m_DateTimeCalculator.AddDays(projectStart, days);
            string date = m_DateTimeCalculator.DisplayFinishDate(finish, finish, 1).ToString(DateTimeCalculator.DateFormat);
            return $@"{number} ({date})";
        }

        private List<SimulationStatisticModel> BuildStatistics(
            SimulationSummaryModel summary,
            bool showDates,
            DateTimeOffset projectStart)
        {
            var statistics = new List<SimulationStatisticModel>();

            void Add(string name, string value) => statistics.Add(new SimulationStatisticModel { Name = name, Value = value });

            Add(
                Resource.ProjectPlan.Labels.Label_SimulationIterationCount,
                summary.ValidIterationCount.ToString(CultureInfo.CurrentCulture));

            if (summary.FailedIterationCount > 0)
            {
                Add(
                    Resource.ProjectPlan.Labels.Label_SimulationFailedIterations,
                    summary.FailedIterationCount.ToString(CultureInfo.CurrentCulture));
            }

            if (!summary.HasResult)
            {
                return statistics;
            }

            Add(Resource.ProjectPlan.Labels.Label_SimulationPlannedFinish, FormatFinish(summary.PlannedFinishTime, showDates, projectStart));
            Add(Resource.ProjectPlan.Labels.Label_SimulationMean, summary.Mean.ToString(@"0.0", CultureInfo.CurrentCulture));
            Add(Resource.ProjectPlan.Labels.Label_SimulationStandardDeviation, summary.StandardDeviation.ToString(@"0.00", CultureInfo.CurrentCulture));
            Add(Resource.ProjectPlan.Labels.Label_SimulationEarliest, FormatFinish(summary.Minimum, showDates, projectStart));
            Add(Resource.ProjectPlan.Labels.Label_SimulationLatest, FormatFinish(summary.Maximum, showDates, projectStart));

            foreach (SimulationPercentileModel percentile in summary.Percentiles)
            {
                Add(
                    string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Labels.Label_SimulationPercentile, percentile.Percentile),
                    FormatFinish(percentile.FinishTime, showDates, projectStart));
            }

            if (summary.TargetFinishTime is int target && summary.TargetProbability is double probability)
            {
                Add(
                    $@"{Resource.ProjectPlan.Labels.Label_SimulationTargetProbability} ({FormatFinish(target, showDates, projectStart)})",
                    probability.ToString(@"P1", CultureInfo.CurrentCulture));
            }

            return statistics;
        }

        private void ResetSimulationChart()
        {
            SimulationChartPlotModel.Axes.AutoScale();
        }

        private async Task SaveSimulationChartImageFileAsync()
        {
            try
            {
                string title = m_SettingService.ProjectTitle;
                title = string.IsNullOrWhiteSpace(title) ? Resource.ProjectPlan.Titles.Title_UntitledProject : title;
                string outputFile = $@"{title}{Resource.ProjectPlan.Suffixes.Suffix_SimulationChart}";
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(outputFile, directory, s_ImageExportFileFilters);

                if (!string.IsNullOrWhiteSpace(filename)
                    && ImageBounds is Rect bounds)
                {
                    int boundedWidth = Math.Abs(Convert.ToInt32(bounds.Width));
                    int boundedHeight = Math.Abs(Convert.ToInt32(bounds.Height));
                    ChartImageFormat format = FileFormatHelper.GetChartImageFormat(filename);

                    await FileStreamHelper.SaveAsync(
                        filename,
                        stream => WriteSimulationChartImageAsync(stream, format, boundedWidth, boundedHeight));
                }
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        // The CSV files are written side by side, under one name that the user chooses: the name, then a word
        // for what the file holds.
        private async Task ExportCsvFilesAsync()
        {
            try
            {
                SimulationSummaryModel? summary;
                SimulationResult<int>? result;
                IReadOnlyDictionary<int, string> names;

                lock (m_Lock)
                {
                    summary = Summary;
                    result = m_Result;
                    names = m_ActivityNames;
                }

                if (summary is null || result is null)
                {
                    return;
                }

                string title = m_SettingService.ProjectTitle;
                title = string.IsNullOrWhiteSpace(title) ? Resource.ProjectPlan.Titles.Title_UntitledProject : title;
                string outputFile = $@"{title}{Resource.ProjectPlan.Suffixes.Suffix_SimulationCsv}.csv";
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(outputFile, directory, s_CsvExportFileFilters);

                if (string.IsNullOrWhiteSpace(filename))
                {
                    return;
                }

                string baseName = Path.Combine(
                    Path.GetDirectoryName(filename) ?? string.Empty,
                    Path.GetFileNameWithoutExtension(filename));

                (string Word, Action<TextWriter> Write)[] files =
                [
                    (@"statistics", writer => SimulationCsvWriter.WriteStatistics(writer, summary)),
                    (@"histogram", writer => SimulationCsvWriter.WriteHistogram(writer, summary)),
                    (@"paths", writer => SimulationCsvWriter.WritePaths(writer, summary)),
                    (@"activities", writer => SimulationCsvWriter.WriteActivities(writer, summary)),
                    (@"iterations", writer => SimulationCsvWriter.WriteIterations(writer, result, names)),
                ];

                await Task.Run(() =>
                {
                    foreach ((string word, Action<TextWriter> write) in files)
                    {
                        FileStreamHelper.Save(
                            $@"{baseName}-{word}.csv",
                            stream =>
                            {
                                // A byte order mark, so that Excel reads the names of the activities as UTF-8.
                                using var writer = new StreamWriter(stream, new UTF8Encoding(true), leaveOpen: true);
                                write(writer);
                            });
                    }
                });

                await m_DialogService.ShowInfoAsync(
                    Resource.ProjectPlan.Titles.Title_ScheduleSimulation,
                    string.Empty,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Resource.ProjectPlan.Messages.Message_SimulationFilesSaved,
                        files.Length,
                        Path.GetDirectoryName(filename) ?? string.Empty));
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        #endregion

        #region IScheduleSimulationManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        private int m_Iterations;
        public int Iterations
        {
            get => m_Iterations;
            set => this.RaiseAndSetIfChanged(ref m_Iterations, value);
        }

        private bool m_UseRandomSeed;
        public bool UseRandomSeed
        {
            get => m_UseRandomSeed;
            set => this.RaiseAndSetIfChanged(ref m_UseRandomSeed, value);
        }

        private ulong m_Seed;
        public ulong Seed
        {
            get => m_Seed;
            set => this.RaiseAndSetIfChanged(ref m_Seed, value);
        }

        private DurationDistribution m_Distribution;
        public DurationDistribution Distribution
        {
            get => m_Distribution;
            set => this.RaiseAndSetIfChanged(ref m_Distribution, value);
        }

        private readonly ObservableAsPropertyHelper<bool> m_IsModifiedBetaDistribution;
        public bool IsModifiedBetaDistribution => m_IsModifiedBetaDistribution.Value;

        private double m_ModifiedBetaShape;
        public double ModifiedBetaShape
        {
            get => m_ModifiedBetaShape;
            set => this.RaiseAndSetIfChanged(ref m_ModifiedBetaShape, value);
        }

        private int? m_TargetFinishTime;
        public int? TargetFinishTime
        {
            get => m_TargetFinishTime;
            set => this.RaiseAndSetIfChanged(ref m_TargetFinishTime, value);
        }

        private bool m_IsRunning;
        public bool IsRunning
        {
            get => m_IsRunning;
            private set => this.RaiseAndSetIfChanged(ref m_IsRunning, value);
        }

        private double m_Progress;
        public double Progress
        {
            get => m_Progress;
            private set => this.RaiseAndSetIfChanged(ref m_Progress, value);
        }

        private string m_StatusText;
        public string StatusText
        {
            get => m_StatusText;
            private set => this.RaiseAndSetIfChanged(ref m_StatusText, value);
        }

        private readonly ObservableAsPropertyHelper<bool> m_HasResult;
        public bool HasResult => m_HasResult.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsResultStale;
        public bool IsResultStale => m_IsResultStale.Value;

        private SimulationSummaryModel? m_Summary;
        public SimulationSummaryModel? Summary
        {
            get => m_Summary;
            private set => this.RaiseAndSetIfChanged(ref m_Summary, value);
        }

        private IReadOnlyList<SimulationStatisticModel> m_Statistics = [];
        public IReadOnlyList<SimulationStatisticModel> Statistics
        {
            get => m_Statistics;
            private set => this.RaiseAndSetIfChanged(ref m_Statistics, value);
        }

        private IReadOnlyList<SimulationPathModel> m_Paths = [];
        public IReadOnlyList<SimulationPathModel> Paths
        {
            get => m_Paths;
            private set => this.RaiseAndSetIfChanged(ref m_Paths, value);
        }

        private IReadOnlyList<SimulationActivityCriticalityModel> m_Activities = [];
        public IReadOnlyList<SimulationActivityCriticalityModel> Activities
        {
            get => m_Activities;
            private set => this.RaiseAndSetIfChanged(ref m_Activities, value);
        }

        public ICommand RunSimulationCommand { get; }

        public ICommand CancelSimulationCommand { get; }

        public ICommand ResetSimulationChartCommand { get; }

        public ICommand SaveSimulationChartImageFileCommand { get; }

        public ICommand ExportCsvFilesCommand { get; }

        public async Task RunSimulationAsync()
        {
            // One run at a time. A second request while a run is in progress does nothing.
            if (Interlocked.CompareExchange(ref m_RunGate, 1, 0) != 0)
            {
                return;
            }

            CancellationTokenSource? cancellation = null;

            try
            {
                string? invalidSetting = FindInvalidSetting();

                if (invalidSetting is not null)
                {
                    await ReportErrorAsync(invalidSetting);
                    return;
                }

                if (HasCompilationErrors)
                {
                    await ReportErrorAsync(Resource.ProjectPlan.Messages.Message_SimulationCompilationErrors);
                    return;
                }

                if (m_CoreViewModel.RawActivities.Count == 0)
                {
                    await ReportErrorAsync(Resource.ProjectPlan.Messages.Message_SimulationNoActivities);
                    return;
                }

                // The seed that the run uses is shown, so that a result the user likes can be made again.
                ulong seed = UseRandomSeed ? NewSeed() : Seed;
                Seed = seed;

                var settings = new SimulationSettings
                {
                    Iterations = Iterations,
                    Seed = seed,
                    Distribution = Distribution,
                    ModifiedBetaShape = Distribution == DurationDistribution.ModifiedBetaPert
                        ? ModifiedBetaShape
                        : c_DefaultModifiedBetaShape,
                    RecordCriticalActivities = true,
                    RecordCriticalPaths = true,
                };
                int? target = TargetFinishTime;

                cancellation = new CancellationTokenSource();
                lock (m_Lock)
                {
                    m_RunCancellation = cancellation;
                }

                IsRunning = true;
                Progress = 0.0;
                StatusText = string.Empty;
                Interlocked.Exchange(ref m_LastProgressTicks, 0);

                var stopwatch = Stopwatch.StartNew();
                var progress = new CallbackProgress(done => ReportProgress(done, settings.Iterations, stopwatch));
                CancellationToken token = cancellation.Token;

                SimulationOutcome outcome = await Task.Run(() => Simulate(settings, target, progress, token), token);

                stopwatch.Stop();

                lock (m_Lock)
                {
                    m_Result = outcome.Result;
                    m_ActivityNames = outcome.ActivityNames;
                }

                ResultRevision = outcome.CompilationOutputRevision;
                Summary = outcome.Summary;
                BuildPresentation();

                Progress = 1.0;
                StatusText = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.ProjectPlan.Labels.Label_SimulationDone,
                    FormatDuration(stopwatch.Elapsed));
            }
            catch (OperationCanceledException)
            {
                StatusText = Resource.ProjectPlan.Labels.Label_SimulationCancelled;
            }
            catch (Exception ex)
            {
                StatusText = string.Empty;
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
            finally
            {
                lock (m_Lock)
                {
                    m_RunCancellation = null;
                }
                cancellation?.Dispose();
                IsRunning = false;
                Volatile.Write(ref m_RunGate, 0);
            }
        }

        public void CancelSimulation()
        {
            CancellationTokenSource? cancellation;

            lock (m_Lock)
            {
                cancellation = m_RunCancellation;
            }

            try
            {
                cancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The run finished while the request was on its way, and there is nothing left to cancel.
            }
        }

        public async Task WriteSimulationChartImageAsync(
            Stream stream,
            ChartImageFormat format,
            int width,
            int height)
        {
            await m_ScottPlotImageExporter.WritePlotImageAsync(SimulationChartPlotModel, stream, format, width, height);
        }

        // Presents the summary that the view model holds: the statistics as text, the tables and the chart. This
        // runs when a run finishes, and again when the display settings change what the numbers look like.
        private void BuildPresentation()
        {
            SimulationSummaryModel? summary = Summary;

            if (summary is null)
            {
                Statistics = [];
                Paths = [];
                Activities = [];
            }
            else
            {
                Statistics = BuildStatistics(
                    summary,
                    m_CoreViewModel.DisplaySettingsViewModel.ShowDates,
                    m_CoreViewModel.ProjectStart);
                Paths = summary.Paths;
                Activities = summary.Activities;
            }

            BuildSimulationChartPlotModel();
        }

        public void BuildSimulationChartPlotModel()
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(ScheduleSimulationManagerViewModel)}.{nameof(BuildSimulationChartPlotModel)}");
            Plot? plotModel = null;

            lock (m_Lock)
            {
                SimulationSummaryModel? summary = Summary;

                if (summary is not null && summary.HasResult)
                {
                    plotModel = SimulationChartBuilder.Build(
                        summary,
                        m_DateTimeCalculator,
                        m_CoreViewModel.DisplaySettingsViewModel.ShowDates,
                        m_CoreViewModel.ProjectStart,
                        m_CoreViewModel.BaseTheme);
                }
            }

            plotModel ??= new Plot();
            Plot outgoing = SimulationChartPlotModel;
            SimulationChartPlotModel = plotModel;
            m_PlotRetirer.Retire(outgoing);
        }

        private sealed class CallbackProgress
            : IProgress<int>
        {
            private readonly Action<int> m_Callback;

            public CallbackProgress(Action<int> callback)
            {
                m_Callback = callback;
            }

            public void Report(int value) => m_Callback(value);
        }

        #endregion

        #region IScottPlotViewModel Members

        public async Task<byte[]?> RenderChartImageAsync()
        {
            if (ImageBounds is not Rect bounds)
            {
                return null;
            }

            int width = Math.Abs(Convert.ToInt32(bounds.Width));
            int height = Math.Abs(Convert.ToInt32(bounds.Height));

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            return await m_ScottPlotImageExporter.RenderPlotImageAsync(SimulationChartPlotModel, width, height);
        }

        public Task ReportErrorAsync(string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            return m_DialogService.ShowErrorAsync(
                Resource.ProjectPlan.Titles.Title_Error,
                string.Empty,
                message);
        }

        #endregion

        #region IStartSubscriptions Members

        public void StartSubscriptions()
        {
            if (!m_SubscriptionLifetime.TryStart())
            {
                return;
            }

            // The numbers on display depend on the display settings (days or dates, the working calendar, the
            // theme), and a result that is already there is presented again when they change.
            m_BuildPresentationSub = this
                .WhenAnyValue(
                    ssm => ssm.m_CoreViewModel.DisplaySettingsViewModel.ShowDates,
                    ssm => ssm.m_CoreViewModel.DisplaySettingsViewModel.UseClassicDates,
                    ssm => ssm.m_CoreViewModel.DisplaySettingsViewModel.NonWorkingDayMode,
                    ssm => ssm.m_CoreViewModel.ProjectStart,
                    ssm => ssm.m_CoreViewModel.BaseTheme,
                    (showDates, _, _, _, _) => showDates)
                .MuteWhile(this.WhenAnyValue(ssm => ssm.m_CoreViewModel.IsBulkUpdating)) // Conflate redundant notifications while a project scenario is loaded/reset.
                .ObserveOn(RxSchedulers.TaskpoolScheduler)
                .Subscribe(_ => BuildPresentation());
        }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
            m_SubscriptionLifetime.Kill();
            m_BuildPresentationSub?.Dispose();
            CancelSimulation();
        }

        #endregion

        #region IDisposable Members

        private bool m_Disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (m_Disposed)
            {
                return;
            }

            if (disposing)
            {
                KillSubscriptions();
                m_PlotRetirer.Dispose();
                m_SimulationChartPlotModel.Dispose();
                m_IsBusy?.Dispose();
                m_HasStaleOutputs?.Dispose();
                m_HasCompilationErrors?.Dispose();
                m_IsModifiedBetaDistribution?.Dispose();
                m_HasResult?.Dispose();
                m_IsResultStale?.Dispose();
            }

            m_Disposed = true;
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
