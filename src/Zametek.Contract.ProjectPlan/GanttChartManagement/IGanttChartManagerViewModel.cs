﻿using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IGanttChartManagerViewModel
        : IKillSubscriptions, IDisposable
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        GroupByMode GroupByMode { get; set; }

        AnnotationStyle AnnotationStyle { get; set; }

        bool ShowGroupLabels { get; set; }

        bool ShowProjectFinish { get; set; }

        bool ShowTracking { get; set; }

        bool ShowToday { get; set; }

        IActivitySelectorViewModel ActivitySelector { get; }

        ICommand SaveGanttChartImageFileCommand { get; }

        Task SaveGanttChartImageFileAsync(string? filename, int width, int height);

        void BuildGanttChartPlotModel();
    }
}
