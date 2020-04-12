using Prism.Interactivity.InteractionRequest;
using System;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IGanttChartManagerViewModel
        : INamed
    {
        IInteractionRequest NotificationInteractionRequest { get; }

        DateTime ProjectStart { get; }

        bool IsBusy { get; }

        bool HasStaleGanttChart { get; }

        GanttChartModel GanttChart { get; }

        bool UseBusinessDays { get; }

        //bool ShowDates { get; }

        //bool ShowDays { get; }

        bool HasCompilationErrors { get; }

        ICommand GenerateGanttChartCommand { get; }

        ArrowGraphSettingsModel ArrowGraphSettings { get; }
    }
}
