using Avalonia;
using Microsoft.Extensions.Logging;
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

        // The progress is written to the log at most this often. The log is how a run of zpp, or of a server, which has no
        // progress bar, shows that it has not stopped.
        private static readonly long s_LogInterval = 2 * Stopwatch.Frequency;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly IScottPlotImageExporter m_ScottPlotImageExporter;

        // What a bound control shows can only be changed on the UI thread. A run reports its progress from the threads
        // of the simulation, and the presentation is made on the task pool, and so what they produce is handed to
        // the UI thread here, in order.
        private readonly IUIDispatcher m_UiDispatcher;

        private readonly ILogger<ScheduleSimulationManagerViewModel> m_Logger;
        private long m_LastLogTicks;

        // Reclaims the unmanaged Skia memory of each plot this view model replaces.
        private readonly PlotRetirer m_PlotRetirer;

        // The reactive pipelines, made by StartSubscriptions and not by the constructor: see IStartSubscriptions.
        private readonly SubscriptionLifetime m_SubscriptionLifetime = new();
        private IDisposable? m_BuildPresentationSub;
        private IDisposable? m_SettingsSub;
        private IDisposable? m_SummarySub;
        private IDisposable? m_ScenarioSub;

        // The run that is in progress, if there is one.
        private int m_RunGate;
        private CancellationTokenSource? m_RunCancellation;
        private long m_LastProgressTicks;

        // What the last run of this session found, and what the CSV files need that the summary does not hold. The
        // iterations are not in a project file, and so a result that was loaded has none of them: m_ResultSummary is
        // the summary that m_Result belongs to.
        private SimulationResult<int>? m_Result;
        private SimulationSummaryModel? m_ResultSummary;
        private IReadOnlyDictionary<int, string> m_ActivityNames = new Dictionary<int, string>();

        // The summary that the tables and the chart show, and the revision of the scenario that the run in progress
        // belongs to.
        private SimulationSummaryModel? m_PresentedSummary;
        private int m_RunScenarioRevision;

        #endregion

        #region Ctors

        public ScheduleSimulationManagerViewModel(
            ICoreViewModel coreViewModel,
            ISettingService settingService,
            IDialogService dialogService,
            IDateTimeCalculator dateTimeCalculator,
            IScottPlotImageExporter scottPlotImageExporter,
            IUIDispatcher uiDispatcher,
            ILogger<ScheduleSimulationManagerViewModel> logger)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(scottPlotImageExporter);
            ArgumentNullException.ThrowIfNull(uiDispatcher);
            ArgumentNullException.ThrowIfNull(logger);
            m_Lock = new();
            m_CoreViewModel = coreViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            m_DateTimeCalculator = dateTimeCalculator;
            m_ScottPlotImageExporter = scottPlotImageExporter;
            m_UiDispatcher = uiDispatcher;
            m_Logger = logger;
            m_SimulationChartPlotModel = new Plot();
            m_PlotRetirer = new PlotRetirer(uiDispatcher);

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
                .WhenAnyValue(ssm => ssm.m_CoreViewModel.SimulationSettings, (SimulationSettingsModel settings) => settings.Distribution == DurationDistribution.ModifiedBetaPert)
                .ToProperty(this, ssm => ssm.IsModifiedBetaDistribution);

            m_ShowDates = this
                .WhenAnyValue(ssm => ssm.m_CoreViewModel.DisplaySettingsViewModel.ShowDates)
                .ToProperty(this, ssm => ssm.ShowDates);

            m_HasResult = this
                .WhenAnyValue(ssm => ssm.m_CoreViewModel.SimulationSummary, (SimulationSummaryModel? summary) => summary is not null && summary.HasResult)
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

            RunSimulationCommand = ReactiveCommand.CreateFromTask(() => RunSimulationAsync(), canRun);

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

            string text = string.Format(
                CultureInfo.CurrentCulture,
                Resource.ProjectPlan.Labels.Label_SimulationProgress,
                done,
                total,
                FormatDuration(remaining));

            SetStatus(fraction, text);

            // Rarely, and not at the end, which the log says in its own words.
            long lastLog = Interlocked.Read(ref m_LastLogTicks);
            if (done < total
                && now - lastLog >= s_LogInterval
                && Interlocked.CompareExchange(ref m_LastLogTicks, now, lastLog) == lastLog)
            {
                m_Logger.LogInformation("Simulation: {Progress}", text);
            }
        }

        // The progress and what it says change together, on the UI thread, and in the order in which they were
        // reported: the last word of a run (done, or cancelled) must not be overwritten by a report that was still
        // on its way.
        private void SetStatus(double progress, string text)
        {
            m_UiDispatcher.Defer(() =>
            {
                Progress = progress;
                StatusText = text;
            });
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

        // The date on which a project that takes the given days finishes, as the display shows it: the day that the
        // last of its days ends on, in the working calendar of the settings.
        private DateTimeOffset FinishDateOf(int days, DateTimeOffset projectStart)
        {
            DateTimeOffset finish = m_DateTimeCalculator.AddDays(projectStart, days);
            return m_DateTimeCalculator.DisplayFinishDate(finish, finish, 1);
        }

        // The finish time of the first day that finishes on the given date or after it: the inverse of FinishDateOf.
        // The finish dates never go back as the days go up, and so a search that halves the range finds it.
        private int DaysOfFinishDate(DateTimeOffset date)
        {
            DateTimeOffset projectStart = m_CoreViewModel.ProjectStart;
            int low = 0;
            int high = GraphLimits.MaximumTimeValue;

            while (low < high)
            {
                int middle = low + ((high - low) / 2);

                if (FinishDateOf(middle, projectStart).Date >= date.Date)
                {
                    high = middle;
                }
                else
                {
                    low = middle + 1;
                }
            }

            return low;
        }

        // The finish time as the user reads it: the day number, and the date as well when the dates are on display.
        private string FormatFinish(int days, bool showDates, DateTimeOffset projectStart)
        {
            string number = days.ToString(CultureInfo.CurrentCulture);

            if (!showDates)
            {
                return number;
            }

            string date = FinishDateOf(days, projectStart).ToString(DateTimeCalculator.DateFormat);
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
                    result = ReferenceEquals(m_ResultSummary, summary) ? m_Result : null;
                    names = m_ActivityNames;
                }

                if (summary is null)
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

                // The iterations are not in a project file. A result that was loaded has the four files, and a run in this
                // session has the fifth too.
                (string Suffix, SimulationCsvFile File)[] files =
                [
                    (Resource.ProjectPlan.Suffixes.Suffix_SimulationStatistics, SimulationCsvFile.Statistics),
                    (Resource.ProjectPlan.Suffixes.Suffix_SimulationHistogram, SimulationCsvFile.Histogram),
                    (Resource.ProjectPlan.Suffixes.Suffix_SimulationPaths, SimulationCsvFile.Paths),
                    (Resource.ProjectPlan.Suffixes.Suffix_SimulationActivities, SimulationCsvFile.Activities),
                    .. result is null ? Array.Empty<(string, SimulationCsvFile)>() : [(Resource.ProjectPlan.Suffixes.Suffix_SimulationIterations, SimulationCsvFile.Iterations)],
                ];

                await Task.Run(() =>
                {
                    foreach ((string suffix, SimulationCsvFile file) in files)
                    {
                        FileStreamHelper.Save(
                            $@"{baseName}{suffix}.csv",
                            stream => WriteCsv(stream, file, summary, result, names));
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

        // One CSV file, as the panel saves it. A byte order mark leads, so that Excel reads the names of the activities as
        // UTF-8.
        private static void WriteCsv(
            Stream stream,
            SimulationCsvFile file,
            SimulationSummaryModel summary,
            SimulationResult<int>? result,
            IReadOnlyDictionary<int, string> names)
        {
            using var writer = new StreamWriter(stream, new UTF8Encoding(true), leaveOpen: true);

            switch (file)
            {
                case SimulationCsvFile.Statistics:
                    SimulationCsvWriter.WriteStatistics(writer, summary);
                    break;
                case SimulationCsvFile.Histogram:
                    SimulationCsvWriter.WriteHistogram(writer, summary);
                    break;
                case SimulationCsvFile.Paths:
                    SimulationCsvWriter.WritePaths(writer, summary);
                    break;
                case SimulationCsvFile.Activities:
                    SimulationCsvWriter.WriteActivities(writer, summary);
                    break;
                case SimulationCsvFile.Iterations:
                    SimulationCsvWriter.WriteIterations(
                        writer,
                        result ?? throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_SimulationNoIterations),
                        names);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(file), file, null);
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

        // The settings are the scenario's, and the core owns them: a project file holds them. A property here reads
        // them from the core and writes them to it, and says that it changed when the core changes (a scenario that
        // loads has other settings).
        private void ChangeSettings(Func<SimulationSettingsModel, SimulationSettingsModel> change, string name)
        {
            SimulationSettingsModel before = m_CoreViewModel.SimulationSettings;
            SimulationSettingsModel after = change(before);

            if (after == before)
            {
                return;
            }

            this.RaisePropertyChanging(name);
            m_CoreViewModel.SimulationSettings = after;
            this.RaisePropertyChanged(name);
        }

        public int Iterations
        {
            get => m_CoreViewModel.SimulationSettings.Iterations;
            set => ChangeSettings(x => x with { Iterations = value }, nameof(Iterations));
        }

        public bool UseRandomSeed
        {
            get => m_CoreViewModel.SimulationSettings.UseRandomSeed;
            set => ChangeSettings(x => x with { UseRandomSeed = value }, nameof(UseRandomSeed));
        }

        public ulong Seed
        {
            get => m_CoreViewModel.SimulationSettings.Seed;
            set => ChangeSettings(x => x with { Seed = value }, nameof(Seed));
        }

        public DurationDistribution Distribution
        {
            get => m_CoreViewModel.SimulationSettings.Distribution;
            set => ChangeSettings(x => x with { Distribution = value }, nameof(Distribution));
        }

        private readonly ObservableAsPropertyHelper<bool> m_IsModifiedBetaDistribution;
        public bool IsModifiedBetaDistribution => m_IsModifiedBetaDistribution.Value;

        public double ModifiedBetaShape
        {
            get => m_CoreViewModel.SimulationSettings.ModifiedBetaShape;
            set => ChangeSettings(x => x with { ModifiedBetaShape = value }, nameof(ModifiedBetaShape));
        }

        public int? TargetFinishTime
        {
            get => m_CoreViewModel.SimulationSettings.TargetFinishTime;
            set
            {
                ChangeSettings(x => x with { TargetFinishTime = value }, nameof(TargetFinishTime));
                this.RaisePropertyChanged(nameof(TargetFinishDate));
            }
        }

        // The settings changed under the panel (a scenario loaded or reset): every property that reads them says so.
        private void RaiseSettingsChanged()
        {
            this.RaisePropertyChanged(nameof(Iterations));
            this.RaisePropertyChanged(nameof(UseRandomSeed));
            this.RaisePropertyChanged(nameof(Seed));
            this.RaisePropertyChanged(nameof(Distribution));
            this.RaisePropertyChanged(nameof(ModifiedBetaShape));
            this.RaisePropertyChanged(nameof(TargetFinishTime));
            this.RaisePropertyChanged(nameof(TargetFinishDate));
        }

        private readonly ObservableAsPropertyHelper<bool> m_ShowDates;
        public bool ShowDates => m_ShowDates.Value;

        public DateTimeOffset? TargetFinishDate
        {
            get => TargetFinishTime is int days ? FinishDateOf(days, m_CoreViewModel.ProjectStart) : null;
            set => TargetFinishTime = value is DateTimeOffset date ? DaysOfFinishDate(date) : null;
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

        // What the last run found, or what the project file held: the scenario's, and the core owns it.
        public SimulationSummaryModel? Summary => m_CoreViewModel.SimulationSummary;

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

        public Task RunSimulationAsync()
        {
            return RunSimulationAsync(CancellationToken.None);
        }

        public async Task RunSimulationAsync(CancellationToken cancellationToken)
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

                // The run belongs to the scenario that is open now. If a scenario loads or resets while it goes on, it stops,
                // and what it found is thrown away.
                int scenarioRevision = m_CoreViewModel.ProjectScenarioRevision;
                Volatile.Write(ref m_RunScenarioRevision, scenarioRevision);

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

                cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Volatile.Write(ref m_RunCancellation, cancellation);

                IsRunning = true;
                SetStatus(0.0, string.Empty);
                Interlocked.Exchange(ref m_LastProgressTicks, 0);

                m_Logger.LogInformation(
                    "Simulation: {Iterations} iterations, seed {Seed}, {Distribution}",
                    settings.Iterations,
                    settings.Seed,
                    settings.Distribution);

                var stopwatch = Stopwatch.StartNew();
                Interlocked.Exchange(ref m_LastLogTicks, Stopwatch.GetTimestamp());
                var progress = new CallbackProgress(done => ReportProgress(done, settings.Iterations, stopwatch));
                CancellationToken token = cancellation.Token;

                SimulationOutcome outcome = await Task.Run(() => Simulate(settings, target, progress, token), token);

                stopwatch.Stop();
                m_Logger.LogInformation("Simulation: done in {Elapsed}", FormatDuration(stopwatch.Elapsed));

                // The scenario was replaced while the run went on: the result is not the scenario's, and nobody sees it.
                if (m_CoreViewModel.ProjectScenarioRevision != scenarioRevision)
                {
                    throw new OperationCanceledException(token);
                }

                lock (m_Lock)
                {
                    m_Result = outcome.Result;
                    m_ResultSummary = outcome.Summary;
                    m_ActivityNames = outcome.ActivityNames;
                    m_PresentedSummary = outcome.Summary;
                }

                ResultRevision = outcome.CompilationOutputRevision;
                m_CoreViewModel.SimulationSummary = outcome.Summary;
                BuildPresentation();

                SetStatus(
                    1.0,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Resource.ProjectPlan.Labels.Label_SimulationDone,
                        FormatDuration(stopwatch.Elapsed)));
            }
            catch (OperationCanceledException)
            {
                SetStatus(0.0, Resource.ProjectPlan.Labels.Label_SimulationCancelled);
            }
            catch (Exception ex)
            {
                SetStatus(0.0, string.Empty);
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
            finally
            {
                Volatile.Write(ref m_RunCancellation, null);
                cancellation?.Dispose();
                IsRunning = false;
                Volatile.Write(ref m_RunGate, 0);
            }
        }

        public void CancelSimulation()
        {
            CancellationTokenSource? cancellation;

            // The core can ask for this while it holds its lock (a scenario resets), and so it takes no lock of its own.
            cancellation = Volatile.Read(ref m_RunCancellation);

            try
            {
                cancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The run finished while the request was on its way, and there is nothing left to cancel.
            }
        }

        public DateOnly GetFinishDate(int finishTime)
        {
            return DateOnly.FromDateTime(FinishDateOf(finishTime, m_CoreViewModel.ProjectStart).DateTime);
        }

        public async Task WriteSimulationChartImageAsync(
            Stream stream,
            ChartImageFormat format,
            int width,
            int height)
        {
            await m_ScottPlotImageExporter.WritePlotImageAsync(SimulationChartPlotModel, stream, format, width, height);
        }

        public Task WriteSimulationCsvAsync(
            Stream stream,
            SimulationCsvFile file)
        {
            ArgumentNullException.ThrowIfNull(stream);

            if (file is not (SimulationCsvFile.Statistics or SimulationCsvFile.Histogram or SimulationCsvFile.Paths or SimulationCsvFile.Activities or SimulationCsvFile.Iterations))
            {
                throw new ArgumentOutOfRangeException(nameof(file), file, null);
            }

            SimulationSummaryModel? summary;
            SimulationResult<int>? result;
            IReadOnlyDictionary<int, string> names;

            lock (m_Lock)
            {
                summary = Summary;
                result = ReferenceEquals(m_ResultSummary, summary) ? m_Result : null;
                names = m_ActivityNames;
            }

            if (summary is null)
            {
                throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_SimulationNoResult);
            }

            WriteCsv(stream, file, summary, result, names);
            return Task.CompletedTask;
        }

        // Presents the summary that the view model holds: the statistics as text, the tables and the chart. This
        // runs when a run finishes, and again when the display settings change what the numbers look like. It can
        // run on any thread, and what it makes is handed to the UI thread.
        private void BuildPresentation()
        {
            SimulationSummaryModel? summary = Summary;
            m_PresentedSummary = summary;

            IReadOnlyList<SimulationStatisticModel> statistics = [];
            IReadOnlyList<SimulationPathModel> paths = [];
            IReadOnlyList<SimulationActivityCriticalityModel> activities = [];

            if (summary is not null)
            {
                statistics = BuildStatistics(
                    summary,
                    m_CoreViewModel.DisplaySettingsViewModel.ShowDates,
                    m_CoreViewModel.ProjectStart);
                paths = summary.Paths;
                activities = summary.Activities;
            }

            m_UiDispatcher.Defer(() =>
            {
                Statistics = statistics;
                Paths = paths;
                Activities = activities;
            });

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

            Plot incoming = plotModel ?? new Plot();

            m_UiDispatcher.Defer(() =>
            {
                if (m_Disposed)
                {
                    incoming.Dispose();
                    return;
                }

                Plot outgoing = SimulationChartPlotModel;
                SimulationChartPlotModel = incoming;
                m_PlotRetirer.Retire(outgoing);
            });
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

            // The settings are the scenario's, and a scenario that loads or resets has other settings.
            m_SettingsSub = this
                .WhenAnyValue(ssm => ssm.m_CoreViewModel.SimulationSettings)
                .MuteWhile(this.WhenAnyValue(ssm => ssm.m_CoreViewModel.IsBulkUpdating))
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ => RaiseSettingsChanged());

            // The result is the scenario's too. One that this panel made is presented already, and it is out of date if
            // the plan compiled since the run began. One that a file held, or none because the scenario reset, is not
            // from a run: it is not out of date, because the file is trusted and there is no newer plan to disagree
            // with, and it is presented unless the pipeline above has presented it already.
            m_SummarySub = this
                .WhenAnyValue(ssm => ssm.m_CoreViewModel.SimulationSummary)
                .Select(summary => ValueTuple.Create(summary)) // A result can be none, and a sequence on a scheduler holds values that are not.
                .MuteWhile(this.WhenAnyValue(ssm => ssm.m_CoreViewModel.IsBulkUpdating))
                .ObserveOn(RxSchedulers.TaskpoolScheduler)
                .Subscribe(change =>
                {
                    SimulationSummaryModel? summary = change.Item1;
                    bool isFromRun;

                    lock (m_Lock)
                    {
                        isFromRun = summary is not null && ReferenceEquals(summary, m_ResultSummary);

                        if (!isFromRun)
                        {
                            // The iterations of a run that the scenario no longer holds are not kept.
                            m_Result = null;
                            m_ResultSummary = null;
                            m_ActivityNames = new Dictionary<int, string>();
                        }
                    }

                    if (isFromRun)
                    {
                        return;
                    }

                    ResultRevision = m_CoreViewModel.CompilationOutputRevision;

                    if (!ReferenceEquals(summary, m_PresentedSummary))
                    {
                        BuildPresentation();
                    }

                    m_UiDispatcher.Defer(() =>
                    {
                        if (!IsRunning)
                        {
                            Progress = 0.0;
                            StatusText = summary is null ? Resource.ProjectPlan.Labels.Label_SimulationNoResult : string.Empty;
                        }
                    });
                });

            // A run belongs to one scenario. When the scenario loads, resets or closes, the run stops.
            m_ScenarioSub = this
                .WhenAnyValue(ssm => ssm.m_CoreViewModel.ProjectScenarioRevision)
                .ObserveOn(RxSchedulers.TaskpoolScheduler)
                .Subscribe(revision =>
                {
                    if (Volatile.Read(ref m_RunCancellation) is not null
                        && Volatile.Read(ref m_RunScenarioRevision) != revision)
                    {
                        CancelSimulation();
                    }
                });
        }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
            m_SubscriptionLifetime.Kill();
            m_BuildPresentationSub?.Dispose();
            m_SettingsSub?.Dispose();
            m_SummarySub?.Dispose();
            m_ScenarioSub?.Dispose();
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
                m_ShowDates?.Dispose();
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
