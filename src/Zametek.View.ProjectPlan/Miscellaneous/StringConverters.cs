using Avalonia.Data.Converters;
using System;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.Resource.ProjectPlan;

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
                    LogicalOperator.AND => Enums.Enum_LogicalOperator_AND,
                    LogicalOperator.OR => Enums.Enum_LogicalOperator_OR,
                    LogicalOperator.ACTIVE_AND => Enums.Enum_LogicalOperator_ACTIVE_AND,
                    _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
                });

        public static readonly IValueConverter InterActivityAllocationTypeValue =
            new FuncValueConverter<InterActivityAllocationType, string>(
                x => x switch
                {
                    InterActivityAllocationType.None => Enums.Enum_InterActivityAllocationType_None,
                    InterActivityAllocationType.Direct => Enums.Enum_InterActivityAllocationType_Direct,
                    InterActivityAllocationType.Indirect => Enums.Enum_InterActivityAllocationType_Indirect,
                    _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
                });

        public static readonly IValueConverter GroupByModeValue =
            new FuncValueConverter<GroupByMode, string>(
                x => x switch
                {
                    GroupByMode.None => Enums.Enum_GroupByMode_None,
                    GroupByMode.Resource => Enums.Enum_GroupByMode_Resource,
                    GroupByMode.WorkStream => Enums.Enum_GroupByMode_WorkStream,
                    _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
                });

        public static readonly IValueConverter AnnotationStyleValue =
            new FuncValueConverter<AnnotationStyle, string>(
                x => x switch
                {
                    AnnotationStyle.None => Enums.Enum_AnnotationStyle_None,
                    AnnotationStyle.Plain => Enums.Enum_AnnotationStyle_Plain,
                    AnnotationStyle.Color => Enums.Enum_AnnotationStyle_Color,
                    _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
                });

        public static readonly IValueConverter AllocationModeValue =
            new FuncValueConverter<AllocationMode, string>(
                x => x switch
                {
                    AllocationMode.Activity => Enums.Enum_AllocationMode_Activity,
                    AllocationMode.Cost => Enums.Enum_AllocationMode_Cost,
                    AllocationMode.Billing => Enums.Enum_AllocationMode_Billing,
                    AllocationMode.Effort => Enums.Enum_AllocationMode_Effort,
                    _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
                });

        public static readonly IValueConverter ScheduleModeValue =
            new FuncValueConverter<ScheduleMode, string>(
                x => x switch
                {
                    ScheduleMode.Combined => Enums.Enum_ScheduleMode_Combined,
                    ScheduleMode.Scheduled => Enums.Enum_ScheduleMode_Scheduled,
                    ScheduleMode.Unscheduled => Enums.Enum_ScheduleMode_Unscheduled,
                    _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
                });

        public static readonly IValueConverter DisplayStyleValue =
            new FuncValueConverter<DisplayStyle, string>(
                x => x switch
                {
                    DisplayStyle.Slanted => Enums.Enum_DisplayStyle_Slanted,
                    DisplayStyle.Block => Enums.Enum_DisplayStyle_Block,
                    _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
                });
    }
}
