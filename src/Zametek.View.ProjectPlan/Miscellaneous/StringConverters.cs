using Avalonia.Data.Converters;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.View.ProjectPlan
{
    public static class StringConverters
    {
        public static readonly IValueConverter IsMatch =
            new FuncValueConverter<string?, string?, bool>(
                (x, y) => ViewModel.ProjectPlan.StringConverters.IsMatch(x, y));

        public static readonly IValueConverter LogicalOperatorValue =
            new FuncValueConverter<LogicalOperator, string>(
                x => ViewModel.ProjectPlan.StringConverters.LogicalOperatorValue(x));

        public static readonly IValueConverter InterActivityAllocationTypeValue =
            new FuncValueConverter<InterActivityAllocationType, string>(
                x => ViewModel.ProjectPlan.StringConverters.InterActivityAllocationTypeValue(x));

        public static readonly IValueConverter GroupByModeValue =
            new FuncValueConverter<GroupByMode, string>(
                x => ViewModel.ProjectPlan.StringConverters.GroupByModeValue(x));

        public static readonly IValueConverter AnnotationStyleValue =
            new FuncValueConverter<AnnotationStyle, string>(
                x => ViewModel.ProjectPlan.StringConverters.AnnotationStyleValue(x));

        public static readonly IValueConverter AllocationModeValue =
            new FuncValueConverter<AllocationMode, string>(
                x => ViewModel.ProjectPlan.StringConverters.AllocationModeValue(x));

        public static readonly IValueConverter ScheduleModeValue =
            new FuncValueConverter<ScheduleMode, string>(
                x => ViewModel.ProjectPlan.StringConverters.ScheduleModeValue(x));

        public static readonly IValueConverter DisplayStyleValue =
            new FuncValueConverter<DisplayStyle, string>(
                x => ViewModel.ProjectPlan.StringConverters.DisplayStyleValue(x));

        public static readonly IValueConverter TrackedMetricsValue =
            new FuncValueConverter<TrackedMetrics, string>(
                x => ViewModel.ProjectPlan.StringConverters.TrackedMetricsValue(x));

        public static readonly IValueConverter CurveFittingTypeValue =
            new FuncValueConverter<CurveFittingType, string>(
                x => ViewModel.ProjectPlan.StringConverters.CurveFittingTypeValue(x));

        public static readonly IValueConverter RecurrenceFrequencyValue =
            new FuncValueConverter<RecurrenceFrequency, string>(
                x => ViewModel.ProjectPlan.StringConverters.RecurrenceFrequencyValue(x));
    }
}
