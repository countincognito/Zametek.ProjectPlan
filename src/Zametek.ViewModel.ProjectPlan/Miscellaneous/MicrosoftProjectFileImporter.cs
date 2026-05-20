using MPXJ.Net;
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

        private static IEnumerable<MPXJ.Net.Task> GetDescendantTasks(MPXJ.Net.Task parentTask)
        {
            if (parentTask.HasChildTasks)
            {
                foreach (MPXJ.Net.Task childTask in parentTask.ChildTasks)
                {
                    foreach (MPXJ.Net.Task grandChildTask in GetDescendantTasks(childTask))
                    {
                        yield return grandChildTask;
                    }

                    yield return childTask;
                }
            }
            yield return parentTask;
        }

        private static DependentActivityModel? ConvertTask(
            MPXJ.Net.Task mpxjTask,
            TimeSpan projectStartOffset)
        {
            int id = mpxjTask.ID ?? default;
            if (s_FilterTaskIds.Contains(id))
            {
                return null;
            }

            int duration = Convert.ToInt32(mpxjTask.Duration.DurationValue);

            DateTimeOffset? minimumEarliestStartDateTime = null;
            if (mpxjTask.ConstraintType == ConstraintType.StartNoEarlierThan)
            {
                DateTime? mpxContraintDate = mpxjTask.ConstraintDate;

                if (mpxContraintDate is not null)
                {
                    // Ensure each value has the same offset as the project start;
                    minimumEarliestStartDateTime = new DateTimeOffset(mpxContraintDate.GetValueOrDefault(), projectStartOffset);
                }
            }

            var targetResources = new List<int>();
            foreach (ResourceAssignment resourceAssignment in mpxjTask.ResourceAssignments)
            {
                int? mpxResourceId = resourceAssignment.Resource?.ID;

                if (mpxResourceId is not null)
                {
                    targetResources.Add(mpxResourceId.GetValueOrDefault());
                }
            }

            var dependencies = new List<int>();
            IList<Relation> preds = [.. mpxjTask.Predecessors];
            if (preds is not null
                && preds.Count != 0)
            {
                foreach (Relation pred in preds)
                {
                    int? dependentTaskId = pred.PredecessorTask?.ID;

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
            ProjectFile mpxjProjectFile = reader.Read(filename);
            ProjectProperties? props = mpxjProjectFile.ProjectProperties;
            DateTimeOffset projectStart = props?.StartDate ?? DateTimeOffset.Now;
            TimeSpan projectStartOffset = projectStart.Offset;

            var resources = new List<ResourceModel>();

            foreach (MPXJ.Net.Resource mpxjResource in mpxjProjectFile.Resources)
            {
                int id = mpxjResource.ID ?? default;
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

            IList<MPXJ.Net.Task> mpxjTasks = [.. mpxjProjectFile.Tasks];

            foreach (MPXJ.Net.Task mpxjTask in mpxjTasks)
            {
                int id = mpxjTask.ID ?? default;
                if (s_FilterTaskIds.Contains(id))
                {
                    continue;
                }

                IList<Relation> preds = [.. mpxjTask.Predecessors]; // Predecessors == Dependencies
                if (preds is not null
                    && preds.Count != 0)
                {
                    foreach (Relation pred in preds)
                    {
                        // For every dependency that is also a parent task, add all its children as dependencies.

                        foreach (MPXJ.Net.Task descendantTask in GetDescendantTasks(pred.PredecessorTask))
                        {
                            int? descendantTaskId = descendantTask.ID;

                            if (descendantTaskId is not null
                                && id != descendantTaskId)
                            {
                                var builder = new Relation.Builder(mpxjProjectFile);
                                builder.PredecessorTask(descendantTask);
                                builder.Type(RelationType.StartFinish);
                                builder.Lag(Duration.GetInstance(0.0, TimeUnit.Days));
                                mpxjTask.AddPredecessor(builder);
                            }
                        }
                    }
                }

                // Add parent tasks as predecessors.

                MPXJ.Net.Task parentTask = mpxjTask.ParentTask;

                if (parentTask is not null)
                {
                    int parentId = parentTask.ID ?? default;
                    if (!s_FilterTaskIds.Contains(parentId))
                    {
                        var builder = new Relation.Builder(mpxjProjectFile);
                        builder.PredecessorTask(parentTask);
                        builder.Type(RelationType.StartFinish);
                        builder.Lag(Duration.GetInstance(0.0, TimeUnit.Days));
                        mpxjTask.AddPredecessor(builder);
                    }
                }

                // If the task is a parent task, ignore duration.

                if (mpxjTask.HasChildTasks)
                {
                    mpxjTask.Duration = Duration.GetInstance(0.0, TimeUnit.Days);
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
