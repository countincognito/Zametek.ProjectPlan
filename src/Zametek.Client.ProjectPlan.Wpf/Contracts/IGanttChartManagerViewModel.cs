using Prism.Interactivity.InteractionRequest;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IGanttChartManagerViewModel
    {
        IInteractionRequest NotificationInteractionRequest { get; }

        DateTime ProjectStart { get; }

        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool ShowDates { get; }

        bool ShowDays { get; }

        bool HasCompilationErrors { get; }

        List<ManagedActivityViewModel> ArrangedActivities { get; }

        ICommand GenerateGanttChartCommand { get; }
    }
}
