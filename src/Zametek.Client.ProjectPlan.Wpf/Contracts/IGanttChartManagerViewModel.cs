using Prism.Interactivity.InteractionRequest;
using System;
using System.Windows.Input;
using Zametek.Common.Project;

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

        ArrowGraphSettingsDto ArrowGraphSettingsDto { get; }
    }
}
