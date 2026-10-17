using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Reads a scenario from a workbook, in the form that the exporter writes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The titles of the columns are the structure of a sheet: a column is read by its title, and a title that the importer
    /// does not know is left alone, as a sheet that it does not know is, and thus a workbook that has more than the importer
    /// reads is a workbook that can be imported. A column that is not there gives the values that it gives when its cells are
    /// blank.
    /// </para>
    /// <para>
    /// The import takes all of a workbook or none of it. It reads the whole workbook, whatever it finds, collects each cell and
    /// each sheet that it cannot read (see <see cref="ImportIssue"/>), and then, if it has collected any,
    /// throws a <see cref="ScenarioImportException"/> that holds them. A blank cell is not a problem (the value is not
    /// given), and a cell that holds something that is not the kind of value of its column is.
    /// </para>
    /// </remarks>
    public class XlsxScenarioFileImporter
        : IXlsxScenarioFileImporter
    {
        #region Fields

        private static readonly IReadOnlyList<string> s_GeneralColumnTitles =
        [
            nameof(ProjectScenarioImportModel.ProjectStart),
            nameof(ProjectScenarioImportModel.Today)
        ];

        private static readonly IReadOnlyList<string> s_ResourceSettingsColumnTitles =
        [
            nameof(ProjectScenarioImportModel.ResourceSettings.DefaultUnitCost),
            nameof(ProjectScenarioImportModel.ResourceSettings.DefaultUnitBilling),
            nameof(ProjectScenarioImportModel.ResourceSettings.AreDisabled)
        ];

        private static readonly IReadOnlyList<string> s_DisplaySettingsColumnTitles =
        [
            nameof(ProjectScenarioModel.DisplaySettings.ShowDates),
            nameof(ProjectScenarioModel.DisplaySettings.UseClassicDates),
            nameof(ProjectScenarioModel.DisplaySettings.NonWorkingDayMode),
            nameof(ProjectScenarioModel.DisplaySettings.HideCost),
            nameof(ProjectScenarioModel.DisplaySettings.HideBilling),
        ];

        private static readonly IReadOnlyList<string> s_ActivityColumnTitles =
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
            nameof(ActivityModel.MinimumFreeSlack),
            nameof(ActivityModel.MinimumEarliestStartTime),
            nameof(ActivityModel.MinimumEarliestStartDateTime),
            nameof(ActivityModel.MaximumLatestFinishTime),
            nameof(ActivityModel.MaximumLatestFinishDateTime),
            nameof(ActivityModel.OverrideColor),
            nameof(ActivityModel.ColorFormat),
            nameof(ActivityModel.Notes),
            nameof(DependentActivityModel.Dependencies),
            nameof(DependentActivityModel.PlanningDependencies)
        ];

        private static readonly IReadOnlyList<string> s_ResourceColumnTitles =
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

        private static readonly IReadOnlyList<string> s_ActivitySeverityColumnTitles =
        [
            nameof(ActivitySeverityModel.SlackLimit),
            nameof(ActivitySeverityModel.CriticalityWeight),
            nameof(ActivitySeverityModel.FibonacciWeight),
            nameof(ActivitySeverityModel.ColorFormat)
        ];

        private static readonly IReadOnlyList<string> s_WorkStreamColumnTitles =
        [
            nameof(WorkStreamModel.Id),
            nameof(WorkStreamModel.Name),
            nameof(WorkStreamModel.IsPhase),
            nameof(WorkStreamModel.DisplayOrder),
            nameof(WorkStreamModel.ColorFormat)
        ];

        private static readonly IReadOnlyList<string> s_HolidayColumnTitles =
        [
            nameof(HolidayModel.Id),
            nameof(HolidayModel.Name),
            nameof(HolidayModel.StartDateTime),
            nameof(HolidayModel.RecurrencePattern),
            nameof(HolidayModel.Notes)
        ];

        // The sheets of the trackers have one column that is read by its title, the Id. The columns after it are the times, and
        // their titles are the times (or the days that they are, when the workbook shows dates).
        private static readonly IReadOnlyList<string> s_TrackerColumnTitles =
        [
            nameof(ActivityModel.Id)
        ];

        private readonly IDateTimeCalculator m_DateTimeCalculator;

        #endregion

        #region Ctors

        public XlsxScenarioFileImporter(IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            m_DateTimeCalculator = dateTimeCalculator;
        }

        #endregion

        #region Private Members

        // Says that the Id of a row is the Id of an earlier row, if it is. The row that has the Id again is not kept: the
        // import fails, and the list of problems says where each of the two is.
        private static bool IsNewId(
            ImportRow row,
            int idIndex,
            int id,
            Dictionary<int, int> firstRowOfId)
        {
            if (firstRowOfId.TryGetValue(id, out int firstRow))
            {
                row.Report(idIndex, ImportIssueCodes.DuplicateId, ImportIssueText.DuplicateId(id, firstRow));
                return false;
            }

            firstRowOfId.Add(id, row.Number);
            return true;
        }

        // The activities that are on the sheet of activities. The second value says whether the sheet has been read without a
        // problem: only then do the trackers know that an activity that is not in the list is an activity that the workbook does
        // not have, and not one that could not be read.
        private static (Dictionary<int, DependentActivityModel> Activities, bool IsComplete) ImportWorksheetActivities(
            XlsxWorkbookReader reader,
            ImportIssueCollector issues)
        {
            Dictionary<int, DependentActivityModel> dependentActivities = [];
            int issueCount = issues.Count;
            ImportTable? table = reader.Table(Resource.ProjectPlan.Reporting.Reporting_WorksheetActivities, s_ActivityColumnTitles);

            if (table is null)
            {
                return (dependentActivities, false);
            }

            if (!table.RequireColumn(nameof(ActivityModel.Id)))
            {
                return (dependentActivities, false);
            }

            int idIndex = table.IndexOf(nameof(ActivityModel.Id)).GetValueOrDefault();
            Dictionary<int, int> firstRowOfId = [];

            foreach (ImportRow row in table.Rows)
            {
                int? id = row.RequiredId(nameof(ActivityModel.Id));

                // A blank cell, or a workbook with no such column, is an estimate that was not given.
                // It follows the one before it, and the activity is then exactly what the Duration says.
                var activity = new ActivityModel
                {
                    Id = id.GetValueOrDefault(),
                    DisplayOrder = row.Int(nameof(ActivityModel.DisplayOrder)) ?? 0,
                    Name = row.Text(nameof(ActivityModel.Name)) ?? string.Empty,
                    TargetWorkStreams = row.Ids(nameof(ActivityModel.TargetWorkStreams)),
                    TargetResources = row.Ids(nameof(ActivityModel.TargetResources)),
                    TargetResourceOperator = row.Option<LogicalOperator>(nameof(ActivityModel.TargetResourceOperator)) ?? LogicalOperator.AND,
                    HasNoCost = row.Bool(nameof(ActivityModel.HasNoCost)) ?? false,
                    HasNoBilling = row.Bool(nameof(ActivityModel.HasNoBilling)) ?? false,
                    HasNoEffort = row.Bool(nameof(ActivityModel.HasNoEffort)) ?? false,
                    HasNoRisk = row.Bool(nameof(ActivityModel.HasNoRisk)) ?? false,
                    Duration = row.Int(nameof(ActivityModel.Duration)) ?? 0,
                    MostLikelyDuration = row.Int(nameof(ActivityModel.MostLikelyDuration)),
                    OptimisticDuration = row.Int(nameof(ActivityModel.OptimisticDuration)),
                    PessimisticDuration = row.Int(nameof(ActivityModel.PessimisticDuration)),
                    MinimumFreeSlack = row.Int(nameof(ActivityModel.MinimumFreeSlack)),
                    MinimumEarliestStartTime = row.Int(nameof(ActivityModel.MinimumEarliestStartTime)),
                    MinimumEarliestStartDateTime = ToOffset(row.Date(nameof(ActivityModel.MinimumEarliestStartDateTime))),
                    MaximumLatestFinishTime = row.Int(nameof(ActivityModel.MaximumLatestFinishTime)),
                    MaximumLatestFinishDateTime = ToOffset(row.Date(nameof(ActivityModel.MaximumLatestFinishDateTime))),
                    OverrideColor = row.Bool(nameof(ActivityModel.OverrideColor)) ?? false,
                    ColorFormat = row.Color(nameof(ActivityModel.ColorFormat)) ?? ColorHelper.None(),
                    Notes = row.Text(nameof(ActivityModel.Notes)) ?? string.Empty,
                };

                List<int> dependencies = row.Ids(nameof(DependentActivityModel.Dependencies));
                List<int> planningDependencies = row.Ids(nameof(DependentActivityModel.PlanningDependencies));

                if (id is int idValue
                    && IsNewId(row, idIndex, idValue, firstRowOfId))
                {
                    dependentActivities[idValue] = new DependentActivityModel
                    {
                        Activity = activity,
                        Dependencies = dependencies,
                        PlanningDependencies = planningDependencies
                    };
                }
            }

            return (dependentActivities, issues.Count == issueCount);
        }

        private static DateTimeOffset? ToOffset(DateTime? date) =>
            date is DateTime value
                ? new DateTimeOffset(value)
                : null;

        private static Dictionary<int, ResourceModel> ImportWorksheetResources(XlsxWorkbookReader reader)
        {
            Dictionary<int, ResourceModel> resources = [];
            ImportTable? table = reader.Table(Resource.ProjectPlan.Reporting.Reporting_WorksheetResources, s_ResourceColumnTitles);

            if (table is null
                || !table.RequireColumn(nameof(ResourceModel.Id)))
            {
                return resources;
            }

            int idIndex = table.IndexOf(nameof(ResourceModel.Id)).GetValueOrDefault();
            Dictionary<int, int> firstRowOfId = [];

            foreach (ImportRow row in table.Rows)
            {
                int? id = row.RequiredId(nameof(ResourceModel.Id));

                var resource = new ResourceModel
                {
                    Id = id.GetValueOrDefault(),
                    Name = row.Text(nameof(ResourceModel.Name)) ?? string.Empty,
                    Notes = row.Text(nameof(ResourceModel.Notes)) ?? string.Empty,
                    IsExplicitTarget = row.Bool(nameof(ResourceModel.IsExplicitTarget)) ?? false,
                    IsInactive = row.Bool(nameof(ResourceModel.IsInactive)) ?? false,
                    ActivityAllocationType = row.Option<ActivityAllocationType>(nameof(ResourceModel.ActivityAllocationType)) ?? ActivityAllocationType.Direct,
                    InterActivityAllocationType = row.Option<InterActivityAllocationType>(nameof(ResourceModel.InterActivityAllocationType)) ?? InterActivityAllocationType.None,
                    InterActivityPhases = row.Ids(nameof(ResourceModel.InterActivityPhases)),
                    UnitCost = row.Double(nameof(ResourceModel.UnitCost)) ?? 0.0,
                    UnitBilling = row.Double(nameof(ResourceModel.UnitBilling)) ?? 0.0,
                    FixedCost = row.Double(nameof(ResourceModel.FixedCost)) ?? 0.0,
                    FixedBilling = row.Double(nameof(ResourceModel.FixedBilling)) ?? 0.0,
                    DisplayOrder = row.Int(nameof(ResourceModel.DisplayOrder)) ?? 0,
                    AllocationOrder = row.Int(nameof(ResourceModel.AllocationOrder)) ?? 0,
                    ColorFormat = row.Color(nameof(ResourceModel.ColorFormat)) ?? ColorHelper.None(),
                };

                if (id is int idValue
                    && IsNewId(row, idIndex, idValue, firstRowOfId))
                {
                    resources[idValue] = resource;
                }
            }

            return resources;
        }

        private static List<ActivitySeverityModel> ImportWorksheetActivitySeverities(XlsxWorkbookReader reader)
        {
            List<ActivitySeverityModel> activitySeverities = [];
            ImportTable? table = reader.Table(Resource.ProjectPlan.Reporting.Reporting_WorksheetActivitySeverities, s_ActivitySeverityColumnTitles);

            if (table is null)
            {
                return activitySeverities;
            }

            foreach (ImportRow row in table.Rows)
            {
                activitySeverities.Add(new ActivitySeverityModel
                {
                    SlackLimit = row.Int(nameof(ActivitySeverityModel.SlackLimit)) ?? 0,
                    CriticalityWeight = row.Double(nameof(ActivitySeverityModel.CriticalityWeight)) ?? 0.0,
                    FibonacciWeight = row.Double(nameof(ActivitySeverityModel.FibonacciWeight)) ?? 0.0,
                    ColorFormat = row.Color(nameof(ActivitySeverityModel.ColorFormat)) ?? ColorHelper.None(),
                });
            }

            return activitySeverities;
        }

        private static Dictionary<int, WorkStreamModel> ImportWorksheetWorkStreams(XlsxWorkbookReader reader)
        {
            Dictionary<int, WorkStreamModel> workStreams = [];
            ImportTable? table = reader.Table(Resource.ProjectPlan.Reporting.Reporting_WorksheetWorkStreams, s_WorkStreamColumnTitles);

            if (table is null
                || !table.RequireColumn(nameof(WorkStreamModel.Id)))
            {
                return workStreams;
            }

            int idIndex = table.IndexOf(nameof(WorkStreamModel.Id)).GetValueOrDefault();
            Dictionary<int, int> firstRowOfId = [];

            foreach (ImportRow row in table.Rows)
            {
                int? id = row.RequiredId(nameof(WorkStreamModel.Id));

                var workStream = new WorkStreamModel
                {
                    Id = id.GetValueOrDefault(),
                    Name = row.Text(nameof(WorkStreamModel.Name)) ?? string.Empty,
                    IsPhase = row.Bool(nameof(WorkStreamModel.IsPhase)) ?? false,
                    DisplayOrder = row.Int(nameof(WorkStreamModel.DisplayOrder)) ?? 0,
                    ColorFormat = row.Color(nameof(WorkStreamModel.ColorFormat)) ?? ColorHelper.None(),
                };

                if (id is int idValue
                    && IsNewId(row, idIndex, idValue, firstRowOfId))
                {
                    workStreams[idValue] = workStream;
                }
            }

            return workStreams;
        }

        private static Dictionary<int, HolidayModel> ImportWorksheetHolidays(XlsxWorkbookReader reader)
        {
            Dictionary<int, HolidayModel> holidays = [];
            ImportTable? table = reader.Table(Resource.ProjectPlan.Reporting.Reporting_WorksheetHolidays, s_HolidayColumnTitles);

            if (table is null
                || !table.RequireColumn(nameof(HolidayModel.Id)))
            {
                return holidays;
            }

            int idIndex = table.IndexOf(nameof(HolidayModel.Id)).GetValueOrDefault();
            Dictionary<int, int> firstRowOfId = [];

            foreach (ImportRow row in table.Rows)
            {
                int? id = row.RequiredId(nameof(HolidayModel.Id));

                var holiday = new HolidayModel
                {
                    Id = id.GetValueOrDefault(),
                    Name = row.Text(nameof(HolidayModel.Name)) ?? string.Empty,
                    StartDateTime = ToOffset(row.Date(nameof(HolidayModel.StartDateTime))),
                    RecurrencePattern = row.Text(nameof(HolidayModel.RecurrencePattern)) ?? string.Empty,
                    Notes = row.Text(nameof(HolidayModel.Notes)) ?? string.Empty,
                };

                if (id is int idValue
                    && IsNewId(row, idIndex, idValue, firstRowOfId))
                {
                    holidays[idValue] = holiday;
                }
            }

            return holidays;
        }

        // The sheet of a tracker: a row for each activity, with the Id in the column titled Id, and a percentage in each column
        // after it. The time of a percentage is the place of its column after the Id: the first is time 0.
        // A row for an Id that the workbook does not have as an activity is a problem, when the activities were read in full.
        private static void ImportTrackerRows(
            ImportTable table,
            IDictionary<int, DependentActivityModel> dependentActivityLookup,
            bool areActivitiesComplete,
            Action<DependentActivityModel, ImportRow, int> readRow)
        {
            if (!table.RequireColumn(nameof(ActivityModel.Id)))
            {
                return;
            }

            int idIndex = table.IndexOf(nameof(ActivityModel.Id)).GetValueOrDefault();
            Dictionary<int, int> firstRowOfId = [];

            foreach (ImportRow row in table.Rows)
            {
                int? id = row.RequiredId(nameof(ActivityModel.Id));

                if (id is not int idValue
                    || !IsNewId(row, idIndex, idValue, firstRowOfId))
                {
                    continue;
                }

                if (dependentActivityLookup.TryGetValue(idValue, out DependentActivityModel? dependentActivity))
                {
                    readRow(dependentActivity, row, idIndex);
                }
                else if (areActivitiesComplete)
                {
                    row.Report(idIndex, ImportIssueCodes.UnknownActivity, ImportIssueText.UnknownActivity(idValue));
                }
            }
        }

        private static void ImportWorksheetActivityTrackers(
            XlsxWorkbookReader reader,
            IDictionary<int, DependentActivityModel> dependentActivityLookup,
            bool areActivitiesComplete)
        {
            ImportTable? table = reader.Table(Resource.ProjectPlan.Reporting.Reporting_WorksheetActivityTracker, s_TrackerColumnTitles);

            if (table is null)
            {
                return;
            }

            ImportTrackerRows(
                table,
                dependentActivityLookup,
                areActivitiesComplete,
                (dependentActivity, row, idIndex) =>
                {
                    dependentActivity.Activity.Trackers.Clear();

                    for (int columnIndex = idIndex + 1; columnIndex < table.Titles.Count; columnIndex++)
                    {
                        int percentageComplete = row.IntAt(columnIndex) ?? 0;

                        if (percentageComplete > 0)
                        {
                            dependentActivity.Activity.Trackers.Add(new ActivityTrackerModel
                            {
                                Time = columnIndex - idIndex - 1,
                                ActivityId = dependentActivity.Activity.Id,
                                PercentageComplete = percentageComplete
                            });
                        }
                    }
                });
        }

        private static void ImportWorksheetResourceTrackers(
            XlsxWorkbookReader reader,
            ResourceModel resource,
            IDictionary<int, DependentActivityModel> dependentActivityLookup,
            bool areActivitiesComplete)
        {
            int resourceId = resource.Id;
            ImportTable? table = reader.Table($@"{Resource.ProjectPlan.Reporting.Reporting_WorksheetResourceTracker} ({resourceId})", s_TrackerColumnTitles);

            if (table is null)
            {
                return;
            }

            resource.Trackers.Clear();

            // Create a lookup dictionary for each time entry.
            Dictionary<int, Dictionary<int, ResourceActivityTrackerModel>> resourceTrackerLookup = [];

            ImportTrackerRows(
                table,
                dependentActivityLookup,
                areActivitiesComplete,
                (dependentActivity, row, idIndex) =>
                {
                    int activityId = dependentActivity.Activity.Id;
                    string activityName = dependentActivity.Activity.Name;

                    for (int columnIndex = idIndex + 1; columnIndex < table.Titles.Count; columnIndex++)
                    {
                        int percentageWorked = row.IntAt(columnIndex) ?? 0;

                        if (percentageWorked > 0)
                        {
                            int time = columnIndex - idIndex - 1;

                            if (!resourceTrackerLookup.TryGetValue(time, out Dictionary<int, ResourceActivityTrackerModel>? trackerLookup))
                            {
                                trackerLookup = [];
                                resourceTrackerLookup.Add(time, trackerLookup);
                            }

                            trackerLookup.TryAdd(
                                activityId,
                                new ResourceActivityTrackerModel
                                {
                                    Time = time,
                                    ResourceId = resourceId,
                                    ActivityId = activityId,
                                    ActivityName = activityName,
                                    PercentageWorked = percentageWorked,
                                });
                        }
                    }
                });

            foreach (KeyValuePair<int, Dictionary<int, ResourceActivityTrackerModel>> kvp in resourceTrackerLookup)
            {
                var tracker = new ResourceTrackerModel
                {
                    Time = kvp.Key,
                    ResourceId = resourceId,
                };
                tracker.ActivityTrackers.AddRange(kvp.Value.Values);

                resource.Trackers.Add(tracker);
            }
        }

        #endregion

        #region IXlsxFileImporter Members

        public ProjectScenarioImportModel ImportProjectScenarioXlsxFile(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            var issues = new ImportIssueCollector();
            XlsxWorkbookReader reader = XlsxWorkbookReader.Open(stream, issues);
            DateTimeOffset projectStart = m_DateTimeCalculator.GetLocalToday();
            DateTimeOffset today = projectStart;

            {
                ImportTable? table = reader.Table(Resource.ProjectPlan.Reporting.Reporting_WorksheetGeneral, s_GeneralColumnTitles);

                foreach (ImportRow row in table?.Rows ?? [])
                {
                    projectStart = row.DateOffset(nameof(ProjectScenarioImportModel.ProjectStart)) ?? projectStart;
                    today = row.DateOffset(nameof(ProjectScenarioImportModel.Today)) ?? today;
                }
            }

            bool showDates = false;
            bool useClassicDates = false;
            NonWorkingDayMode nonWorkingDayMode = NonWorkingDayMode.None;
            bool hideCost = false;
            bool hideBilling = false;

            {
                ImportTable? table = reader.Table(Resource.ProjectPlan.Reporting.Reporting_WorksheetDisplaySettings, s_DisplaySettingsColumnTitles);

                foreach (ImportRow row in table?.Rows ?? [])
                {
                    showDates = row.Bool(nameof(ProjectScenarioModel.DisplaySettings.ShowDates)) ?? showDates;
                    useClassicDates = row.Bool(nameof(ProjectScenarioModel.DisplaySettings.UseClassicDates)) ?? useClassicDates;
                    nonWorkingDayMode = row.Option<NonWorkingDayMode>(nameof(ProjectScenarioModel.DisplaySettings.NonWorkingDayMode)) ?? nonWorkingDayMode;
                    hideCost = row.Bool(nameof(ProjectScenarioModel.DisplaySettings.HideCost)) ?? hideCost;
                    hideBilling = row.Bool(nameof(ProjectScenarioModel.DisplaySettings.HideBilling)) ?? hideBilling;
                }
            }

            double defaultUnitCost = 1.0;
            double defaultUnitBilling = 1.0;
            bool areDisabled = false;

            {
                ImportTable? table = reader.Table(Resource.ProjectPlan.Reporting.Reporting_WorksheetResourceSettings, s_ResourceSettingsColumnTitles);

                foreach (ImportRow row in table?.Rows ?? [])
                {
                    defaultUnitCost = row.Double(nameof(ProjectScenarioImportModel.ResourceSettings.DefaultUnitCost)) ?? defaultUnitCost;
                    defaultUnitBilling = row.Double(nameof(ProjectScenarioImportModel.ResourceSettings.DefaultUnitBilling)) ?? defaultUnitBilling;
                    areDisabled = row.Bool(nameof(ProjectScenarioImportModel.ResourceSettings.AreDisabled)) ?? areDisabled;
                }
            }

            (Dictionary<int, DependentActivityModel> dependentActivities, bool areActivitiesComplete) = ImportWorksheetActivities(reader, issues);
            Dictionary<int, ResourceModel> resources = ImportWorksheetResources(reader);
            List<ActivitySeverityModel> activitySeverities = ImportWorksheetActivitySeverities(reader);
            Dictionary<int, WorkStreamModel> workStreams = ImportWorksheetWorkStreams(reader);
            Dictionary<int, HolidayModel> holidays = ImportWorksheetHolidays(reader);

            ImportWorksheetActivityTrackers(reader, dependentActivities, areActivitiesComplete);

            foreach (ResourceModel resource in resources.Values)
            {
                ImportWorksheetResourceTrackers(reader, resource, dependentActivities, areActivitiesComplete);
            }

            (SimulationSettingsModel simulationSettings, SimulationSummaryModel? simulationSummary) =
                XlsxSimulationSheets.ReadFromWorkbook(reader);

            // A workbook with none of the sheets that the import reads is not a workbook of this application, and an import of
            // it would give an empty plan and no word of why.
            if (!reader.HasFoundASheet)
            {
                issues.Add(new ImportIssue
                {
                    Code = ImportIssueCodes.NoKnownSheet,
                    Message = ImportIssueText.NoKnownSheet(),
                });
            }

            issues.ThrowIfAny();

            return new ProjectScenarioImportModel
            {
                ProjectStart = projectStart,
                Today = today,
                DependentActivities = [.. dependentActivities.Values],
                ResourceSettings = new()
                {
                    Resources = [.. resources.Values],
                    DefaultUnitCost = defaultUnitCost,
                    DefaultUnitBilling = defaultUnitBilling,
                    AreDisabled = areDisabled,
                },
                ActivitySeverities = [.. activitySeverities],
                WorkStreams = [.. workStreams.Values],
                Holidays = [.. holidays.Values],

                DisplaySettings = new()
                {
                    ShowDates = showDates,
                    UseClassicDates = useClassicDates,
                    NonWorkingDayMode = nonWorkingDayMode,
                    HideCost = hideCost,
                    HideBilling = hideBilling,
                },

                SimulationSettings = simulationSettings,
                SimulationSummary = simulationSummary,
            };
        }

        #endregion
    }
}
