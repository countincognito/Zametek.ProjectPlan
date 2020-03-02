using Prism.Interactivity.InteractionRequest;
using System;
using System.Windows.Input;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IGanttChartManagerViewModel
    {
        IInteractionRequest NotificationInteractionRequest { get; }

        DateTime ProjectStart { get; }

        bool IsBusy { get; }

        bool HasStaleGanttChart { get; }

        GanttChartDto GanttChartDto { get; }

        bool UseBusinessDays { get; }

        //bool ShowDates { get; }

        //bool ShowDays { get; }

        bool HasCompilationErrors { get; }

        ICommand GenerateGanttChartCommand { get; }

        Common.Project.v0_1_0.ArrowGraphSettingsDto ArrowGraphSettingsDto { get; }
    }
}
