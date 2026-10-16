using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface IScheduleSimulationManagerViewModel
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

        void CancelSimulation();

        // Writes the chart to the stream at the given size. The caller owns the stream, which is left open.
        Task WriteSimulationChartImageAsync(Stream stream, ChartImageFormat format, int width, int height);

        void BuildSimulationChartPlotModel();
    }
}
