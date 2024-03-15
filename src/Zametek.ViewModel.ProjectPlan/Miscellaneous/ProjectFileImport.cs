using net.sf.mpxj.MpxjUtilities;
using net.sf.mpxj.reader;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ProjectFileImport
        : IProjectFileImport
    {
        #region Fields

        private static readonly IList<string> s_GeneralColumnTitles = new List<string>
        {
            nameof(ProjectImportModel.ProjectStart),
            nameof(ProjectImportModel.DefaultUnitCost)
        };

        private static readonly IList<string> s_ActivityColumnTitles = new List<string>
        {
            nameof(ActivityModel.Id),
            nameof(ActivityModel.Name),
            nameof(ActivityModel.TargetResources),
            nameof(ActivityModel.TargetResourceOperator),
            nameof(ActivityModel.AllocatedToResources),
            nameof(ActivityModel.HasNoCost),
            nameof(ActivityModel.Duration),
            nameof(ActivityModel.MinimumFreeSlack),
            nameof(ActivityModel.MinimumEarliestStartTime),
            nameof(ActivityModel.MinimumEarliestStartDateTime),
            nameof(ActivityModel.MaximumLatestFinishTime),
            nameof(ActivityModel.MaximumLatestFinishDateTime),
            nameof(ActivityModel.Notes)
        };

        private static readonly IList<string> s_DependentActivityColumnTitles = new List<string>
        {
            nameof(DependentActivityModel.Dependencies)
        };

        private static readonly IList<string> s_ResourceColumnTitles = new List<string>
        {
            nameof(ResourceModel.Id),
            nameof(ResourceModel.Name),
            nameof(ResourceModel.IsExplicitTarget),
            nameof(ResourceModel.IsInactive),
            nameof(ResourceModel.InterActivityAllocationType),
            nameof(ResourceModel.UnitCost),
            nameof(ResourceModel.DisplayOrder),
            nameof(ResourceModel.AllocationOrder),
            nameof(ResourceModel.ColorFormat)
        };

        private static readonly IList<string> s_ActivitySeverityColumnTitles = new List<string>
        {
            nameof(ActivitySeverityModel.SlackLimit),
            nameof(ActivitySeverityModel.CriticalityWeight),
            nameof(ActivitySeverityModel.FibonacciWeight),
            nameof(ActivitySeverityModel.ColorFormat)
        };

        private static readonly int[] s_FilterTaskIds = [0];

        private static readonly int[] s_FilterResourceIds = [0];

        #endregion

        private static DataTable SheetToDataTable(ISheet sheet)
        {
            ArgumentNullException.ThrowIfNull(sheet);
            DataTable dtTable = new();
            IRow titleRow = sheet.GetRow(0);
            int cellCount = titleRow.LastCellNum;

            for (int i = 0; i < cellCount; i++)
            {
                ICell cell = titleRow.GetCell(i);

                if (cell is null || string.IsNullOrWhiteSpace(cell.ToString()))
                {
                    cellCount = i;
                    break;
                }

                dtTable.Columns.Add(cell.ToString());
            }

            List<string> rowList = [];
            for (int i = sheet.FirstRowNum + 1; i <= sheet.LastRowNum; i++)
            {
                IRow row = sheet.GetRow(i);
                if (row is null
                    || row.Cells.All(d => d.CellType == CellType.Blank))
                {
                    continue;
                }
                for (int j = row.FirstCellNum; j < cellCount; j++)
                {
                    ICell cell = row.GetCell(j);
                    string content = cell?.ToString() ?? string.Empty;
                    rowList.Add(content);
                }
                if (rowList.Count > 0)
                {
                    dtTable.Rows.Add(rowList.ToArray());
                }
                rowList.Clear();
            }

            return dtTable;
        }

        #region IProjectFileImport Members

        public ProjectImportModel ImportProjectFile(string filename)
        {
            string fileExtension = Path.GetExtension(filename);

            Func<string, ProjectImportModel> func =
                filename => throw new ArgumentOutOfRangeException(
                    nameof(filename),
                    @$"{Resource.ProjectPlan.Messages.Message_UnableToImportFile} {filename}");

            fileExtension.ValueSwitchOn()
                .Case($".{Resource.ProjectPlan.Filters.Filter_MicrosoftProjectMppFileExtension}", _ => func = ImportMicrosoftProjectFile)
                .Case($".{Resource.ProjectPlan.Filters.Filter_MicrosoftProjectXmlFileExtension}", _ => func = ImportMicrosoftProjectFile)
                .Case($".{Resource.ProjectPlan.Filters.Filter_ProjectXlsxFileExtension}", _ => func = ImportProjectXlsxFile);

            return func(filename);
        }

        public async Task<ProjectImportModel> ImportProjectFileAsync(string filename)
        {
            return await Task.Run(() => ImportProjectFile(filename));
        }

        public ProjectImportModel ImportMicrosoftProjectFile(string filename)
        {
            var reader = new UniversalProjectReader();
            net.sf.mpxj.ProjectFile mpxjProjectFile = reader.read(filename);
            net.sf.mpxj.ProjectProperties? props = mpxjProjectFile.ProjectProperties;
            DateTimeOffset projectStart = props?.StartDate?.ToDateTime() ?? DateTimeOffset.Now;
            TimeSpan projectStartOffset = projectStart.Offset;

            var resources = new List<ResourceModel>();

            foreach (net.sf.mpxj.Resource mpxjResource in mpxjProjectFile.Resources.ToIEnumerable<net.sf.mpxj.Resource>())
            {
                int id = mpxjResource.ID?.intValue() ?? default;
                if (s_FilterResourceIds.Contains(id))
                {
                    continue;
                }
                var resource = new ResourceModel
                {
                    Id = id,
                    IsExplicitTarget = true,
                    IsInactive = false,
                    Name = mpxjResource.Name ?? string.Empty,
                    DisplayOrder = id,
                    ColorFormat = ColorHelper.RandomColor()
                };
                resources.Add(resource);
            }

            var dependentActivities = new List<DependentActivityModel>();

            IList<net.sf.mpxj.Task> mpxjTasks = mpxjProjectFile.Tasks.ToIEnumerable<net.sf.mpxj.Task>().ToList();

            foreach (net.sf.mpxj.Task mpxjTask in mpxjTasks)
            {
                int id = mpxjTask.ID?.intValue() ?? default;
                if (s_FilterTaskIds.Contains(id))
                {
                    continue;
                }

                java.util.List? preds = mpxjTask.Predecessors; // Predecessors == Dependencies
                if (preds is not null
                    && !preds.isEmpty())
                {
                    foreach (net.sf.mpxj.Relation pred in preds.ToIEnumerable<net.sf.mpxj.Relation>().ToList())
                    {
                        // For every dependency that is also a parent task, add all its children as dependencies.

                        foreach (net.sf.mpxj.Task descendantTask in GetDescendantTasks(pred.TargetTask))
                        {
                            int? descendantTaskId = descendantTask.ID?.intValue();

                            if (descendantTaskId is not null
                                && id != descendantTaskId)
                            {
                                mpxjTask.AddPredecessor(
                                    descendantTask,
                                    net.sf.mpxj.RelationType.START_FINISH,
                                    net.sf.mpxj.Duration.getInstance(0.0, mpxjTask.Duration.Units));
                            }
                        }
                    }
                }

                // Add parent tasks as predecessors.

                net.sf.mpxj.Task parentTask = mpxjTask.ParentTask;

                if (parentTask is not null)
                {
                    int parentId = parentTask.ID?.intValue() ?? default;
                    if (!s_FilterTaskIds.Contains(parentId))
                    {
                        mpxjTask.AddPredecessor(
                            parentTask,
                            net.sf.mpxj.RelationType.START_FINISH,
                            net.sf.mpxj.Duration.getInstance(0.0, mpxjTask.Duration.Units));
                    }
                }

                // If the task is a parent task, ignore duration.

                if (mpxjTask.HasChildTasks())
                {
                    mpxjTask.Duration = net.sf.mpxj.Duration.getInstance(0.0, mpxjTask.Duration.Units);
                }

                DependentActivityModel? dependentActivity = ConvertTask(mpxjTask, projectStartOffset);

                if (dependentActivity is not null)
                {
                    dependentActivities.Add(dependentActivity);
                }
            }

            return new ProjectImportModel
            {
                ProjectStart = projectStart,
                DependentActivities = dependentActivities,
                Resources = resources
            };
        }

        public ProjectImportModel ImportProjectXlsxFile(string filename)
        {
            using FileStream file = new(filename, FileMode.Open, FileAccess.Read);

            IWorkbook workbook = new XSSFWorkbook(file);
            DateTimeOffset projectStart = new(DateTime.Today);

            double defaultUnitCost = 1;

            ISheet? sheet = workbook?.GetSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetGeneral);
            if (sheet is not null)
            {
                DataTable dtTable = SheetToDataTable(sheet);
                DataColumnCollection columns = dtTable.Columns;

                // Check columns.
                var columnNames = new List<string>();

                foreach (string title in s_GeneralColumnTitles)
                {
                    if (columns.Contains(title))
                    {
                        columnNames.Add(title);
                    }
                }

                foreach (DataRow row in dtTable.Rows)
                {
                    foreach (string columnName in columnNames)
                    {
                        columnName.ValueSwitchOn()
                            .Case(nameof(ProjectImportModel.ProjectStart),
                                name =>
                                {
                                    if (DateTime.TryParse(row[name]?.ToString(), out DateTime output))
                                    {
                                        projectStart = new DateTimeOffset(output);
                                    }
                                })
                            .Case(nameof(ProjectImportModel.DefaultUnitCost),
                                name =>
                                {
                                    if (double.TryParse(row[name]?.ToString(), out double output))
                                    {
                                        defaultUnitCost = output;
                                    }
                                });
                    }
                }
            }

            IDictionary<int, DependentActivityModel> dependentActivities = ImportWorksheetActivities(workbook);
            IDictionary<int, ResourceModel> resources = ImportWorksheetResources(workbook);
            IList<ActivitySeverityModel> activitySeverities = ImportWorksheetActivitySeverities(workbook);
            dependentActivities = ImportWorksheetTrackers(workbook, dependentActivities);

            return new ProjectImportModel
            {
                ProjectStart = projectStart,
                DependentActivities = [.. dependentActivities.Values],
                Resources = [.. resources.Values],
                DefaultUnitCost = defaultUnitCost,
                ActivitySeverities = [.. activitySeverities],
            };
        }

        private static IEnumerable<net.sf.mpxj.Task> GetDescendantTasks(net.sf.mpxj.Task parentTask)
        {
            if (parentTask.HasChildTasks())
            {
                foreach (net.sf.mpxj.Task childTask in parentTask.ChildTasks.ToIEnumerable<net.sf.mpxj.Task>())
                {
                    foreach (net.sf.mpxj.Task grandChildTask in GetDescendantTasks(childTask))
                    {
                        yield return grandChildTask;
                    }

                    yield return childTask;
                }
            }
            yield return parentTask;
        }

        private static DependentActivityModel? ConvertTask(
            net.sf.mpxj.Task mpxjTask,
            TimeSpan projectStartOffset)
        {
            int id = mpxjTask.ID?.intValue() ?? default;
            if (s_FilterTaskIds.Contains(id))
            {
                return null;
            }

            int duration = Convert.ToInt32(mpxjTask.Duration?.Duration ?? default);

            DateTimeOffset? minimumEarliestStartDateTime = null;
            if (mpxjTask.ConstraintType == net.sf.mpxj.ConstraintType.START_NO_EARLIER_THAN)
            {
                DateTime? mpxContraintDate = mpxjTask.ConstraintDate?.ToDateTime();

                if (mpxContraintDate is not null)
                {
                    // Ensure each value has the same offset as the project start;
                    minimumEarliestStartDateTime = new DateTimeOffset(mpxContraintDate.GetValueOrDefault(), projectStartOffset);
                }
            }

            var targetResources = new List<int>();
            foreach (net.sf.mpxj.ResourceAssignment resourceAssignment in mpxjTask.ResourceAssignments.ToIEnumerable<net.sf.mpxj.ResourceAssignment>())
            {
                int? mpxResourceId = resourceAssignment.Resource?.ID?.intValue();

                if (mpxResourceId is not null)
                {
                    targetResources.Add(mpxResourceId.GetValueOrDefault());
                }
            }

            var dependencies = new List<int>();
            java.util.List? preds = mpxjTask.Predecessors;
            if (preds is not null && !preds.isEmpty())
            {
                foreach (net.sf.mpxj.Relation pred in preds.ToIEnumerable<net.sf.mpxj.Relation>())
                {
                    int? dependentTaskId = pred.TargetTask?.ID?.intValue();

                    if (dependentTaskId is not null)
                    {
                        dependencies.Add(dependentTaskId.GetValueOrDefault());
                    }
                }
            }
            var dependentActivity = new DependentActivityModel
            {
                Activity = new ActivityModel
                {
                    Id = id,
                    Name = mpxjTask.Name ?? string.Empty,
                    TargetResources = targetResources,
                    Duration = duration,
                    MinimumEarliestStartDateTime = minimumEarliestStartDateTime
                },
                Dependencies = dependencies,
            };
            return dependentActivity;
        }

        private static IDictionary<int, DependentActivityModel> ImportWorksheetActivities(IWorkbook? workbook)
        {
            Dictionary<int, DependentActivityModel> dependentActivities = [];
            ISheet? sheet = workbook?.GetSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetActivities);
            if (sheet is not null)
            {
                DataTable dtTable = SheetToDataTable(sheet);
                DataColumnCollection columns = dtTable.Columns;

                // Check columns.
                var activityColumnNames = new List<string>();
                var dependentActivityColumnNames = new List<string>();

                foreach (string title in s_ActivityColumnTitles)
                {
                    if (columns.Contains(title))
                    {
                        activityColumnNames.Add(title);
                    }
                }
                foreach (string title in s_DependentActivityColumnTitles)
                {
                    if (columns.Contains(title))
                    {
                        dependentActivityColumnNames.Add(title);
                    }
                }

                foreach (DataRow row in dtTable.Rows)
                {
                    int? id = 0;
                    string name = string.Empty;
                    List<int> targetResources = [];
                    var targetResourceOperator = LogicalOperator.AND;
                    bool hasNoCost = false;
                    int duration = 0;
                    int? minimumFreeSlack = null;
                    int? minimumEarliestStartTime = null;
                    DateTimeOffset? minimumEarliestStartDateTime = null;
                    int? maximumLatestFinishTime = null;
                    DateTimeOffset? maximumLatestFinishDateTime = null;
                    string notes = string.Empty;
                    List<int> dependencies = [];

                    foreach (string columnName in activityColumnNames)
                    {
                        columnName.ValueSwitchOn()
                            .Case(nameof(ActivityModel.Id),
                                colName =>
                                {
                                    if (int.TryParse(row[colName]?.ToString(), out int output))
                                    {
                                        id = output;
                                    }
                                })
                            .Case(nameof(ActivityModel.Name),
                                colName => name = row[colName]?.ToString() ?? string.Empty)
                            .Case(nameof(ActivityModel.TargetResources),
                                colName =>
                                {
                                    string targetResourcesString = row[colName]?.ToString() ?? string.Empty;
                                    foreach (string targetResource in targetResourcesString.Split(DependenciesStringValidationRule.Separator))
                                    {
                                        if (int.TryParse(targetResource, out int output))
                                        {
                                            targetResources.Add(output);
                                        }
                                    }
                                })
                            .Case(nameof(ActivityModel.TargetResourceOperator),
                                colName => targetResourceOperator = row[colName]?.ToString().GetValueFromDescription<LogicalOperator>() ?? default)
                            .Case(nameof(ActivityModel.HasNoCost),
                                colName =>
                                {
                                    if (bool.TryParse(row[colName]?.ToString(), out bool output))
                                    {
                                        hasNoCost = output;
                                    }
                                })
                            .Case(nameof(ActivityModel.Duration),
                                colName =>
                                {
                                    if (int.TryParse(row[colName]?.ToString(), out int output))
                                    {
                                        duration = output;
                                    }
                                })
                            .Case(nameof(ActivityModel.MinimumFreeSlack),
                                colName =>
                                {
                                    if (int.TryParse(row[colName]?.ToString(), out int output))
                                    {
                                        minimumFreeSlack = output;
                                    }
                                })
                            .Case(nameof(ActivityModel.MinimumEarliestStartTime),
                                colName =>
                                {
                                    if (int.TryParse(row[colName]?.ToString(), out int output))
                                    {
                                        minimumEarliestStartTime = output;
                                    }
                                })
                            .Case(nameof(ActivityModel.MinimumEarliestStartDateTime),
                                colName =>
                                {
                                    if (DateTime.TryParse(row[colName]?.ToString(), out DateTime output))
                                    {
                                        minimumEarliestStartDateTime = new DateTimeOffset(output);
                                    }
                                })
                            .Case(nameof(ActivityModel.MaximumLatestFinishTime),
                                colName =>
                                {
                                    if (int.TryParse(row[colName]?.ToString(), out int output))
                                    {
                                        maximumLatestFinishTime = output;
                                    }
                                })
                            .Case(nameof(ActivityModel.MaximumLatestFinishDateTime),
                                colName =>
                                {
                                    if (DateTime.TryParse(row[colName]?.ToString(), out DateTime output))
                                    {
                                        maximumLatestFinishDateTime = new DateTimeOffset(output);
                                    }
                                })
                            .Case(nameof(ActivityModel.Notes),
                                colName => notes = row[colName]?.ToString() ?? string.Empty);
                    }
                    foreach (string columnName in dependentActivityColumnNames)
                    {
                        columnName.ValueSwitchOn()
                            .Case(nameof(DependentActivityModel.Dependencies),
                                colName =>
                                {
                                    string dependenciesString = row[colName]?.ToString() ?? string.Empty;
                                    foreach (string dependency in dependenciesString.Split(DependenciesStringValidationRule.Separator))
                                    {
                                        if (int.TryParse(dependency, out int output))
                                        {
                                            dependencies.Add(output);
                                        }
                                    }
                                });
                    }

                    if (id is not null)
                    {
                        int idVal = id.GetValueOrDefault();
                        dependentActivities.TryAdd(idVal, new DependentActivityModel
                        {
                            Activity = new ActivityModel
                            {
                                Id = idVal,
                                Name = name,
                                TargetResources = targetResources,
                                TargetResourceOperator = targetResourceOperator,
                                HasNoCost = hasNoCost,
                                Duration = duration,
                                MinimumFreeSlack = minimumFreeSlack,
                                MinimumEarliestStartTime = minimumEarliestStartTime,
                                MinimumEarliestStartDateTime = minimumEarliestStartDateTime,
                                MaximumLatestFinishTime = maximumLatestFinishTime,
                                MaximumLatestFinishDateTime = maximumLatestFinishDateTime,
                                Notes = notes
                            },
                            Dependencies = dependencies
                        });
                    }
                }
            }
            return dependentActivities;
        }

        private static IDictionary<int, ResourceModel> ImportWorksheetResources(IWorkbook? workbook)
        {
            Dictionary<int, ResourceModel> resources = [];
            ISheet? sheet = workbook?.GetSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetResources);
            if (sheet is not null)
            {
                DataTable dtTable = SheetToDataTable(sheet);
                DataColumnCollection columns = dtTable.Columns;

                // Check columns.
                var columnNames = new List<string>();

                foreach (string title in s_ResourceColumnTitles)
                {
                    if (columns.Contains(title))
                    {
                        columnNames.Add(title);
                    }
                }

                foreach (DataRow row in dtTable.Rows)
                {
                    int? id = 0;
                    string name = string.Empty;
                    bool isExplicitTarget = false;
                    bool isInactive = false;
                    InterActivityAllocationType interActivityAllocationType = InterActivityAllocationType.None;
                    double unitCost = 0.0;
                    int displayOrder = 0;
                    int allocationOrder = 0;
                    ColorFormatModel colorFormat = ColorHelper.RandomColor();

                    foreach (string columnName in columnNames)
                    {
                        columnName.ValueSwitchOn()
                            .Case(nameof(ResourceModel.Id),
                                colName =>
                                {
                                    if (int.TryParse(row[colName]?.ToString(), out int output))
                                    {
                                        id = output;
                                    }
                                })
                            .Case(nameof(ResourceModel.Name),
                                colName => name = row[colName]?.ToString() ?? string.Empty)
                            .Case(nameof(ResourceModel.IsExplicitTarget),
                                colName =>
                                {
                                    if (bool.TryParse(row[colName]?.ToString(), out bool output))
                                    {
                                        isExplicitTarget = output;
                                    }
                                })
                            .Case(nameof(ResourceModel.IsInactive),
                                colName =>
                                {
                                    if (bool.TryParse(row[colName]?.ToString(), out bool output))
                                    {
                                        isInactive = output;
                                    }
                                })
                            .Case(nameof(ResourceModel.InterActivityAllocationType),
                                colName => interActivityAllocationType = row[colName]?.ToString().GetValueFromDescription<InterActivityAllocationType>() ?? default)
                            .Case(nameof(ResourceModel.UnitCost),
                                colName =>
                                {
                                    if (double.TryParse(row[colName]?.ToString(), out double output))
                                    {
                                        unitCost = output;
                                    }
                                })
                            .Case(nameof(ResourceModel.DisplayOrder),
                                colName =>
                                {
                                    if (int.TryParse(row[colName]?.ToString(), out int output))
                                    {
                                        displayOrder = output;
                                    }
                                })
                            .Case(nameof(ResourceModel.AllocationOrder),
                                colName =>
                                {
                                    if (int.TryParse(row[colName]?.ToString(), out int output))
                                    {
                                        allocationOrder = output;
                                    }
                                })
                            .Case(nameof(ResourceModel.ColorFormat),
                                colName =>
                                {
                                    string? hexCode = row[colName]?.ToString();
                                    if (!string.IsNullOrWhiteSpace(hexCode))
                                    {
                                        colorFormat = ColorHelper.HtmlHexCodeToColorFormat(hexCode);
                                    }
                                });
                    }

                    if (id is not null)
                    {
                        int idVal = id.GetValueOrDefault();
                        resources.TryAdd(idVal, new ResourceModel
                        {

                            Id = idVal,
                            Name = name,
                            IsExplicitTarget = isExplicitTarget,
                            IsInactive = isInactive,
                            InterActivityAllocationType = interActivityAllocationType,
                            UnitCost = unitCost,
                            DisplayOrder = displayOrder,
                            AllocationOrder = allocationOrder,
                            ColorFormat = colorFormat
                        });
                    }
                }
            }
            return resources;
        }

        private static IList<ActivitySeverityModel> ImportWorksheetActivitySeverities(IWorkbook? workbook)
        {
            List<ActivitySeverityModel> activitySeverities = [];
            ISheet? sheet = workbook?.GetSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetActivitySeverities);
            if (sheet is not null)
            {
                DataTable dtTable = SheetToDataTable(sheet);
                DataColumnCollection columns = dtTable.Columns;

                // Check columns.
                var columnNames = new List<string>();

                foreach (string title in s_ActivitySeverityColumnTitles)
                {
                    if (columns.Contains(title))
                    {
                        columnNames.Add(title);
                    }
                }

                foreach (DataRow row in dtTable.Rows)
                {
                    int slackLimit = 0;
                    double criticalityWeight = 0.0;
                    double fibonacciWeight = 0.0;
                    ColorFormatModel colorFormat = ColorHelper.RandomColor();

                    foreach (string columnName in columnNames)
                    {
                        columnName.ValueSwitchOn()
                            .Case(nameof(ActivitySeverityModel.SlackLimit),
                                colName =>
                                {
                                    if (int.TryParse(row[colName]?.ToString(), out int output))
                                    {
                                        slackLimit = output;
                                    }
                                })
                            .Case(nameof(ActivitySeverityModel.CriticalityWeight),
                                colName =>
                                {
                                    if (double.TryParse(row[colName]?.ToString(), out double output))
                                    {
                                        criticalityWeight = output;
                                    }
                                })
                            .Case(nameof(ActivitySeverityModel.FibonacciWeight),
                                colName =>
                                {
                                    if (double.TryParse(row[colName]?.ToString(), out double output))
                                    {
                                        fibonacciWeight = output;
                                    }
                                })
                            .Case(nameof(ResourceModel.ColorFormat),
                                colName =>
                                {
                                    string? hexCode = row[colName]?.ToString();
                                    if (!string.IsNullOrWhiteSpace(hexCode))
                                    {
                                        colorFormat = ColorHelper.HtmlHexCodeToColorFormat(hexCode);
                                    }
                                });
                    }

                    activitySeverities.Add(new ActivitySeverityModel
                    {
                        SlackLimit = slackLimit,
                        CriticalityWeight = criticalityWeight,
                        FibonacciWeight = fibonacciWeight,
                        ColorFormat = colorFormat
                    });
                }
            }
            return activitySeverities;
        }

        private static IDictionary<int, DependentActivityModel> ImportWorksheetTrackers(
            IWorkbook? workbook,
            IDictionary<int, DependentActivityModel> dependentActivities)
        {
            ISheet? percentageCompleteSheet = workbook?.GetSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetTrackerPercentageComplete);
            ISheet? daysIncludedSheet = workbook?.GetSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetTrackerDaysIncluded);
            if (percentageCompleteSheet is not null
                && daysIncludedSheet is not null)
            {
                DataTable percentageCompleteTable = SheetToDataTable(percentageCompleteSheet);
                int percentageCompleteColumnCount = percentageCompleteTable.Columns.Count;
                int percentageCompleteRowCount = percentageCompleteTable.Rows.Count;

                DataTable daysIncludedTable = SheetToDataTable(daysIncludedSheet);
                int daysIncludedColumnCount = daysIncludedTable.Columns.Count;
                int daysIncludedRowCount = daysIncludedTable.Rows.Count;

                if (percentageCompleteColumnCount == daysIncludedColumnCount
                    && percentageCompleteRowCount == daysIncludedRowCount)
                {
                    for (int rowIndex = 0; rowIndex < percentageCompleteRowCount; rowIndex++)
                    {
                        DataRow percentageCompleteRow = percentageCompleteTable.Rows[rowIndex];
                        DataRow daysIncludedRow = daysIncludedTable.Rows[rowIndex];

                        // Check IDs.
                        int columnIndex = 0;
                        int? percentageCompleteId = null;
                        int? daysIncludedId = null;

                        {
                            if (int.TryParse(percentageCompleteRow[columnIndex]?.ToString(), out int output))
                            {
                                percentageCompleteId = output;
                            }
                        }
                        {
                            if (int.TryParse(daysIncludedRow[columnIndex]?.ToString(), out int output))
                            {
                                daysIncludedId = output;
                            }
                        }

                        if (percentageCompleteId.HasValue
                            && daysIncludedId.HasValue
                            && percentageCompleteId == daysIncludedId
                            && dependentActivities.TryGetValue(percentageCompleteId.GetValueOrDefault(), out DependentActivityModel? dependentActivity))
                        {
                            dependentActivity.Activity.Trackers.Clear();

                            for (columnIndex = 1; columnIndex < percentageCompleteColumnCount; columnIndex++)
                            {
                                int percentageComplete = 0;
                                bool isIncluded = false;

                                {
                                    if (int.TryParse(percentageCompleteRow[columnIndex]?.ToString(), out int output))
                                    {
                                        percentageComplete = output;
                                    }
                                }
                                {
                                    if (bool.TryParse(daysIncludedRow[columnIndex]?.ToString(), out bool output))
                                    {
                                        isIncluded = output;
                                    }
                                }

                                int trackerIndex = columnIndex - 1;
                                dependentActivity.Activity.Trackers.Add(new TrackerModel
                                {
                                    Index = trackerIndex,
                                    Time = trackerIndex,
                                    ActivityId = dependentActivity.Activity.Id,
                                    PercentageComplete = percentageComplete,
                                    IsIncluded = isIncluded
                                });
                            }
                        }
                    }
                }
            }
            return dependentActivities;
        }

        #endregion
    }
}
