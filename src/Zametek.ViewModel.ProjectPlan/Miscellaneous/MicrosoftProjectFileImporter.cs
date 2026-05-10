using net.sf.mpxj.MpxjUtilities;
using net.sf.mpxj.reader;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class MicrosoftProjectFileImporter
        : IMicrosoftProjectFileImporter
    {
        #region Fields

        private readonly ISettingService m_SettingService;

        private static readonly int[] s_FilterTaskIds = [0];

        private static readonly int[] s_FilterResourceIds = [0];

        #endregion

        #region Ctors

        public MicrosoftProjectFileImporter(ISettingService settingService)
        {
            ArgumentNullException.ThrowIfNull(settingService);
            m_SettingService = settingService;
        }

        #endregion

        #region Private Members

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
                    int? dependentTaskId = pred.PredecessorTask?.ID?.intValue();

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

        #endregion

        #region IMicrosoftProjectFileImporter Members

        public ProjectScenarioImportModel ImportMicrosoftProjectFile(string filename)
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
                    ColorFormat = ColorHelper.Random()
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

                        foreach (net.sf.mpxj.Task descendantTask in GetDescendantTasks(pred.PredecessorTask))
                        {
                            int? descendantTaskId = descendantTask.ID?.intValue();

                            if (descendantTaskId is not null
                                && id != descendantTaskId)
                            {
                                var builder = new net.sf.mpxj.Relation.Builder();
                                builder.PredecessorTask(descendantTask);
                                builder.Type(net.sf.mpxj.RelationType.START_FINISH);
                                builder.Lag(net.sf.mpxj.Duration.getInstance(0.0, mpxjTask.Duration.Units));
                                mpxjTask.AddPredecessor(builder);
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
                        var builder = new net.sf.mpxj.Relation.Builder();
                        builder.PredecessorTask(parentTask);
                        builder.Type(net.sf.mpxj.RelationType.START_FINISH);
                        builder.Lag(net.sf.mpxj.Duration.getInstance(0.0, mpxjTask.Duration.Units));
                        mpxjTask.AddPredecessor(builder);
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

            bool showDates = m_SettingService.DefaultShowDates;
            bool useClassicDates = m_SettingService.DefaultUseClassicDates;
            NonWorkingDayMode nonWorkingDayMode = m_SettingService.DefaultNonWorkingDayMode;
            bool hideCost = m_SettingService.DefaultHideCost;
            bool hideBilling = m_SettingService.DefaultHideBilling;

            return new ProjectScenarioImportModel
            {
                ProjectStart = projectStart,
                Today = new(DateTime.Today),
                DependentActivities = dependentActivities,
                ResourceSettings = new ResourceSettingsModel
                {
                    Resources = resources
                },
                DisplaySettings = new()
                {
                    ShowDates = showDates,
                    UseClassicDates = useClassicDates,
                    NonWorkingDayMode = nonWorkingDayMode,
                    HideCost = hideCost,
                    HideBilling = hideBilling,
                }
            };
        }

        #endregion
    }
}
