using System.Collections;
using System.Diagnostics;
using System.Reflection;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class XlsxScenarioFileExporter
        : IXlsxScenarioFileExporter
    {
        #region Fields

        private static readonly IList<string> s_GeneralColumnTitles =
            [
                nameof(ProjectScenarioModel.ProjectStart),
                nameof(ProjectScenarioModel.Today)
            ];

        private static readonly IList<string> s_ResourceSettingsColumnTitles =
            [
                nameof(ProjectScenarioModel.ResourceSettings.DefaultUnitCost),
                nameof(ProjectScenarioModel.ResourceSettings.DefaultUnitBilling),
                nameof(ProjectScenarioModel.ResourceSettings.AreDisabled),
            ];

        private static readonly IList<string> s_DisplaySettingsColumnTitles =
            [
                nameof(ProjectScenarioModel.DisplaySettings.ShowDates),
                nameof(ProjectScenarioModel.DisplaySettings.UseClassicDates),
                nameof(ProjectScenarioModel.DisplaySettings.NonWorkingDayMode),
                nameof(ProjectScenarioModel.DisplaySettings.HideCost),
                nameof(ProjectScenarioModel.DisplaySettings.HideBilling),
            ];

        // Two columns that the grid shows and the model does not hold. They are worked out from the
        // estimates by the rules of the grid (EstimateHelper), for the person who reads the workbook,
        // and the import ignores them: the estimates are what it reads.
        //
        // The expected duration is the Duration, which is the duration that was scheduled, and the
        // workbook has it as a column of its own so that it sits with the estimates that it comes from.
        private const string c_ExpectedDurationColumnTitle = @"ExpectedDuration";

        private const string c_StandardDeviationColumnTitle = @"StandardDeviation";

        private static readonly IReadOnlyDictionary<string, Func<ActivityModel, object?>> s_ActivityComputedColumns =
            new Dictionary<string, Func<ActivityModel, object?>>
            {
                [c_ExpectedDurationColumnTitle] = static x =>
                {
                    (int optimistic, int mostLikely, int pessimistic) = EstimatesOf(x);
                    return EstimateHelper.ExpectedDuration(optimistic, mostLikely, pessimistic);
                },
                [c_StandardDeviationColumnTitle] = static x =>
                {
                    (int optimistic, _, int pessimistic) = EstimatesOf(x);
                    return EstimateHelper.StandardDeviation(optimistic, pessimistic);
                },
            };

        private static (int Optimistic, int MostLikely, int Pessimistic) EstimatesOf(ActivityModel activity) =>
            EstimateHelper.Effective(activity.MostLikelyDuration ?? activity.Duration, activity.OptimisticDuration, activity.PessimisticDuration);

        private static readonly IList<string> s_ActivityColumnTitles =
            [
                nameof(ActivityModel.Id),
                nameof(ActivityModel.DisplayOrder),
                nameof(ActivityModel.Name),
                nameof(ActivityModel.TargetWorkStreams),
                nameof(ActivityModel.TargetResources),
                nameof(ActivityModel.TargetResourceOperator),
                nameof(ActivityModel.AllocatedToResources),
                nameof(ActivityModel.HasNoCost),
                nameof(ActivityModel.HasNoBilling),
                nameof(ActivityModel.HasNoEffort),
                nameof(ActivityModel.HasNoRisk),
                nameof(ActivityModel.Duration),
                nameof(ActivityModel.MostLikelyDuration),
                nameof(ActivityModel.OptimisticDuration),
                nameof(ActivityModel.PessimisticDuration),
                c_ExpectedDurationColumnTitle,
                c_StandardDeviationColumnTitle,
                nameof(ActivityModel.FreeSlack),
                nameof(ActivityModel.TotalSlack),
                nameof(ActivityModel.EarliestStartTime),
                nameof(ActivityModel.LatestStartTime),
                nameof(ActivityModel.EarliestFinishTime),
                nameof(ActivityModel.LatestFinishTime),
                nameof(ActivityModel.MinimumFreeSlack),
                nameof(ActivityModel.MinimumEarliestStartTime),
                nameof(ActivityModel.MinimumEarliestStartDateTime),
                nameof(ActivityModel.MaximumLatestFinishTime),
                nameof(ActivityModel.MaximumLatestFinishDateTime),
                nameof(ActivityModel.OverrideColor),
                nameof(ActivityModel.ColorFormat),
                nameof(ActivityModel.Notes)
            ];

        private static readonly IList<string> s_DependentActivityColumnTitles =
            [
                nameof(DependentActivityModel.Dependencies),
                nameof(DependentActivityModel.PlanningDependencies),
                nameof(DependentActivityModel.ResourceDependencies),
                nameof(DependentActivityModel.Successors)
            ];

        private static readonly IList<string> s_ResourceColumnTitles =
            [
                nameof(ResourceModel.Id),
                nameof(ResourceModel.Name),
                nameof(ResourceModel.Notes),
                nameof(ResourceModel.IsExplicitTarget),
                nameof(ResourceModel.IsInactive),
                nameof(ResourceModel.ActivityAllocationType),
                nameof(ResourceModel.InterActivityAllocationType),
                nameof(ResourceModel.InterActivityPhases),
                nameof(ResourceModel.UnitCost),
                nameof(ResourceModel.UnitBilling),
                nameof(ResourceModel.FixedCost),
                nameof(ResourceModel.FixedBilling),
                nameof(ResourceModel.DisplayOrder),
                nameof(ResourceModel.AllocationOrder),
                nameof(ResourceModel.ColorFormat)
            ];

        private static readonly IList<string> s_ActivitySeverityColumnTitles =
            [
                nameof(ActivitySeverityModel.SlackLimit),
                nameof(ActivitySeverityModel.CriticalityWeight),
                nameof(ActivitySeverityModel.FibonacciWeight),
                nameof(ActivitySeverityModel.ColorFormat)
            ];

        private static readonly IList<string> s_WorkStreamColumnTitles =
            [
                nameof(WorkStreamModel.Id),
                nameof(WorkStreamModel.Name),
                nameof(WorkStreamModel.IsPhase),
                nameof(WorkStreamModel.DisplayOrder),
                nameof(WorkStreamModel.ColorFormat)
            ];

        private static readonly IList<string> s_HolidayColumnTitles =
            [
                nameof(HolidayModel.Id),
                nameof(HolidayModel.Name),
                nameof(HolidayModel.StartDateTime),
                nameof(HolidayModel.RecurrencePattern),
                nameof(HolidayModel.Notes)
            ];

        private static readonly IList<string> s_TrackingPointColumnTitles =
            [
                nameof(TrackingPointModel.Time),
                nameof(TrackingPointModel.ActivityId),
                nameof(TrackingPointModel.ActivityName),
                nameof(TrackingPointModel.Value),
                nameof(TrackingPointModel.ValuePercentage)
            ];

        private readonly IDateTimeCalculator m_DateTimeCalculator;

        #endregion

        #region Ctors

        public XlsxScenarioFileExporter(IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            m_DateTimeCalculator = dateTimeCalculator;
        }

        #endregion

        #region Private Methods

        // What it takes to make a date of a time: a time in a sheet is a number of days from the start of the project, and a
        // workbook that shows dates has the date of the day in its place.
        private sealed record DatesFromStart(
            bool ShowDates,
            DateTimeOffset ProjectStart,
            IDateTimeCalculator DateTimeCalculator);

        private static void DateFromProjectStart(
            XlsxSheetWriter sheet,
            int row,
            int column,
            int time,
            XlsxCellStyle style,
            XlsxCellStyle dateStyle,
            DatesFromStart datesFromStart)
        {
            if (datesFromStart.ShowDates)
            {
                sheet.Date(row, column, datesFromStart.DateTimeCalculator.AddDays(datesFromStart.ProjectStart, time).DateTime, dateStyle);
            }
            else
            {
                sheet.Number(row, column, time, style);
            }
        }

        private static DatesFromStart? GetActivityDatesFromStart(
            string columnTitle,
            bool showDates,
            DateTimeOffset projectStart,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);

            return columnTitle is nameof(ActivityModel.EarliestStartTime)
                or nameof(ActivityModel.LatestStartTime)
                or nameof(ActivityModel.EarliestFinishTime)
                or nameof(ActivityModel.LatestFinishTime)
                    ? new DatesFromStart(showDates, projectStart, dateTimeCalculator)
                    : null;
        }

        private static DatesFromStart? GetTrackingPointDatesFromStart(
            string columnTitle,
            bool showDates,
            DateTimeOffset projectStart,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);

            return columnTitle is nameof(TrackingPointModel.Time)
                ? new DatesFromStart(showDates, projectStart, dateTimeCalculator)
                : null;
        }

        // Writes a value into a cell, as what it is. A time (a whole number) in a column that has dates from the start is a date
        // when the workbook shows dates.
        //
        // style is the look of the cell, and dateStyle is the look that a date has, which replaces it.
        private static void AddToCell(
            object? content,
            XlsxSheetWriter sheet,
            int row,
            int column,
            XlsxCellStyle style = XlsxCellStyle.Plain,
            XlsxCellStyle dateStyle = XlsxCellStyle.Date,
            DatesFromStart? datesFromStart = null)
        {
            ArgumentNullException.ThrowIfNull(sheet);

            if (datesFromStart is not null
                && content is int time)
            {
                DateFromProjectStart(sheet, row, column, time, style, dateStyle, datesFromStart);
                return;
            }

            switch (content)
            {
                case string x:
                    sheet.Text(row, column, x, style);
                    break;
                case int x:
                    sheet.Number(row, column, x, style);
                    break;
                case double x:
                    sheet.Number(row, column, x, style);
                    break;
                case bool x:
                    sheet.Boolean(row, column, x, style);
                    break;
                case ColorFormatModel x:
                    sheet.Text(row, column, ColorHelper.ColorFormatToHtmlHexCode(x), style);
                    break;
                case DateTime x:
                    sheet.Date(row, column, x, dateStyle);
                    break;
                case DateTimeOffset x:
                    sheet.Date(row, column, x.DateTime, dateStyle);
                    break;
                case IEnumerable x:
                    sheet.Text(row, column, string.Join(DependenciesStringValidationRule.Separator, x.Cast<object>().Select(y => y.ToString())), style);
                    break;
                default:
                    sheet.Text(row, column, content?.ToString(), style);
                    break;
            }
        }

        // The row of titles of a sheet.
        private static void WriteTitles(
            XlsxSheetWriter sheet,
            IEnumerable<string> columnTitles)
        {
            int titleColumnIndex = 0;

            foreach (string columnTitle in columnTitles)
            {
                AddToCell(columnTitle, sheet, 0, titleColumnIndex, XlsxCellStyle.Title);
                titleColumnIndex++;
            }
        }

        private static void WriteGeneralToWorkbook(
            ProjectScenarioModel projectScenario,
            XlsxWorkbookWriter workbook)
        {
            ArgumentNullException.ThrowIfNull(projectScenario);
            ArgumentNullException.ThrowIfNull(workbook);
            XlsxDateStyles dateStyles = workbook.CreateDateStyles(DateTimeCalculator.DateFormat);

            XlsxSheetWriter sheet = workbook.AddSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetGeneral, dateStyles);

            WriteTitles(sheet, s_GeneralColumnTitles);

            int columnIndex = 0;

            foreach (string columnTitle in s_GeneralColumnTitles)
            {
                columnTitle.ValueSwitchOn()
                    .Case(nameof(ProjectScenarioModel.ProjectStart),
                        colName => AddToCell(projectScenario.ProjectStart, sheet, 1, columnIndex))
                    .Case(nameof(ProjectScenarioModel.Today),
                        colName => AddToCell(projectScenario.Today, sheet, 1, columnIndex));

                columnIndex++;
            }
        }

        private static void WriteDisplaySettingsToWorkbook(
            ProjectScenarioModel projectScenario,
            XlsxWorkbookWriter workbook)
        {
            ArgumentNullException.ThrowIfNull(projectScenario);
            ArgumentNullException.ThrowIfNull(workbook);
            XlsxDateStyles dateStyles = workbook.CreateDateStyles(DateTimeCalculator.DateFormat);

            XlsxSheetWriter sheet = workbook.AddSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetDisplaySettings, dateStyles);

            WriteTitles(sheet, s_DisplaySettingsColumnTitles);

            int columnIndex = 0;

            foreach (string columnTitle in s_DisplaySettingsColumnTitles)
            {
                columnTitle.ValueSwitchOn()
                    .Case(nameof(ProjectScenarioModel.DisplaySettings.ShowDates),
                        colName => AddToCell(projectScenario.DisplaySettings.ShowDates, sheet, 1, columnIndex))
                    .Case(nameof(ProjectScenarioModel.DisplaySettings.UseClassicDates),
                        colName => AddToCell(projectScenario.DisplaySettings.UseClassicDates, sheet, 1, columnIndex))
                    .Case(nameof(ProjectScenarioModel.DisplaySettings.NonWorkingDayMode),
                        colName => AddToCell(projectScenario.DisplaySettings.NonWorkingDayMode, sheet, 1, columnIndex))
                    .Case(nameof(ProjectScenarioModel.DisplaySettings.HideCost),
                        colName => AddToCell(projectScenario.DisplaySettings.HideCost, sheet, 1, columnIndex))
                    .Case(nameof(ProjectScenarioModel.DisplaySettings.HideBilling),
                        colName => AddToCell(projectScenario.DisplaySettings.HideBilling, sheet, 1, columnIndex));

                columnIndex++;
            }
        }

        private static void WriteResourceSettingsToWorkbook(
            ProjectScenarioModel projectScenario,
            XlsxWorkbookWriter workbook)
        {
            ArgumentNullException.ThrowIfNull(projectScenario);
            ArgumentNullException.ThrowIfNull(workbook);
            XlsxDateStyles dateStyles = workbook.CreateDateStyles(DateTimeCalculator.DateFormat);

            XlsxSheetWriter sheet = workbook.AddSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetResourceSettings, dateStyles);

            WriteTitles(sheet, s_ResourceSettingsColumnTitles);

            int columnIndex = 0;

            foreach (string columnTitle in s_ResourceSettingsColumnTitles)
            {
                columnTitle.ValueSwitchOn()
                    .Case(nameof(ProjectScenarioModel.ResourceSettings.DefaultUnitCost),
                        colName => AddToCell(projectScenario.ResourceSettings.DefaultUnitCost, sheet, 1, columnIndex))
                    .Case(nameof(ProjectScenarioModel.ResourceSettings.DefaultUnitBilling),
                        colName => AddToCell(projectScenario.ResourceSettings.DefaultUnitBilling, sheet, 1, columnIndex))
                    .Case(nameof(ProjectScenarioModel.ResourceSettings.AreDisabled),
                        colName => AddToCell(projectScenario.ResourceSettings.AreDisabled, sheet, 1, columnIndex));

                columnIndex++;
            }
        }

        private static void WriteActivitiesToWorkbook(
            IEnumerable<DependentActivityModel> dependentActivities,
            XlsxWorkbookWriter workbook,
            bool showDates,
            DateTimeOffset projectStart,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(dependentActivities);
            ArgumentNullException.ThrowIfNull(workbook);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            XlsxDateStyles dateStyles = workbook.CreateDateStyles(DateTimeCalculator.DateFormat);

            XlsxSheetWriter sheet = workbook.AddSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetActivities, dateStyles);

            WriteTitles(sheet, [.. s_ActivityColumnTitles, .. s_DependentActivityColumnTitles]);

            int rowIndex = 1;

            {
                Type activityType = typeof(ActivityModel);
                PropertyInfo[] activityPropertyInfos = activityType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty);
                var activityPropertyInfoLookup = activityPropertyInfos.ToDictionary(x => x.Name);

                Type dependentActivityType = typeof(DependentActivityModel);
                PropertyInfo[] dependentActivityPropertyInfos = dependentActivityType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty);
                var dependentActivityPropertyInfoLookup = dependentActivityPropertyInfos.ToDictionary(x => x.Name);

                foreach (DependentActivityModel dependentActivity in dependentActivities)
                {
                    int columnIndex = 0;
                    ActivityModel activity = dependentActivity.Activity;

                    foreach (string columnTitle in s_ActivityColumnTitles)
                    {
                        if (s_ActivityComputedColumns.TryGetValue(columnTitle, out Func<ActivityModel, object?>? compute))
                        {
                            AddToCell(compute(activity), sheet, rowIndex, columnIndex);
                            columnIndex++;
                            continue;
                        }

                        PropertyInfo? propertyInfo = activityPropertyInfoLookup[columnTitle];

                        if (propertyInfo is not null)
                        {
                            object? content = propertyInfo.GetValue(activity);
                            AddToCell(
                                content,
                                sheet,
                                rowIndex,
                                columnIndex,
                                datesFromStart: GetActivityDatesFromStart(columnTitle, showDates, projectStart, dateTimeCalculator));
                        }
                        columnIndex++;
                    }
                    foreach (string columnTitle in s_DependentActivityColumnTitles)
                    {
                        PropertyInfo? propertyInfo = dependentActivityPropertyInfoLookup[columnTitle];

                        if (propertyInfo is not null)
                        {
                            object? content = propertyInfo.GetValue(dependentActivity);
                            AddToCell(content, sheet, rowIndex, columnIndex);
                        }
                        columnIndex++;
                    }

                    rowIndex++;
                }
            }
        }

        private static void WriteItemsToWorkbook<T>(
            IEnumerable<T> items,
            IEnumerable<string> columnTitles,
            string sheetTitle,
            XlsxWorkbookWriter workbook)
        {
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(columnTitles);
            ArgumentNullException.ThrowIfNull(workbook);
            XlsxDateStyles dateStyles = workbook.CreateDateStyles(DateTimeCalculator.DateFormat);

            XlsxSheetWriter sheet = workbook.AddSheet(sheetTitle, dateStyles);

            WriteTitles(sheet, columnTitles);

            int rowIndex = 1;

            {
                Type itemType = typeof(T);
                PropertyInfo[] propertyInfos = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty);
                var propertyInfoLookup = propertyInfos.ToDictionary(x => x.Name);

                foreach (T item in items)
                {
                    int columnIndex = 0;

                    foreach (string columnTitle in columnTitles)
                    {
                        PropertyInfo? propertyInfo = propertyInfoLookup[columnTitle];

                        if (propertyInfo is not null)
                        {
                            object? content = propertyInfo.GetValue(item);
                            AddToCell(content, sheet, rowIndex, columnIndex);
                        }
                        columnIndex++;
                    }

                    rowIndex++;
                }
            }
        }

        // The cells of a row of the sheet of a tracker that hold times: a number from the start of the project (or a date, when the
        // workbook shows dates), in the look of a title.
        private static void WriteTrackerTitles(
            XlsxSheetWriter sheet,
            int endTime,
            DatesFromStart datesFromStart)
        {
            int titleColumnIndex = 0;

            // Title row (Activity ID column).
            AddToCell(nameof(ActivityModel.Id), sheet, 0, titleColumnIndex, XlsxCellStyle.Title, XlsxCellStyle.TitleDate);
            titleColumnIndex++;

            for (int i = 0; i <= endTime; i++)
            {
                AddToCell(i, sheet, 0, titleColumnIndex, XlsxCellStyle.Title, XlsxCellStyle.TitleDate, datesFromStart);
                titleColumnIndex++;
            }
        }

        private static void WriteActivityTrackersToWorkbook<T>(
            IEnumerable<ActivityModel> activities,
            Func<ActivityTrackerModel, T> trackerFunc,
            string sheetTitle,
            XlsxWorkbookWriter workbook,
            bool showDates,
            DateTimeOffset projectStart,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(activities);
            ArgumentNullException.ThrowIfNull(trackerFunc);
            ArgumentNullException.ThrowIfNull(workbook);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            XlsxDateStyles dateStyles = workbook.CreateDateStyles(DateTimeCalculator.DateFormat, includeTitleDate: true);

            XlsxSheetWriter sheet = workbook.AddSheet(sheetTitle, dateStyles);

            int rowIndex = 0;
            int endTime = 0;

            int plannedEndTime = activities
                .Select(x => x.EarliestFinishTime.GetValueOrDefault())
                .DefaultIfEmpty()
                .Max() - 1; // Remove a day because we iterate to this time inclusively.

            if (plannedEndTime > endTime)
            {
                endTime = plannedEndTime;
            }

            int progressTime = activities
                .SelectMany(x => x.Trackers)
                .DefaultIfEmpty()
                .Max(x => x?.Time ?? 0);

            if (progressTime > endTime)
            {
                endTime = progressTime;
            }

            {
                WriteTrackerTitles(sheet, endTime, new DatesFromStart(showDates, projectStart, dateTimeCalculator));
                rowIndex++;
            }
            {
                // Activity Trackers.
                foreach (ActivityModel activity in activities)
                {
                    int columnIndex = 0;
                    int activityId = activity.Id;

                    // Activity Id.
                    AddToCell(activityId, sheet, rowIndex, columnIndex, XlsxCellStyle.Title);
                    columnIndex++;

                    // Create a lookup dictionary for each time entry.
                    Dictionary<int, ActivityTrackerModel> activityTrackerLookup = [];

                    foreach (ActivityTrackerModel tracker in activity.Trackers)
                    {
                        if (tracker.ActivityId == activityId)
                        {
                            activityTrackerLookup[tracker.Time] = tracker;
                        }
                    }

                    // Tracker values.
                    for (int i = 0; i <= endTime; i++)
                    {
                        if (activityTrackerLookup.TryGetValue(i, out ActivityTrackerModel? activityTracker))
                        {
                            AddToCell(trackerFunc(activityTracker), sheet, rowIndex, columnIndex);
                        }

                        columnIndex++;
                    }

                    rowIndex++;
                }
            }
        }

        private static void WriteResourceTrackersToWorkbook(
            IEnumerable<ActivityModel> activities,
            IEnumerable<ResourceModel> resources,
            XlsxWorkbookWriter workbook,
            bool showDates,
            DateTimeOffset projectStart,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(activities);
            ArgumentNullException.ThrowIfNull(resources);
            ArgumentNullException.ThrowIfNull(workbook);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            XlsxDateStyles dateStyles = workbook.CreateDateStyles(DateTimeCalculator.DateFormat, includeTitleDate: true);

            int plannedEndTime = activities
                .Select(x => x.EarliestFinishTime.GetValueOrDefault())
                .DefaultIfEmpty()
                .Max() - 1; // Remove a day because we iterate to this time inclusively.

            List<int> activityIds = [.. activities.Select(x => x.Id).Order()];

            foreach (ResourceModel resource in resources.OrderBy(x => x.Id))
            {
                string sheetTitle = $@"{Resource.ProjectPlan.Reporting.Reporting_WorksheetResourceTracker} ({resource.Id})";
                XlsxSheetWriter sheet = workbook.AddSheet(sheetTitle, dateStyles);
                int rowIndex = 0;
                int endTime = 0;

                if (plannedEndTime > endTime)
                {
                    endTime = plannedEndTime;
                }

                int effortTime = resource.Trackers
                    .DefaultIfEmpty()
                    .Max(x => x?.Time ?? 0);

                if (effortTime > endTime)
                {
                    endTime = effortTime;
                }

                {
                    WriteTrackerTitles(sheet, endTime, new DatesFromStart(showDates, projectStart, dateTimeCalculator));
                    rowIndex++;
                }
                {
                    int resourceId = resource.Id;

                    // Create a lookup dictionary for each time entry.
                    Dictionary<int, Dictionary<int, ResourceActivityTrackerModel>> resourceTrackerLookup = [];

                    foreach (ResourceTrackerModel tracker in resource.Trackers)
                    {
                        if (tracker.ResourceId == resourceId)
                        {
                            resourceTrackerLookup[tracker.Time] = tracker.ActivityTrackers.ToDictionary(x => x.ActivityId);
                        }
                    }

                    // Now cycle through the activities
                    foreach (int activityId in activityIds)
                    {
                        int columnIndex = 0;

                        // Activity Id.
                        AddToCell(activityId, sheet, rowIndex, columnIndex, XlsxCellStyle.Title);
                        columnIndex++;

                        // Tracker values.
                        for (int i = 0; i <= endTime; i++)
                        {
                            if (resourceTrackerLookup.TryGetValue(i, out Dictionary<int, ResourceActivityTrackerModel>? trackerLookup))
                            {
                                if (trackerLookup.TryGetValue(activityId, out ResourceActivityTrackerModel? activityTracker))
                                {
                                    AddToCell(activityTracker.PercentageWorked, sheet, rowIndex, columnIndex);
                                }
                            }

                            columnIndex++;
                        }

                        rowIndex++;
                    }
                }
            }
        }

        private static void WriteResourceChartToWorkbook<T>(
            ResourceSeriesSetModel resourceSeriesSet,
            Func<ResourceSeriesModel, int> resourceAllocationCountFunc,
            Func<ResourceSeriesModel, int, T> resourceSeriesFunc,
            string sheetTitle,
            XlsxWorkbookWriter workbook,
            bool showDates,
            DateTimeOffset projectStart,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(resourceSeriesSet);
            ArgumentNullException.ThrowIfNull(resourceAllocationCountFunc);
            ArgumentNullException.ThrowIfNull(resourceSeriesFunc);
            ArgumentNullException.ThrowIfNull(workbook);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            XlsxDateStyles dateStyles = workbook.CreateDateStyles(DateTimeCalculator.DateFormat);

            XlsxSheetWriter sheet = workbook.AddSheet(sheetTitle, dateStyles);

            IEnumerable<ResourceSeriesModel> combinedResourceSeries = resourceSeriesSet.Combined.OrderBy(x => x.DisplayOrder);

            int rowIndex = 0;

            Debug.Assert(combinedResourceSeries.Select(resourceAllocationCountFunc).Distinct().Count() <= 1);
            int valueCount = combinedResourceSeries.Select(resourceAllocationCountFunc).FirstOrDefault();

            {
                int titleColumnIndex = 0;

                AddToCell(Resource.ProjectPlan.Reporting.Reporting_Time, sheet, rowIndex, titleColumnIndex, XlsxCellStyle.Title);
                titleColumnIndex++;

                foreach (ResourceSeriesModel resourceSeries in combinedResourceSeries)
                {
                    AddToCell(resourceSeries.Title, sheet, rowIndex, titleColumnIndex, XlsxCellStyle.Title);
                    titleColumnIndex++;
                }

                rowIndex++;
            }
            {
                var datesFromStart = new DatesFromStart(showDates, projectStart, dateTimeCalculator);

                // Pivot the series values.
                for (int timeIndex = 0; timeIndex < valueCount; timeIndex++)
                {
                    int columnIndex = 0;

                    // Time.
                    DateFromProjectStart(sheet, rowIndex, columnIndex, timeIndex, XlsxCellStyle.Title, XlsxCellStyle.Date, datesFromStart);
                    columnIndex++;

                    // Values.
                    foreach (ResourceSeriesModel resourceSeries in combinedResourceSeries)
                    {
                        AddToCell(
                            resourceSeriesFunc(resourceSeries, timeIndex),
                            sheet,
                            rowIndex,
                            columnIndex);
                        columnIndex++;
                    }

                    rowIndex++;
                }
            }
        }

        private static void WriteEarnedValueChartToWorkbook(
            IEnumerable<TrackingPointModel> trackingPoints,
            string sheetTitle,
            XlsxWorkbookWriter workbook,
            bool showDates,
            DateTimeOffset projectStart,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(trackingPoints);
            ArgumentNullException.ThrowIfNull(workbook);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            XlsxDateStyles dateStyles = workbook.CreateDateStyles(DateTimeCalculator.DateFormat);

            XlsxSheetWriter sheet = workbook.AddSheet(sheetTitle, dateStyles);

            WriteTitles(sheet, s_TrackingPointColumnTitles);

            int rowIndex = 1;

            {
                Type activityType = typeof(TrackingPointModel);
                PropertyInfo[] propertyInfos = activityType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty);
                var propertyInfoLookup = propertyInfos.ToDictionary(x => x.Name);

                foreach (TrackingPointModel trackingPoint in trackingPoints)
                {
                    int columnIndex = 0;

                    foreach (string columnTitle in s_TrackingPointColumnTitles)
                    {
                        PropertyInfo? propertyInfo = propertyInfoLookup[columnTitle];

                        if (propertyInfo is not null)
                        {
                            object? content = propertyInfo.GetValue(trackingPoint);
                            AddToCell(
                                content,
                                sheet,
                                rowIndex,
                                columnIndex,
                                datesFromStart: GetTrackingPointDatesFromStart(columnTitle, showDates, projectStart, dateTimeCalculator));
                        }
                        columnIndex++;
                    }

                    rowIndex++;
                }
            }
        }

        #endregion

        #region IXlsxScenarioExporter Members

        public void ExportProjectScenarioXlsxFile(
            ProjectScenarioModel projectScenario,
            ResourceSeriesSetModel resourceSeriesSet,
            TrackingSeriesSetModel trackingSeriesSet,
            bool showDates,
            Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            using var workbook = new XlsxWorkbookWriter();

            WriteGeneralToWorkbook(
                projectScenario,
                workbook);

            WriteDisplaySettingsToWorkbook(
                projectScenario,
                workbook);

            WriteResourceSettingsToWorkbook(
                projectScenario,
                workbook);

            WriteActivitiesToWorkbook(
                projectScenario.DependentActivities,
                workbook,
                showDates,
                projectScenario.ProjectStart,
                m_DateTimeCalculator);

            WriteItemsToWorkbook(
                projectScenario.ResourceSettings.Resources,
                s_ResourceColumnTitles,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetResources,
                workbook);

            WriteItemsToWorkbook(
                projectScenario.GraphSettings.ActivitySeverities,
                s_ActivitySeverityColumnTitles,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetActivitySeverities,
                workbook);

            WriteItemsToWorkbook(
                projectScenario.WorkStreamSettings.WorkStreams,
                s_WorkStreamColumnTitles,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetWorkStreams,
                workbook);

            WriteItemsToWorkbook(
                projectScenario.HolidaySettings.Holidays,
                s_HolidayColumnTitles,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetHolidays,
                workbook);

            WriteActivityTrackersToWorkbook(
                projectScenario.DependentActivities.Select(x => x.Activity),
                tracker => tracker.PercentageComplete,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetActivityTracker,
                workbook,
                showDates,
                projectScenario.ProjectStart,
                m_DateTimeCalculator);

            WriteResourceTrackersToWorkbook(
                projectScenario.DependentActivities.Select(x => x.Activity),
                projectScenario.ResourceSettings.Resources,
                workbook,
                showDates,
                projectScenario.ProjectStart,
                m_DateTimeCalculator);

            WriteResourceChartToWorkbook(
                resourceSeriesSet,
                x => x.ResourceSchedule.ResourceAllocation.Count,
                (resourceSeries, timeIndex) => resourceSeries.ResourceSchedule.ResourceAllocation[timeIndex],
                Resource.ProjectPlan.Reporting.Reporting_WorksheetResourceChartActivity,
                workbook,
                showDates,
                projectScenario.ProjectStart,
                m_DateTimeCalculator);

            WriteResourceChartToWorkbook(
                resourceSeriesSet,
                x => x.ResourceSchedule.CostAllocation.Count,
                (resourceSeries, timeIndex) => resourceSeries.ResourceSchedule.CostAllocation[timeIndex] ? resourceSeries.UnitCost : 0.0,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetResourceChartCost,
                workbook,
                showDates,
                projectScenario.ProjectStart,
                m_DateTimeCalculator);

            WriteResourceChartToWorkbook(
                resourceSeriesSet,
                x => x.ResourceSchedule.EffortAllocation.Count,
                (resourceSeries, timeIndex) => resourceSeries.ResourceSchedule.EffortAllocation[timeIndex],
                Resource.ProjectPlan.Reporting.Reporting_WorksheetResourceChartEffort,
                workbook,
                showDates,
                projectScenario.ProjectStart,
                m_DateTimeCalculator);

            WriteEarnedValueChartToWorkbook(
                trackingSeriesSet.Plan,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetEarnedValueChartPlan,
                workbook,
                showDates,
                projectScenario.ProjectStart,
                m_DateTimeCalculator);

            WriteEarnedValueChartToWorkbook(
                trackingSeriesSet.Effort,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetEarnedValueChartEffort,
                workbook,
                showDates,
                projectScenario.ProjectStart,
                m_DateTimeCalculator);

            WriteEarnedValueChartToWorkbook(
                trackingSeriesSet.Progress,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetEarnedValueChartProgress,
                workbook,
                showDates,
                projectScenario.ProjectStart,
                m_DateTimeCalculator);

            // The simulation: only a scenario that has settings of its own, or a result, has the sheets.
            if (XlsxSimulationSheets.HasSimulation(projectScenario))
            {
                XlsxSimulationSheets.WriteToWorkbook(
                    projectScenario,
                    workbook);
            }

            // The workbook is stamped with the calculator's clock and not the wall clock (see XlsxWorkbookWriter.Save).
            workbook.Save(stream, m_DateTimeCalculator.GetLocalNow());
        }

        #endregion
    }
}
