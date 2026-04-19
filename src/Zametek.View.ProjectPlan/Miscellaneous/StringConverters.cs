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

        public static readonly IValueConverter TrackedMetricsValue =
            new FuncValueConverter<TrackedMetrics, string>(
                x => x switch
                {
                    TrackedMetrics.None => Enums.Enum_TrackedMetric_None,
                    TrackedMetrics.RisksCriticality => Enums.Enum_TrackedMetric_RisksCriticality,
                    TrackedMetrics.RisksFibonacci => Enums.Enum_TrackedMetric_RisksFibonacci,
                    TrackedMetrics.RisksActivity => Enums.Enum_TrackedMetric_RisksActivity,
                    TrackedMetrics.RisksActivityStdDevCorrection => Enums.Enum_TrackedMetric_RisksActivityStdDevCorrection,
                    TrackedMetrics.RisksGeometricCriticality => Enums.Enum_TrackedMetric_RisksGeometricCriticality,
                    TrackedMetrics.RisksGeometricFibonacci => Enums.Enum_TrackedMetric_RisksGeometricFibonacci,
                    TrackedMetrics.RisksGeometricActivity => Enums.Enum_TrackedMetric_RisksGeometricActivity,
                    TrackedMetrics.CostsDirect => Enums.Enum_TrackedMetric_CostsDirect,
                    TrackedMetrics.CostsIndirect => Enums.Enum_TrackedMetric_CostsIndirect,
                    TrackedMetrics.CostsOther => Enums.Enum_TrackedMetric_CostsOther,
                    TrackedMetrics.CostsTotal => Enums.Enum_TrackedMetric_CostsTotal,
                    TrackedMetrics.BillingsDirect => Enums.Enum_TrackedMetric_BillingsDirect,
                    TrackedMetrics.BillingsIndirect => Enums.Enum_TrackedMetric_BillingsIndirect,
                    TrackedMetrics.BillingsOther => Enums.Enum_TrackedMetric_BillingsOther,
                    TrackedMetrics.BillingsTotal => Enums.Enum_TrackedMetric_BillingsTotal,
                    TrackedMetrics.MarginsDirect => Enums.Enum_TrackedMetric_MarginsDirect,
                    TrackedMetrics.MarginsIndirect => Enums.Enum_TrackedMetric_MarginsIndirect,
                    TrackedMetrics.MarginsOther => Enums.Enum_TrackedMetric_MarginsOther,
                    TrackedMetrics.MarginsTotal => Enums.Enum_TrackedMetric_MarginsTotal,
                    TrackedMetrics.MarginsDirectAbsolute => Enums.Enum_TrackedMetric_MarginsDirectAbsolute,
                    TrackedMetrics.MarginsIndirectAbsolute => Enums.Enum_TrackedMetric_MarginsIndirectAbsolute,
                    TrackedMetrics.MarginsOtherAbsolute => Enums.Enum_TrackedMetric_MarginsOtherAbsolute,
                    TrackedMetrics.MarginsTotalAbsolute => Enums.Enum_TrackedMetric_MarginsTotalAbsolute,
                    TrackedMetrics.EffortsDirect => Enums.Enum_TrackedMetric_EffortsDirect,
                    TrackedMetrics.EffortsIndirect => Enums.Enum_TrackedMetric_EffortsIndirect,
                    TrackedMetrics.EffortsOther => Enums.Enum_TrackedMetric_EffortsOther,
                    TrackedMetrics.EffortsTotal => Enums.Enum_TrackedMetric_EffortsTotal,
                    TrackedMetrics.EffortsActivity => Enums.Enum_TrackedMetric_EffortsActivity,
                    TrackedMetrics.EffortsEfficiency => Enums.Enum_TrackedMetric_EffortsEfficiency,
                    TrackedMetrics.NetworkCyclomaticComplexity => Enums.Enum_TrackedMetric_NetworkCyclomaticComplexity,
                    TrackedMetrics.NetworkDuration => Enums.Enum_TrackedMetric_NetworkDuration,
                    TrackedMetrics.NetworkDurationManMonths => Enums.Enum_TrackedMetric_NetworkDurationManMonths,
                    _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
                });

        public static readonly IValueConverter CurveFittingTypeValue =
            new FuncValueConverter<CurveFittingType, string>(
                x => x switch
                {
                    CurveFittingType.None => Enums.Enum_CurveFittingType_None,
                    CurveFittingType.Linear => Enums.Enum_CurveFittingType_Linear,
                    CurveFittingType.Exponential => Enums.Enum_CurveFittingType_Exponential,
                    CurveFittingType.Logarithmic => Enums.Enum_CurveFittingType_Logarithmic,
                    CurveFittingType.Power => Enums.Enum_CurveFittingType_Power,
                    CurveFittingType.PolynomialOrder2 => Enums.Enum_CurveFittingType_PolynomialOrder2,
                    CurveFittingType.PolynomialOrder3 => Enums.Enum_CurveFittingType_PolynomialOrder3,
                    CurveFittingType.PolynomialOrder4 => Enums.Enum_CurveFittingType_PolynomialOrder4,
                    _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
                });

        public static readonly IValueConverter RecurrenceFrequencyValue =
            new FuncValueConverter<RecurrenceFrequency, string>(
                x => x switch
                {
                    //RecurrenceFrequency.Secondly => Enums.Enum_RecurrenceFrequency_Secondly,
                    //RecurrenceFrequency.Minutely => Enums.Enum_RecurrenceFrequency_Minutely,
                    //RecurrenceFrequency.Hourly => Enums.Enum_RecurrenceFrequency_Hourly,
                    RecurrenceFrequency.Daily => Enums.Enum_RecurrenceFrequency_Daily,
                    RecurrenceFrequency.Weekly => Enums.Enum_RecurrenceFrequency_Weekly,
                    RecurrenceFrequency.Monthly => Enums.Enum_RecurrenceFrequency_Monthly,
                    RecurrenceFrequency.Yearly => Enums.Enum_RecurrenceFrequency_Yearly,
                    _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
                });
    }
}
