﻿using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IResourceChartManagerViewModel
        : IKillSubscriptions
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        AllocationMode AllocationMode { get; set; }

        ScheduleMode ScheduleMode { get; set; }

        DisplayStyle DisplayStyle { get; set; }

        bool ShowToday { get; set; }

        ICommand SaveResourceChartImageFileCommand { get; }

        Task SaveResourceChartImageFileAsync(string? filename, int width, int height);

        void BuildResourceChartPlotModel();
    }
}
