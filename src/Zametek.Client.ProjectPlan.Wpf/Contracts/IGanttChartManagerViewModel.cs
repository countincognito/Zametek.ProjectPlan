using Prism.Interactivity.InteractionRequest;
using System;
using System.Collections.ObjectModel;

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

        ObservableCollection<ManagedActivityViewModel> ArrangedActivities { get; }
    }
}
