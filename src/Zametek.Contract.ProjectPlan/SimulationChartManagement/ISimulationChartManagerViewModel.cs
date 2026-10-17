using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface ISimulationChartManagerViewModel
        : IStartSubscriptions, IKillSubscriptions, IDisposable
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        // The settings of a run. The seed is the one that the last run used.

        int Iterations { get; set; }

        bool UseRandomSeed { get; set; }

        ulong Seed { get; set; }

        DurationDistribution Distribution { get; set; }

        // Only the modified beta-PERT distribution uses the shape value.
        bool IsModifiedBetaDistribution { get; }

        double ModifiedBetaShape { get; set; }

        // The finish time, in days from the start of the project, that a run reports the chance of meeting.
        int? TargetFinishTime { get; set; }

        // Whether the dates are on display. Then the target is given as a date, and not as a number of days.
        bool ShowDates { get; }

        // The date of the finish time that is the target. Setting it sets the finish time of the first day that
        // finishes on that date or after it.
        DateTimeOffset? TargetFinishDate { get; set; }

        // The state of a run.

        bool IsRunning { get; }

        // From 0 to 1.
        double Progress { get; }

        string StatusText { get; }

        // The result of the last run.

        bool HasResult { get; }

        // Whether the plan has changed since the last run, and so the result no longer belongs to it.
        bool IsResultStale { get; }

        SimulationSummaryModel? Summary { get; }

        IReadOnlyList<SimulationStatisticModel> Statistics { get; }

        IReadOnlyList<SimulationPathModel> Paths { get; }

        IReadOnlyList<SimulationActivityCriticalityModel> Activities { get; }

        ICommand RunSimulationCommand { get; }

        ICommand CancelSimulationCommand { get; }

        ICommand ResetSimulationChartCommand { get; }

        ICommand SaveSimulationChartImageFileCommand { get; }

        ICommand ExportCsvFilesCommand { get; }

        // Runs a simulation of a copy of the plan, on the task pool, and presents the result. A second call
        // while a run is in progress does nothing.
        Task RunSimulationAsync();

        // The same, and the run also stops when the token is cancelled. A token that is already cancelled stops it before it
        // starts, and so a caller that cancels at any time, also just before the call, is never left with a run that goes on.
        Task RunSimulationAsync(CancellationToken cancellationToken);

        // The same, with the seed that the scenario has saved, also when "New seed each run" is set: the settings of the
        // scenario are not changed, and the run repeats. A caller that runs a saved scenario (zpp does) uses this one.
        Task RunSimulationWithSavedSeedAsync(CancellationToken cancellationToken);

        void CancelSimulation();

        // The date on which a project that takes the given number of days finishes, in the working calendar of the plan.
        DateOnly GetFinishDate(int finishTime);

        // Writes the chart to the stream at the given size. The caller owns the stream, which is left open.
        Task WriteSimulationChartImageAsync(Stream stream, ChartImageFormat format, int width, int height);

        // Writes one of the CSV files of the last result to the stream, which is left open. The file is the one that the
        // panel saves: UTF-8 with a byte order mark. It is an error to ask for more than one file, or for a result that
        // does not exist.
        Task WriteSimulationCsvAsync(Stream stream, SimulationCsvFile file);

        // Presents the result that the scenario holds: the statistics, the tables and the chart. A run does this when it ends,
        // and the panel when the display settings change. A caller that has no running panel, and wants the statistics of a
        // result that a project file held, calls it first.
        void BuildPresentation();

        void BuildSimulationChartPlotModel();
    }
}
