using Avalonia.Data.Converters;
using System;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.View.ProjectPlan
{
    public static class StringConverters
    {
        public static readonly IValueConverter IsMatch =
            new FuncValueConverter<string?, string?, bool>(
                (x, y) => string.Equals(x, y, StringComparison.InvariantCulture));

        public static readonly IValueConverter LogicalOperatorValue =
            new FuncValueConverter<LogicalOperator, string>(
                x => x switch
                {
                    LogicalOperator.AND => "AND",
                    LogicalOperator.OR => "OR",
                    LogicalOperator.ACTIVE_AND => "AND (Enabled)",
                    _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
                });

        public static readonly IValueConverter InterActivityAllocationTypeValue =
            new FuncValueConverter<InterActivityAllocationType, string>(
                x => x switch
                {
                    InterActivityAllocationType.None => "None",
                    InterActivityAllocationType.Direct => "Direct",
                    InterActivityAllocationType.Indirect => "Indirect",
                    _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
                });

        public static readonly IValueConverter GroupByModeValue =
            new FuncValueConverter<GroupByMode, string>(
                x => x switch
                {
                    GroupByMode.None => "None",
                    GroupByMode.Resource => "Resource",
                    GroupByMode.WorkStream => "Work Stream",
                    _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
                });

        public static readonly IValueConverter AnnotationStyleValue =
            new FuncValueConverter<AnnotationStyle, string>(
                x => x switch
                {
                    AnnotationStyle.None => "None",
                    AnnotationStyle.Plain => "Plain",
                    AnnotationStyle.Color => "Color",
                    _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
                });

        public static readonly IValueConverter AllocationModeValue =
            new FuncValueConverter<AllocationMode, string>(
                x => x switch
                {
                    AllocationMode.Activity => "Activity",
                    AllocationMode.Cost => "Cost",
                    AllocationMode.Billing => "Billing",
                    AllocationMode.Effort => "Effort",
                    _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
                });

        public static readonly IValueConverter ScheduleModeValue =
            new FuncValueConverter<ScheduleMode, string>(
                x => x switch
                {
                    ScheduleMode.Combined => "Combined",
                    ScheduleMode.Scheduled => "Scheduled",
                    ScheduleMode.Unscheduled => "Unscheduled",
                    _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
                });

        public static readonly IValueConverter DisplayStyleValue =
            new FuncValueConverter<DisplayStyle, string>(
                x => x switch
                {
                    DisplayStyle.Slanted => "Slanted",
                    DisplayStyle.Block => "Block",
                    _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
                });
    }
}
