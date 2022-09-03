using net.sf.mpxj.MpxjUtilities;
using net.sf.mpxj.reader;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;
using System.Xml;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ProjectFileImport
        : IProjectFileImport
    {
        #region Fields

        private static readonly IList<string> s_GeneralColumnTitles = new List<string>
        {
            nameof(ProjectPlanModel.ProjectStart)
        };













        private const int c_DefaultMinutesPerDay = 60 * 60 * 8;

        #endregion







        private static DataTable SheetToDataTable(ISheet sheet!!)
        {
            DataTable dtTable = new();
            IRow titleRow = sheet.GetRow(0);
            int cellCount = titleRow.LastCellNum;

            for (int i = 0; i < cellCount; i++)
            {
                ICell cell = titleRow.GetCell(i);
                if (cell is not null
                    && !string.IsNullOrWhiteSpace(cell.ToString()))
                {
                    dtTable.Columns.Add(cell.ToString());
                }
            }

            List<string> rowList = new();
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
                    if (row.GetCell(j) is not null)
                    {
                        string? content = row.GetCell(j).ToString();

                        if (!string.IsNullOrWhiteSpace(content))
                        {
                            rowList.Add(content);
                        }
                    }
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
                .Case($".{Resource.ProjectPlan.Filters.Filter_MicrosoftProjectMppFileExtension}", _ => func = ImportMicrosoftProjectMppFile)
                .Case($".{Resource.ProjectPlan.Filters.Filter_MicrosoftProjectXmlFileExtension}", _ => func = ImportMicrosoftProjectXmlFile)
                .Case($".{Resource.ProjectPlan.Filters.Filter_ProjectXlsxFileExtension}", _ => func = ImportProjectXlsxFile);

            return func(filename);
        }

        public async Task<ProjectImportModel> ImportProjectFileAsync(string filename)
        {
            return await Task.Run(() => ImportProjectFile(filename));
        }

        public ProjectImportModel ImportMicrosoftProjectMppFile(string filename)
        {
            ProjectReader reader = ProjectReaderUtility.getProjectReader(filename);
            net.sf.mpxj.ProjectFile mpx = reader.read(filename);
            net.sf.mpxj.ProjectProperties props = mpx.getProjectProperties();
            DateTimeOffset projectStart = props.getStartDate().ToDateTime();
            TimeSpan projectStartOffset = projectStart.Offset;

            var resources = new List<ResourceModel>();
            foreach (net.sf.mpxj.Resource mpxjResource in mpx.getResources().ToIEnumerable<net.sf.mpxj.Resource>())
            {
                int id = mpxjResource.getID().intValue();
                if (id == 0)
                {
                    continue;
                }
                var resource = new ResourceModel
                {
                    Id = id,
                    IsExplicitTarget = true,
                    Name = mpxjResource.getName(),
                    DisplayOrder = id,
                    ColorFormat = new ColorFormatModel()
                };
                resources.Add(resource);
            }

            var dependentActivities = new List<DependentActivityModel>();
            foreach (net.sf.mpxj.Task mpxjTask in mpx.getTasks().ToIEnumerable<net.sf.mpxj.Task>())
            {
                int id = mpxjTask.getID().intValue();
                if (id == 0)
                {
                    continue;
                }
                string name = mpxjTask.getName();
                int duration = Convert.ToInt32(mpxjTask.getDuration().getDuration());

                DateTimeOffset? minimumEarliestStartDateTime = null;
                if (mpxjTask.getConstraintType() == net.sf.mpxj.ConstraintType.START_NO_EARLIER_THAN)
                {
                    // Ensure each value has the same offset as the project start;
                    minimumEarliestStartDateTime = new DateTimeOffset(mpxjTask.getConstraintDate().ToDateTime(), projectStartOffset);
                }

                var targetResources = new List<int>();
                foreach (net.sf.mpxj.ResourceAssignment resourceAssignment in mpxjTask.getResourceAssignments().ToIEnumerable<net.sf.mpxj.ResourceAssignment>())
                {
                    if (resourceAssignment.getResource() is not null)
                    {
                        targetResources.Add(resourceAssignment.getResource().getID().intValue());
                    }
                }

                var dependencies = new List<int>();
                java.util.List? preds = mpxjTask.getPredecessors();
                if (preds is not null && !preds.isEmpty())
                {
                    foreach (net.sf.mpxj.Relation pred in preds.ToIEnumerable<net.sf.mpxj.Relation>())
                    {
                        dependencies.Add(pred.getTargetTask().getID().intValue());
                    }
                }
                var dependentActivity = new DependentActivityModel
                {
                    Activity = new ActivityModel
                    {
                        Id = id,
                        Name = name,
                        TargetResources = targetResources,
                        Duration = duration,
                        MinimumEarliestStartDateTime = minimumEarliestStartDateTime
                    },
                    Dependencies = dependencies,
                };
                dependentActivities.Add(dependentActivity);
            }

            return new ProjectImportModel
            {
                ProjectStart = projectStart,
                DependentActivities = dependentActivities,
                Resources = resources
            };
        }

        public async Task<ProjectImportModel> ImportMicrosoftProjectMppFileAsync(string filename)
        {
            return await Task.Run(() => ImportMicrosoftProjectMppFile(filename));
        }

        public ProjectImportModel ImportMicrosoftProjectXmlFile(string filename)
        {
            var xDoc = new XmlDocument();
            var nsMan = new XmlNamespaceManager(xDoc.NameTable);
            nsMan.AddNamespace(@"ns", "http://schemas.microsoft.com/project");
            xDoc.Load(filename);

            string? projectStartText = xDoc[@"Project"]?[@"StartDate"]?.InnerText;
            DateTimeOffset projectStart =
                projectStartText is not null
                ? XmlConvert.ToDateTimeOffset(projectStartText)
                : new DateTimeOffset(DateTime.Today);
            TimeSpan projectStartOffset = projectStart.Offset;

            string? minutesPerDayText = xDoc[@"Project"]?[@"MinutesPerDay"]?.InnerText;
            int minutesPerDay =
                minutesPerDayText is not null
                ? XmlConvert.ToInt32(minutesPerDayText)
                : c_DefaultMinutesPerDay;

            // Resources.
            var resources = new List<ResourceModel>();
            var resourceUidToIdLookup = new Dictionary<int, int>();

            XmlNodeList? projectResources = xDoc[@"Project"]?[@"Resources"]?.ChildNodes;
            if (projectResources is not null)
            {
                foreach (XmlNode projectResource in projectResources)
                {
                    string? resourceUidText = projectResource[@"UID"]?.InnerText;
                    int resourceUid = resourceUidText is not null ? XmlConvert.ToInt32(resourceUidText) : 0;

                    string? resourceIdText = projectResource[@"ID"]?.InnerText;
                    int resourceId = resourceIdText is not null ? XmlConvert.ToInt32(resourceIdText) : 0;

                    if (resourceUid == 0 || resourceId == 0)
                    {
                        continue;
                    }

                    string name = projectResource[@"Name"]?.InnerText ?? string.Empty;
                    string? costText = projectResource[@"Cost"]?.InnerText;
                    double cost = costText is not null ? XmlConvert.ToDouble(costText) : 0.0;
                    var resource = new ResourceModel
                    {
                        Id = resourceId,
                        IsExplicitTarget = true,
                        Name = name,
                        DisplayOrder = resourceId,
                        UnitCost = cost,
                        ColorFormat = new ColorFormatModel()
                    };
                    resources.Add(resource);
                    resourceUidToIdLookup.Add(resourceUid, resourceId);
                }
            }

            // Tasks.
            var taskUidToIdLookup = new Dictionary<int, int>();

            XmlNodeList? projectTasks = xDoc[@"Project"]?[@"Tasks"]?.ChildNodes;
            if (projectTasks is not null)
            {
                foreach (XmlNode projectTask in projectTasks)
                {
                    string? taskUidText = projectTask[@"UID"]?.InnerText;
                    int taskUid = taskUidText is not null ? XmlConvert.ToInt32(taskUidText) : 0;

                    string? taskIdText = projectTask[@"ID"]?.InnerText;
                    int taskId = taskIdText is not null ? XmlConvert.ToInt32(taskIdText) : 0;

                    if (taskUid == 0 || taskId == 0)
                    {
                        continue;
                    }

                    taskUidToIdLookup.Add(taskUid, taskId);
                }
            }

            // Resource assignments.
            var resourceAssignmentLookup = new Dictionary<int, IList<int>>();

            XmlNodeList? projectAssignments = xDoc[@"Project"]?[@"Assignments"]?.ChildNodes;
            if (projectAssignments is not null)
            {
                foreach (XmlNode projectAssignment in projectAssignments)
                {
                    string? taskUidText = projectAssignment[@"TaskUID"]?.InnerText;
                    int taskUid = taskUidText is not null ? XmlConvert.ToInt32(taskUidText) : 0;

                    string? resourceUidText = projectAssignment[@"ResourceUID"]?.InnerText;
                    int resourceUid = resourceUidText is not null ? XmlConvert.ToInt32(resourceUidText) : 0;

                    if (taskUid == 0 || resourceUid == 0)
                    {
                        continue;
                    }

                    if (taskUidToIdLookup.TryGetValue(taskUid, out int taskId)
                        && resourceUidToIdLookup.TryGetValue(resourceUid, out int resourceId))
                    {
                        if (!resourceAssignmentLookup.TryGetValue(taskId, out IList<int>? resourceAssignments))
                        {
                            resourceAssignments = new List<int>();
                            resourceAssignmentLookup.Add(taskId, resourceAssignments);
                        }

                        resourceAssignments.Add(resourceId);
                    }
                }
            }

            // Cycle through tasks.
            var dependentActivities = new List<DependentActivityModel>();
            if (projectTasks is not null)
            {
                foreach (XmlNode projectTask in projectTasks)
                {
                    string? taskIdText = projectTask[@"ID"]?.InnerText;
                    int taskId = taskIdText is not null ? XmlConvert.ToInt32(taskIdText) : 0;

                    if (taskId == 0)
                    {
                        continue;
                    }

                    string name = projectTask[@"Name"]?.InnerText ?? string.Empty;

                    string? totalDurationMinutesText = projectTask[@"Duration"]?.InnerText;
                    double totalDurationMinutes = totalDurationMinutesText is not null ? XmlConvert.ToTimeSpan(totalDurationMinutesText).TotalMinutes : 0.0;
                    int durationDays = Convert.ToInt32(totalDurationMinutes / minutesPerDay);

                    DateTimeOffset? minimumEarliestStartDateTime = null;
                    string? constraintTypeText = projectTask[@"ConstraintType"]?.InnerText;
                    if (string.Equals(constraintTypeText, @"4", StringComparison.OrdinalIgnoreCase)) // START_NO_EARLIER_THAN
                    {
                        string? constraintDateText = projectTask[@"ConstraintDate"]?.InnerText;

                        // Ensure each value has the same offset as the project start;
                        minimumEarliestStartDateTime =
                            constraintDateText is not null
                            ? new DateTimeOffset(XmlConvert.ToDateTime(constraintDateText, XmlDateTimeSerializationMode.Unspecified), projectStartOffset)
                            : null;
                    }

                    if (!resourceAssignmentLookup.TryGetValue(taskId, out IList<int>? targetResources))
                    {
                        targetResources = new List<int>();
                    }

                    var dependencies = new List<int>();
                    XmlNodeList? predecessorNodes = projectTask.SelectNodes(@"./ns:PredecessorLink", nsMan);
                    if (predecessorNodes is not null)
                    {
                        // Do not forget namespaces.
                        // https://stackoverflow.com/questions/33125519/how-to-get-text-from-ms-projects-xml-file-in-c
                        foreach (XmlNode predecessorNode in predecessorNodes)
                        {
                            string? predecessorUidText = predecessorNode[@"PredecessorUID"]?.InnerText;
                            int predecessorUid = predecessorUidText is not null ? XmlConvert.ToInt32(predecessorUidText) : 0;

                            if (predecessorUid == 0)
                            {
                                continue;
                            }

                            if (taskUidToIdLookup.TryGetValue(predecessorUid, out int predecessorId))
                            {
                                dependencies.Add(predecessorId);
                            }
                        }
                    }

                    var dependentActivity = new DependentActivityModel
                    {
                        Activity = new ActivityModel
                        {
                            Id = taskId,
                            Name = name,
                            TargetResources = targetResources.ToList(),
                            Duration = durationDays,
                            MinimumEarliestStartDateTime = minimumEarliestStartDateTime
                        },
                        Dependencies = dependencies
                    };
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

        public async Task<ProjectImportModel> ImportMicrosoftProjectXmlFileAsync(string filename)
        {
            return await Task.Run(() => ImportMicrosoftProjectXmlFile(filename));
        }

















        public ProjectImportModel ImportProjectXlsxFile(string filename)
        {


            using FileStream file = new(filename, FileMode.Open, FileAccess.Read);

            IWorkbook workbook = new XSSFWorkbook(file);

            if (workbook is null)
            {
                return new ProjectImportModel();
            }

            {
                ISheet? sheet = workbook.GetSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetGeneral);

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
                        DateTimeOffset projectStart = new(DateTime.Today);

                        foreach (string columnName in columnNames)
                        {

                            var a = row[columnName];



                            //columnName.ValueSwitchOn()
                            //    .Case(nameof(ProjectImportModel.ProjectStart),
                            //        x =>
                            //        {
                            //            if (DateTime.TryParse(x.ToString(), out DateTime ps))
                            //            {
                            //                projectStart = new DateTimeOffset(ps);
                            //            }
                            //        });
                        }
                    }
                }
            }

















            throw new NotImplementedException();
        }

        #endregion
    }
}
