using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Data.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public abstract class SettingServiceBase
        : ViewModelBase, ISettingService
    {
        #region Fields

        private readonly Lock m_Lock;
        protected AppSettingsModel m_AppSettingsModel;

        private static readonly double s_GoldenRatio = (1.0 + Math.Sqrt(5.0)) / 2.0;

        #endregion

        #region Ctors

        public SettingServiceBase(string settingsFilename)
        {
            m_Lock = new();
            m_ProjectTitle = string.Empty;
            m_ProjectId = Guid.NewGuid();
            m_ScenarioTitle = string.Empty;
            m_ScenarioId = Guid.NewGuid();
            m_AppSettingsModel = new()
            {
                Version = Versions.AppSettingsLatest,
            };
            SettingsFilename = settingsFilename;
            DockLayoutFilename = string.Empty;
            DataGridLayoutFilename = string.Empty;

            if (File.Exists(SettingsFilename))
            {
                try
                {
                    m_AppSettingsModel = ReadSettings(SettingsFilename) ?? m_AppSettingsModel;
                }
                catch (Exception ex)
                {
                    // The start must not stop for a settings file that cannot be read: a write that did not finish, or a
                    // file that a person changed. The service starts with the default settings. It moves the file out of
                    // the way, so that the next start finds none and the next write does not lose what the file holds.
                    SettingsReadFailure = ex;
                    UnreadableSettingsFilename = SetAside(SettingsFilename);
                }
            }
        }

        // The settings that a file holds, or null when the file is empty. A file that is not JSON, or whose values are not
        // of the kinds that its version has, throws.
        private static AppSettingsModel? ReadSettings(string settingsFilename)
        {
            string content = File.ReadAllText(settingsFilename);

            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            JObject json = JObject.Parse(content);
            string version =
                json?.GetValue(nameof(AppSettingsModel.Version), StringComparison.OrdinalIgnoreCase)?.ToString()
                ?? string.Empty;
            string jsonString = json?.ToString() ?? string.Empty;

            Func<string, AppSettingsModel> func =
                jString => new AppSettingsModel();

            version.ValueSwitchOn()
                .Case(Versions.v0_3_0, x =>
                {
                    func = jString => Converter.Upgrade(
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_3_0.AppSettingsModel>(jString)
                        ?? new Data.ProjectPlan.v0_3_0.AppSettingsModel());
                })
                .Case(Versions.v0_4_1, x =>
                {
                    func = jString => Converter.Upgrade(
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_4_1.AppSettingsModel>(jString)
                        ?? new Data.ProjectPlan.v0_4_1.AppSettingsModel());
                })
                .Case(Versions.v0_4_4, x =>
                {
                    func = jString => Converter.Upgrade(
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_4_4.AppSettingsModel>(jString)
                        ?? new Data.ProjectPlan.v0_4_4.AppSettingsModel());
                })
                .Case(Versions.v0_6_0, x =>
                {
                    func = jString => Converter.Upgrade(
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_6_0.AppSettingsModel>(jString)
                        ?? new Data.ProjectPlan.v0_6_0.AppSettingsModel());
                })
                .Case(Versions.v0_6_1, x =>
                {
                    func = jString => Converter.Upgrade(
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_6_1.AppSettingsModel>(jString)
                        ?? new Data.ProjectPlan.v0_6_1.AppSettingsModel());
                });

            return func(jsonString);
        }

        // Moves a settings file that cannot be read to the same name with .bad after it, in place of an earlier one. Gives
        // the new name, or null when the file cannot be moved.
        private static string? SetAside(string settingsFilename)
        {
            string aside = $@"{settingsFilename}.bad";

            try
            {
                File.Move(settingsFilename, aside, overwrite: true);
                return aside;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        #endregion

        #region Public Members

        // Why the settings file could not be read, when the service started with the default settings in its place. Null
        // when the file was read, or when there was none.
        public Exception? SettingsReadFailure { get; }

        // Where the settings file that could not be read is now. Null when the service could not move it, and it is then
        // still in its place.
        public string? UnreadableSettingsFilename { get; }

        #endregion

        #region ISettingService Members

        public string SettingsFilename { get; init; }

        public string DockLayoutFilename { get; init; }

        public string DataGridLayoutFilename { get; init; }

        private string m_ProjectTitle;
        public string ProjectTitle
        {
            get => string.IsNullOrWhiteSpace(m_ProjectTitle) ? string.Empty : m_ProjectTitle;
            protected set
            {
                lock (m_Lock)
                {
                    m_ProjectTitle = value;
                }
            }
        }

        private Guid m_ProjectId;
        public Guid ProjectId
        {
            get => m_ProjectId;
            protected set
            {
                lock (m_Lock)
                {
                    m_ProjectId = value == Guid.Empty ? Guid.NewGuid() : value;
                }
            }
        }

        private string m_ScenarioTitle;
        public string ScenarioTitle
        {
            get => string.IsNullOrWhiteSpace(m_ScenarioTitle) ? string.Empty : m_ScenarioTitle;
            protected set
            {
                lock (m_Lock)
                {
                    m_ScenarioTitle = value;
                }
            }
        }

        private Guid m_ScenarioId;
        public Guid ScenarioId
        {
            get => m_ScenarioId;
            protected set
            {
                lock (m_Lock)
                {
                    m_ScenarioId = value == Guid.Empty ? Guid.NewGuid() : value;
                }
            }
        }

        public abstract string DockLayout { get; set; }

        public abstract IList<DataGridModel> GetDataGridLayout();

        public abstract void SetDataGridLayout(IList<DataGridModel> models);

        public abstract bool DefaultShowDates { get; set; }

        public abstract bool DefaultUseClassicDates { get; set; }

        public abstract NonWorkingDayMode DefaultNonWorkingDayMode { get; set; }

        public abstract bool DefaultHideCost { get; set; }

        public abstract bool DefaultHideBilling { get; set; }

        public abstract string SelectedTheme { get; set; }

        public abstract int MaxRecentProjectFilePaths { get; }

        public abstract int CompilationTimeoutMilliseconds { get; set; }

        public abstract IReadOnlyList<string> RecentProjectFilePaths { get; }

        public abstract void RecordRecentProjectFilePath(string filename);

        public abstract void RemoveRecentProjectFilePath(string filename);

        public abstract void ClearRecentProjectFilePaths();

        public abstract string ProjectDirectory { get; protected set; }

        public bool IsTitleBoundToFilename { get; set; }

        public void SetProjectFilePath(
            string filename,
            bool bindTitleToFilename)
        {
            SetProjectTitle(filename);
            SetProjectDirectory(filename);
            IsTitleBoundToFilename = bindTitleToFilename;
        }

        public void SetProjectTitle(string filename)
        {
            ProjectTitle = FileFormatHelper.GetProjectTitle(filename);
        }

        public void SetProjectId(Guid projectId)
        {
            ProjectId = projectId;
        }

        public void SetProjectDirectory(string filename)
        {
            ProjectDirectory = Path.GetDirectoryName(filename) ?? string.Empty;
        }

        public void SetProjectScenarioTitle(string name)
        {
            ScenarioTitle = name.Trim();
        }

        public void SetProjectScenarioId(Guid scenarioId)
        {
            ScenarioId = scenarioId;
        }

        public GraphSettingsModel DefaultGraphSettings =>
            new()
            {
                NodeTypeFormats = DefaultFormatCollections.NodeTypeFormats,
                EdgeTypeFormats = DefaultFormatCollections.EdgeTypeFormats,
                ActivitySeverities =
                    [
                        // Black.
                        new()
                        {
                            SlackLimit = 1,
                            CriticalityWeight = 4.0,
                            FibonacciWeight = Math.Pow(s_GoldenRatio, 3.0),
                            ColorFormat = ColorHelper.Black()
                        },
                        // Red.
                        new()
                        {
                            SlackLimit = 9,
                            CriticalityWeight = 3.0,
                            FibonacciWeight = Math.Pow(s_GoldenRatio, 2.0),
                            ColorFormat = ColorHelper.Red()
                        },
                        // Gold.
                        new()
                        {
                            SlackLimit = 25,
                            CriticalityWeight = 2.0,
                            FibonacciWeight = Math.Pow(s_GoldenRatio, 1.0),
                            ColorFormat = ColorHelper.Gold()
                        },
                        // Green.
                        new()
                        {
                            SlackLimit = int.MaxValue,
                            CriticalityWeight = 1.0,
                            FibonacciWeight = Math.Pow(s_GoldenRatio, 0.0),
                            ColorFormat = ColorHelper.Green()
                        }
                    ],
            };

        public ResourceSettingsModel DefaultResourceSettings =>
            new()
            {
                DefaultUnitCost = 1.0,
                DefaultUnitBilling = 1.0,
                AreDisabled = false
            };

        public WorkStreamSettingsModel DefaultWorkStreamSettings => new();

        public HolidaySettingsModel DefaultHolidaySettings =>
            new()
            {
                Holidays =
                    [
                        // Weekends.
                        new()
                        {
                            Id = 1,
                            Name = Resource.ProjectPlan.Messages.Message_DefaultWeekendsName,
                            Notes = Resource.ProjectPlan.Messages.Message_DefaultWeekendsNotes,
                            RecurrencePattern = "FREQ=WEEKLY;BYDAY=SA,SU",
                        },
                        // Christmas Day.
                        new()
                        {
                            Id = 2,
                            Name = Resource.ProjectPlan.Messages.Message_DefaultChristmasDayName,
                            Notes = Resource.ProjectPlan.Messages.Message_DefaultChristmasDayNotes,
                            RecurrencePattern = "FREQ=YEARLY;BYMONTH=12;BYMONTHDAY=25",
                        },
                        // New Year's Day.
                        new()
                        {
                            Id = 3,
                            Name = Resource.ProjectPlan.Messages.Message_DefaultNewYearsDayName,
                            Notes = Resource.ProjectPlan.Messages.Message_DefaultNewYearsDayNotes,
                            RecurrencePattern = "FREQ=YEARLY;BYMONTH=1;BYMONTHDAY=1",
                        },
                    ],
            };

        public void ResetProject()
        {
            ProjectTitle = string.Empty;
            ProjectId = Guid.NewGuid();
        }

        public void ResetProjectScenario()
        {
            ScenarioTitle = string.Empty;
            ScenarioId = Guid.NewGuid();
        }

        #endregion
    }
}
